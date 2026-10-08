using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// Makes a schema document written for XSD 1.1 loadable by the XSD 1.0 processor the layer uses
/// (System.Xml's), in the two ways the specification and practice allow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conditional inclusion</b> (XSD 1.1 §4.2.2, the <c>vc:</c> versioning namespace). An element
/// whose <c>vc:minVersion</c> is above 1.0, or whose <c>vc:maxVersion</c> is 1.0 or below, is not
/// part of the schema for a 1.0 processor and is removed. Schemas written for both versions mark
/// their 1.1-only parts this way, typically <c>&lt;xs:assert test="…" vc:minVersion="1.1"/&gt;</c>;
/// loaded unprocessed the whole schema is lost for a construct it says a 1.0 processor must skip.
/// </para>
/// <para>
/// <b>Built-in types XSD 1.1 added</b> (<c>xs:dayTimeDuration</c>, <c>xs:yearMonthDuration</c>,
/// <c>xs:dateTimeStamp</c>). A reference to one is rewritten to the 1.0 type it restricts, so the
/// schema loads and a value still validates lexically against the base type. The 1.1 type's own
/// constraint is not applied: an <c>xs:dayTimeDuration</c> with a year component is accepted.
/// </para>
/// <para>
/// A document that is rewritten is serialized again, so the line and column of a later diagnostic
/// about it can differ from the original where an element was removed.
/// </para>
/// </remarks>
internal static class Xsd11Compatibility
{
    private const string VcNamespace = "http://www.w3.org/2007/XMLSchema-versioning";
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";
    private const decimal ProcessorVersion = 1.0m;

    private static readonly Dictionary<string, string> Xsd11BuiltIns = new(StringComparer.Ordinal)
    {
        ["dayTimeDuration"] = "duration",
        ["yearMonthDuration"] = "duration",
        ["dateTimeStamp"] = "dateTime",
    };

    private static readonly byte[][] Markers =
        [.. new[] { VcNamespace }.Concat(Xsd11BuiltIns.Keys).Select(System.Text.Encoding.ASCII.GetBytes)];

    /// <summary>
    /// The document as a 1.0 processor is to see it, or the same array when nothing in it needs
    /// rewriting. A document that is not well-formed is returned as it is, for the schema parser
    /// to report.
    /// </summary>
    /// <param name="document">A schema document already checked for a document type declaration and for depth.</param>
    public static byte[] Apply(byte[] document)
    {
        if (!MayNeedRewriting(document))
            return document;
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(document, writable: false), settings);
            doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return document;
        }

        var changed = false;
        foreach (var element in doc.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.Namespace != XNamespace.None
                    || attribute.Name.LocalName is not ("type" or "base" or "itemType" or "memberTypes"))
                    continue;
                var mapped = string.Join(' ', attribute.Value
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => MapBuiltIn(name, element)));
                if (mapped != attribute.Value)
                {
                    attribute.Value = mapped;
                    changed = true;
                }
            }
        }

        var minName = XName.Get("minVersion", VcNamespace);
        var maxName = XName.Get("maxVersion", VcNamespace);
        foreach (var element in doc.Descendants().ToList())
        {
            if (element.Parent is null && element != doc.Root)
                continue; // already removed with an ancestor
            if (element == doc.Root)
                continue; // a schema that excludes itself is the schema parser's to report
            if (Version(element.Attribute(minName)) is { } min && min > ProcessorVersion
                || Version(element.Attribute(maxName)) is { } max && max <= ProcessorVersion)
            {
                element.Remove();
                changed = true;
            }
        }
        if (!changed)
            return document;

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) }))
            doc.Save(writer);
        return output.ToArray();
    }

    /// <summary>
    /// Whether the bytes can hold anything to rewrite. The markers are ASCII, so they are found
    /// in any ASCII-compatible encoding; a document in another encoding (UTF-16, UTF-32) is
    /// always parsed.
    /// </summary>
    private static bool MayNeedRewriting(byte[] document)
    {
        if (document.Length >= 2 && (document[0] == 0 || document[1] == 0 || (document[0] == 0xFF && document[1] == 0xFE)
            || (document[0] == 0xFE && document[1] == 0xFF)))
            return true;
        var span = document.AsSpan();
        foreach (var marker in Markers)
            if (span.IndexOf(marker) >= 0)
                return true;
        return false;
    }

    private static string MapBuiltIn(string qname, XElement scope)
    {
        var colon = qname.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon < 0 ? "" : qname[..colon];
        var local = colon < 0 ? qname : qname[(colon + 1)..];
        var ns = colon < 0 ? scope.GetDefaultNamespace() : scope.GetNamespaceOfPrefix(prefix);
        return ns?.NamespaceName == XsdNamespace && Xsd11BuiltIns.TryGetValue(local, out var baseType)
            ? (colon < 0 ? baseType : prefix + ":" + baseType)
            : qname;
    }

    private static decimal? Version(XAttribute? attribute)
        => attribute is not null && decimal.TryParse(attribute.Value.Trim(), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
}
