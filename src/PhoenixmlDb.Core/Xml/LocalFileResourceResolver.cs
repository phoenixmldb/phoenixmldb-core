using System;
using System.IO;
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

        // Uri.IsFile is true for BOTH local paths (file:///c:/x.xml) and UNC paths
        // (file://attacker-host/share/x.xml, IsUnc == true, LocalPath == \\attacker-host\
        // share\x.xml). A UNC file: URI reaches a remote host over SMB and must go through
        // the same AllowRemote gate as http/https — otherwise it's an SSRF bypass. Only a
        // file: URI with no host (or "localhost") is genuinely local.
        var isLocalFile = absolute.IsFile
            && !absolute.IsUnc
            && (string.IsNullOrEmpty(absolute.Host) || string.Equals(absolute.Host, "localhost", StringComparison.OrdinalIgnoreCase));

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

        return XmlReader.Create(absolute.AbsoluteUri, remoteSettings);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown — <c>parse="text"</c> support ships in XInclude SP2.
    /// </exception>
    public string ResolveText(Uri absolute, string? encoding)
    {
        throw new NotSupportedException("parse=text (SP2)");
    }
}
