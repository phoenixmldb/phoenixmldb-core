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

    /// <summary>
    /// Elements only XSD 1.1 has. A schema that uses one without a <c>vc:</c> guard cannot be
    /// read as XSD 1.0: there is no 1.0 schema to fall back to.
    /// </summary>
    private static readonly HashSet<string> Xsd11Elements = new(StringComparer.Ordinal)
    {
        "assert", "assertion", "alternative", "openContent", "defaultOpenContent", "override",
    };

    private static readonly byte[][] Markers =
        [.. new[] { VcNamespace }.Concat(Xsd11BuiltIns.Keys).Concat(Xsd11Elements).Select(System.Text.Encoding.ASCII.GetBytes)];

    /// <summary>
    /// The document as a 1.0 processor is to see it, or the same array when nothing in it needs
    /// rewriting. A document that is not well-formed is returned as it is, for the schema parser
    /// to report.
    /// </summary>
    /// <param name="document">A schema document already checked for a document type declaration and for depth.</param>
    /// <param name="requiresXsd11">
    /// Why the document cannot be a schema for a 1.0 processor at all, or null: its
    /// <c>xs:schema</c> element excludes itself from version 1.0, or it uses an element only
    /// XSD 1.1 has outside any <c>vc:</c> guard. Such a document is not to be given to the
    /// schema parser, whose message would not say this.
    /// </param>
    public static byte[] Apply(byte[] document, out string? requiresXsd11)
    {
        requiresXsd11 = null;
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
            if (Version(element.Attribute(minName)) is { } min && min > ProcessorVersion
                || Version(element.Attribute(maxName)) is { } max && max <= ProcessorVersion)
            {
                if (element == doc.Root)
                {
                    // Nothing of the document is a schema for version 1.0.
                    if (element.Name == XName.Get("schema", XsdNamespace))
                        requiresXsd11 = element.Attribute(minName) is { } rootMin && Version(rootMin) > ProcessorVersion
                            ? $"its xs:schema element has vc:minVersion=\"{rootMin.Value.Trim()}\""
                            : $"its xs:schema element has vc:maxVersion=\"{element.Attribute(maxName)!.Value.Trim()}\"";
                    return document;
                }
                element.Remove();
                changed = true;
            }
        }

        // What is left is what a 1.0 processor is to read. An element only 1.1 has, left in it,
        // was written without a guard.
        var unguarded = doc.Descendants()
            .Where(e => e.Name.Namespace == XsdNamespace && Xsd11Elements.Contains(e.Name.LocalName))
            .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"xs:{g.Key} ({g.Count().ToString(CultureInfo.InvariantCulture)})")
            .ToList();
        if (unguarded.Count > 0)
        {
            requiresXsd11 = "it uses " + string.Join(", ", unguarded) + " with no vc:minVersion guard";
            return document;
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
