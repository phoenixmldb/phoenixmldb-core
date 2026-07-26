using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Xml;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Default <see cref="IXmlResourceResolver"/> implementation: resolves <c>file:</c> (local
/// filesystem) URIs directly and, unless <see cref="AllowRemote"/> is set, blocks any other
/// scheme (e.g. <c>http:</c>, <c>https:</c>) as a security precaution against
/// server-side-request-forgery via <c>xi:include href</c>.
/// </summary>
public sealed class LocalFileResourceResolver : IXmlResourceResolver
{
    /// <summary>
    /// Whether non-local (e.g. <c>http:</c>/<c>https:</c>) URIs may be fetched. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool AllowRemote { get; init; }

    /// <summary>
    /// Maximum size (characters for XML, bytes for text) of a single resolved resource; <c>&lt;= 0</c>
    /// = unlimited. An oversized resource surfaces as a resource error (fallback-eligible).
    /// </summary>
    public long MaxResourceBytes { get; init; }

    /// <summary>
    /// Shared client for the (opt-in) <c>AllowRemote</c> <c>parse="text"</c> fetch. Auto-redirect
    /// is disabled so an allowed fetch cannot be bounced to an unintended host (e.g. a cloud
    /// metadata endpoint), and a bounded timeout prevents a hung remote from stalling the caller.
    /// </summary>
    private static readonly HttpClient RemoteTextClient = new(
        new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <inheritdoc />
    public XmlReader ResolveXml(Uri absolute)
    {
        if (!absolute.IsAbsoluteUri)
        {
            throw new XIncludeException(isFatal: true, $"XInclude resource URI must be absolute: '{absolute}'.");
        }

        // Secure settings for the sub-parse: no DTD processing (blocks XXE / billion-laughs)
        // and no further external-entity/URI resolution. Whitespace is preserved by default
        // (IgnoreWhitespace = false) so included content round-trips faithfully. XmlResolver
        // stays null here (the reader must not itself dereference anything); the local-file
        // branch below opens its own stream, and the remote branch below builds a separate
        // settings instance with a resolver that can actually fetch.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
            CloseInput = true,
        };
        if (MaxResourceBytes > 0)
        {
            settings.MaxCharactersInDocument = MaxResourceBytes;
        }

        // Uri.IsFile is true for BOTH local paths (file:///c:/x.xml) and UNC paths
        // (file://attacker-host/share/x.xml, IsUnc == true, LocalPath == \\attacker-host\
        // share\x.xml). A UNC file: URI reaches a remote host over SMB and must go through
        // the same AllowRemote gate as http/https — otherwise it's an SSRF bypass. Any
        // host-bearing file: URI (including file://localhost/...) parses as IsUnc == true,
        // so only a hostless file: URI is treated as genuinely local.
        var isLocalFile = absolute.IsFile
            && !absolute.IsUnc
            && string.IsNullOrEmpty(absolute.Host);

        if (isLocalFile)
        {
            var stream = File.OpenRead(absolute.LocalPath);
            try
            {
                return XmlReader.Create(stream, settings);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        if (!AllowRemote)
        {
            throw new XIncludeException(
                isFatal: true,
                $"XInclude remote resource blocked (AllowRemote is false): '{absolute}'.");
        }

        // AllowRemote = true: fetch the resource for real. XmlUrlResolver handles http(s)
        // (and, for a UNC file: URI, the SMB fetch) for the *initial* input only — DTD
        // processing stays Prohibit so any DTD/external-entity reference inside the fetched
        // content is still blocked (no XXE via a remote document).
        var remoteSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = new XmlUrlResolver(),
            IgnoreWhitespace = false,
            CloseInput = true,
        };
        if (MaxResourceBytes > 0)
        {
            remoteSettings.MaxCharactersInDocument = MaxResourceBytes;
        }

        return XmlReader.Create(absolute.AbsoluteUri, remoteSettings);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure reading/fetching the text resource (I/O, network, ...) " +
            "must be surfaced as a non-fatal XIncludeException so ProcessInclude can route it " +
            "through fallback recovery.")]
    public string ResolveText(Uri absolute, string? encoding, string? accept, string? acceptLanguage)
    {
        if (!absolute.IsAbsoluteUri)
        {
            // A non-absolute URI is a caller error, not a fetch failure — fatal (not
            // fallback-eligible), matching ResolveXml.
            throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: true,
                $"XInclude resource URI must be absolute: '{absolute}'.");
        }

        var isLocalFile = absolute.IsFile && !absolute.IsUnc && string.IsNullOrEmpty(absolute.Host);

        Encoding? enc = null;
        if (!string.IsNullOrEmpty(encoding))
        {
            try
            {
                enc = Encoding.GetEncoding(encoding);
            }
            catch (ArgumentException ex)
            {
                throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                    $"Unknown parse=text encoding '{encoding}'.", ex);
            }
        }

        if (isLocalFile)
        {
            try
            {
                if (MaxResourceBytes > 0)
                {
                    var len = new FileInfo(absolute.LocalPath).Length;
                    if (len > MaxResourceBytes)
                    {
                        throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                            $"resource '{absolute}' exceeds MaxResourceBytes ({MaxResourceBytes}).");
                    }
                }

                var bytes = File.ReadAllBytes(absolute.LocalPath);
                // Explicit encoding wins; else detect BOM; else UTF-8.
                if (enc != null)
                {
                    return enc.GetString(StripBom(bytes, enc));
                }

                return DecodeWithBomOrUtf8(bytes);
            }
            catch (XIncludeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                    $"Could not read text resource '{absolute}': {ex.Message}", ex);
            }
        }

        if (!AllowRemote)
        {
            throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: true,
                $"XInclude remote text resource blocked (AllowRemote is false): '{absolute}'.");
        }

        // AllowRemote http(s): fetch with content-negotiation headers, decode by
        // encoding→charset→BOM→UTF-8.
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, absolute);
            if (!string.IsNullOrEmpty(accept))
            {
                req.Headers.TryAddWithoutValidation("Accept", accept);
            }

            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                req.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
            }

            using var resp = RemoteTextClient.Send(req);
            resp.EnsureSuccessStatusCode();
            if (MaxResourceBytes > 0 && resp.Content.Headers.ContentLength is { } contentLength
                && contentLength > MaxResourceBytes)
            {
                throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                    $"resource '{absolute}' exceeds MaxResourceBytes ({MaxResourceBytes}).");
            }

            // Read the body with a hard cap: the Content-Length check above is advisory (a hostile
            // or chunked response may omit or lie about it), so bound the actual bytes read so a
            // no-length streaming response cannot pull unbounded data into memory.
            var bytes = ReadCapped(resp, absolute);
            if (enc != null)
            {
                return enc.GetString(StripBom(bytes, enc));
            }

            var charset = resp.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrEmpty(charset))
            {
                try
                {
                    var c = Encoding.GetEncoding(charset);
                    return c.GetString(StripBom(bytes, c));
                }
                catch (ArgumentException)
                {
                    // fall through to BOM/UTF-8
                }
            }

            return DecodeWithBomOrUtf8(bytes);
        }
        catch (XIncludeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                $"Could not fetch text resource '{absolute}': {ex.Message}", ex);
        }
    }

    // Reads the response body, capping the number of bytes at MaxResourceBytes when set (<= 0 =
    // unlimited). Reading one byte past the cap and finding data left = over the limit → a
    // fallback-eligible resource error, even when Content-Length is absent (chunked responses).
    private byte[] ReadCapped(HttpResponseMessage resp, Uri absolute)
    {
        if (MaxResourceBytes <= 0)
        {
            return resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }

        using var stream = resp.Content.ReadAsStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > MaxResourceBytes)
            {
                throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                    $"resource '{absolute}' exceeds MaxResourceBytes ({MaxResourceBytes}).");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static byte[] StripBom(byte[] bytes, Encoding enc)
    {
        var preamble = enc.GetPreamble();
        if (preamble.Length > 0 && bytes.Length >= preamble.Length)
        {
            for (var i = 0; i < preamble.Length; i++)
            {
                if (bytes[i] != preamble[i])
                {
                    return bytes;
                }
            }

            return bytes[preamble.Length..];
        }

        return bytes;
    }

    private static string DecodeWithBomOrUtf8(byte[] bytes)
    {
        // BOM detection (UTF-8, UTF-32, UTF-16); default UTF-8 (no BOM). UTF-32 is tested
        // BEFORE UTF-16 because a UTF-32LE BOM (FF FE 00 00) starts with the UTF-16LE BOM
        // bytes (FF FE) and would otherwise be mis-detected as UTF-16LE.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetString(bytes, 4, bytes.Length - 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetString(bytes, 4, bytes.Length - 4);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
