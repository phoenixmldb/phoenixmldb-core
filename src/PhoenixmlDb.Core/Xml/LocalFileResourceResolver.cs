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
        // (IgnoreWhitespace = false) so included content round-trips faithfully.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
            CloseInput = true,
        };

        if (absolute.IsFile)
        {
            var stream = File.OpenRead(absolute.LocalPath);
            return XmlReader.Create(stream, settings);
        }

        if (!AllowRemote)
        {
            throw new XIncludeException(
                isFatal: true,
                $"XInclude remote resource blocked (AllowRemote is false): '{absolute}'.");
        }

        return XmlReader.Create(absolute.AbsoluteUri, settings);
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
