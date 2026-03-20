using System;
using System.IO;
using System.Text;
using System.Xml;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Parsing;

/// <summary>
/// Serializes XDM node trees back to XML text, completing the round-trip from
/// <see cref="XmlDocumentParser"/> (parse) to in-memory XDM to XML output.
/// </summary>
/// <remarks>
/// <para>
/// <b>Round-trip fidelity:</b> The serializer preserves namespace prefixes, attribute order,
/// and namespace declarations from the original parse. Combined with <see cref="XmlDocumentParser"/>,
/// this enables a parse-modify-serialize workflow where the output closely matches the input
/// (modulo XML declaration differences and whitespace normalization).
/// </para>
/// <para>
/// <b>Namespace handling:</b> Namespace URIs are stored as interned <see cref="NamespaceId"/>
/// values in XDM nodes. The serializer uses the provided <c>namespaceResolver</c> function to
/// convert them back to URI strings, and writes namespace declarations based on each element's
/// <see cref="XdmElement.NamespaceDeclarations"/>.
/// </para>
/// <para>
/// <b>Node resolution:</b> Since XDM nodes reference their children by <see cref="NodeId"/>,
/// the serializer requires a <c>nodeResolver</c> function to look up child nodes. This is
/// typically provided by the storage layer.
/// </para>
/// </remarks>
/// <example>
/// Serializing a parsed document back to XML:
/// <code>
/// var serializer = new XmlSerializer(
///     nodeResolver: id => nodeTable[id],
///     namespaceResolver: nsId => namespaceTable.Resolve(nsId),
///     indent: true);
///
/// string xml = serializer.Serialize(parseResult.Document);
/// </code>
/// </example>
public sealed class XmlSerializer
{
    private readonly Func<NodeId, XdmNode?> _nodeResolver;
    private readonly Func<NamespaceId, string?> _namespaceResolver;
    private readonly XmlWriterSettings _settings;

    /// <summary>
    /// Creates a new XML serializer with the specified resolution functions.
    /// </summary>
    /// <param name="nodeResolver">
    /// Function to resolve <see cref="NodeId"/> references to <see cref="XdmNode"/> instances.
    /// Returns <c>null</c> if the node is not found (which causes it to be silently skipped).
    /// </param>
    /// <param name="namespaceResolver">
    /// Function to resolve <see cref="NamespaceId"/> values back to namespace URI strings.
    /// Returns <c>null</c> if the namespace is not found (treated as no namespace).
    /// </param>
    /// <param name="indent">When <c>true</c>, the output XML is indented for readability.</param>
    public XmlSerializer(
        Func<NodeId, XdmNode?> nodeResolver,
        Func<NamespaceId, string?> namespaceResolver,
        bool indent = false)
    {
        _nodeResolver = nodeResolver;
        _namespaceResolver = namespaceResolver;
        _settings = new XmlWriterSettings
        {
            Indent = indent,
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8
        };
    }

    /// <summary>
    /// Serializes a complete <see cref="XdmDocument"/> to an XML string, including the
    /// XML declaration.
    /// </summary>
    /// <param name="document">The document node to serialize.</param>
    /// <returns>The XML string representation of the document.</returns>
    public string Serialize(XdmDocument document)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, _settings);

        writer.WriteStartDocument();

        foreach (var childId in document.Children)
        {
            var child = _nodeResolver(childId);
            if (child != null)
                SerializeNode(writer, child);
        }

        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    /// Serializes a single <see cref="XdmNode"/> (and its subtree) to an XML string fragment.
    /// </summary>
    /// <param name="node">The node to serialize. If this is an element, its entire subtree is included.</param>
    /// <returns>The XML string representation of the node.</returns>
    public string Serialize(XdmNode node)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, _settings);

        SerializeNode(writer, node);
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    /// Serializes a complete <see cref="XdmDocument"/> to a <see cref="TextWriter"/>,
    /// for streaming output without buffering the entire XML string in memory.
    /// </summary>
    /// <param name="document">The document node to serialize.</param>
    /// <param name="textWriter">The writer to receive the XML output.</param>
    public void Serialize(XdmDocument document, TextWriter textWriter)
    {
        using var writer = XmlWriter.Create(textWriter, _settings);

        writer.WriteStartDocument();

        foreach (var childId in document.Children)
        {
            var child = _nodeResolver(childId);
            if (child != null)
                SerializeNode(writer, child);
        }

        writer.WriteEndDocument();
        writer.Flush();
    }

    private void SerializeNode(XmlWriter writer, XdmNode node)
    {
        switch (node)
        {
            case XdmElement element:
                SerializeElement(writer, element);
                break;

            case XdmText text:
                writer.WriteString(text.Value);
                break;

            case XdmComment comment:
                writer.WriteComment(comment.Value);
                break;

            case XdmProcessingInstruction pi:
                writer.WriteProcessingInstruction(pi.Target, pi.Value);
                break;

            case XdmDocument document:
                foreach (var childId in document.Children)
                {
                    var child = _nodeResolver(childId);
                    if (child != null)
                        SerializeNode(writer, child);
                }
                break;
        }
    }

    private void SerializeElement(XmlWriter writer, XdmElement element)
    {
        var namespaceUri = _namespaceResolver(element.Namespace) ?? string.Empty;
        var prefix = element.Prefix;

        if (prefix != null)
            writer.WriteStartElement(prefix, element.LocalName, namespaceUri);
        else if (!string.IsNullOrEmpty(namespaceUri))
            writer.WriteStartElement(element.LocalName, namespaceUri);
        else
            writer.WriteStartElement(element.LocalName);

        // Write namespace declarations
        foreach (var nsDecl in element.NamespaceDeclarations)
        {
            var nsUri = _namespaceResolver(nsDecl.Namespace) ?? string.Empty;
            if (string.IsNullOrEmpty(nsDecl.Prefix))
                writer.WriteAttributeString("xmlns", nsUri);
            else
                writer.WriteAttributeString("xmlns", nsDecl.Prefix, null, nsUri);
        }

        // Write attributes
        foreach (var attrId in element.Attributes)
        {
            var attr = _nodeResolver(attrId) as XdmAttribute;
            if (attr != null)
            {
                var attrNsUri = _namespaceResolver(attr.Namespace) ?? string.Empty;

                if (attr.Prefix != null)
                    writer.WriteAttributeString(attr.Prefix, attr.LocalName, attrNsUri, attr.Value);
                else if (!string.IsNullOrEmpty(attrNsUri))
                    writer.WriteAttributeString(attr.LocalName, attrNsUri, attr.Value);
                else
                    writer.WriteAttributeString(attr.LocalName, attr.Value);
            }
        }

        // Write children
        foreach (var childId in element.Children)
        {
            var child = _nodeResolver(childId);
            if (child != null)
                SerializeNode(writer, child);
        }

        writer.WriteEndElement();
    }
}
