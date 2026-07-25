using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Resolves an XInclude <c>xpointer</c>/<c>fragid</c> value against a target
/// <see cref="XmlDocument"/>, per the W3C XPointer Framework: shorthand (barename),
/// <c>element()</c>, <c>xmlns()</c>, and <c>xpath1()</c> schemes. <c>xpath1()</c> uses
/// System.Xml's built-in XPath 1.0 engine. Returns the selected nodes in document order
/// (empty if nothing is selected); throws a fatal <see cref="XIncludeException"/> for a
/// grammar-invalid pointer or an invalid <c>xpath1()</c> expression.
/// </summary>
internal static class XPointerEvaluator
{
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    public static IReadOnlyList<XmlNode> Evaluate(XmlDocument target, string pointer)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pointer);

        // Shorthand: a bare NCName with no scheme part.
        if (IsShorthand(pointer))
        {
            return SelectByXmlId(target, pointer);
        }

        var nsmgr = new XmlNamespaceManager(target.NameTable);
        nsmgr.AddNamespace("xml", XmlNamespace);

        foreach (var (scheme, data) in ParseParts(pointer))
        {
            switch (scheme)
            {
                case "xmlns":
                    BindXmlns(nsmgr, data);
                    break;
                case "element":
                    var el = EvaluateElement(target, data, nsmgr);
                    if (el.Length > 0) return el;
                    break;
                case "xpath1":
                    var xp = EvaluateXPath1(target, data, nsmgr);
                    if (xp.Length > 0) return xp;
                    break;
                default:
                    // Unknown scheme part is ignored per the XPointer Framework; try the next.
                    break;
            }
        }

        return Array.Empty<XmlNode>();
    }

    // A shorthand is an NCName: a letter or '_' followed by NCName chars, no '(' anywhere.
    private static bool IsShorthand(string pointer)
    {
        if (pointer.Length == 0 || pointer.Contains('(', StringComparison.Ordinal)) return false;
        if (!(char.IsLetter(pointer[0]) || pointer[0] == '_')) return false;
        foreach (var c in pointer)
        {
            if (!(char.IsLetterOrDigit(c) || c is '_' or '-' or '.')) return false;
        }
        return true;
    }

    private static IReadOnlyList<XmlNode> SelectByXmlId(XmlDocument target, string id)
    {
        var nsmgr = new XmlNamespaceManager(target.NameTable);
        nsmgr.AddNamespace("xml", XmlNamespace);
        // id is an NCName (no quotes possible), so a single-quoted literal is safe.
        var matches = target.SelectNodes($"//*[@xml:id='{id}']", nsmgr);
        return ToList(matches);
    }

    // Parse "scheme(data) scheme(data) ..." honoring the Framework escaping in data:
    // '^(' , '^)' are literal parens; '^^' is a literal '^'; balanced unescaped parens are kept.
    private static List<(string Scheme, string Data)> ParseParts(string pointer)
    {
        var parts = new List<(string, string)>();
        int i = 0, n = pointer.Length;
        while (i < n)
        {
            // Skip inter-part whitespace.
            while (i < n && char.IsWhiteSpace(pointer[i])) i++;
            if (i >= n) break;

            // Scheme name: up to '('.
            int schemeStart = i;
            while (i < n && pointer[i] != '(') i++;
            if (i >= n) throw Malformed($"xpointer part is missing '(': '{pointer}'.");
            var scheme = pointer[schemeStart..i].Trim();
            if (scheme.Length == 0) throw Malformed($"xpointer part has empty scheme: '{pointer}'.");

            // Data: from after '(' to the matching ')', tracking nested balanced parens and
            // the '^(' '^)' '^^' escapes.
            i++; // consume '('
            var sb = new StringBuilder();
            int depth = 1;
            while (i < n)
            {
                var c = pointer[i];
                if (c == '^' && i + 1 < n && pointer[i + 1] is '(' or ')' or '^')
                {
                    sb.Append(pointer[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '(') { depth++; sb.Append(c); i++; continue; }
                if (c == ')')
                {
                    depth--;
                    if (depth == 0) { i++; break; }
                    sb.Append(c); i++; continue;
                }
                sb.Append(c); i++;
            }
            if (depth != 0) throw Malformed($"xpointer part has unbalanced parentheses: '{pointer}'.");
            parts.Add((scheme, sb.ToString()));
        }
        if (parts.Count == 0) throw Malformed($"xpointer is not a valid pointer: '{pointer}'.");
        return parts;
    }

    private static void BindXmlns(XmlNamespaceManager nsmgr, string data)
    {
        // data is "prefix=uri".
        int eq = data.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0) throw Malformed($"xmlns() part must be 'prefix=uri': '{data}'.");
        var prefix = data[..eq].Trim();
        var uri = data[(eq + 1)..].Trim();
        if (prefix.Length == 0) throw Malformed($"xmlns() part has empty prefix: '{data}'.");
        nsmgr.AddNamespace(prefix, uri);
    }

    // Stubs completed in Tasks 2-3.
    private static XmlNode[] EvaluateElement(XmlDocument target, string data, XmlNamespaceManager nsmgr)
        => Array.Empty<XmlNode>();

    private static XmlNode[] EvaluateXPath1(XmlDocument target, string data, XmlNamespaceManager nsmgr)
        => Array.Empty<XmlNode>();

    private static IReadOnlyList<XmlNode> ToList(XmlNodeList? nodes)
    {
        if (nodes is null || nodes.Count == 0) return Array.Empty<XmlNode>();
        var list = new List<XmlNode>(nodes.Count);
        foreach (XmlNode node in nodes) list.Add(node);
        return list;
    }

    private static XIncludeException Malformed(string message)
        => new(XIncludeErrorKind.MalformedInclude, isFatal: true, message);
}
