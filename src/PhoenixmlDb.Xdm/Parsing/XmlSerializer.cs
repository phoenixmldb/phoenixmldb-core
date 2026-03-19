using System;
using System.IO;
using System.Text;
using System.Xml;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Parsing;

/// <summary>
/// Serializes XDM nodes to XML.
/// </summary>
public sealed class XmlSerializer
{
    private readonly Func<NodeId, XdmNode?> _nodeResolver;
    private readonly Func<NamespaceId, string?> _namespaceResolver;
    private readonly XmlWriterSettings _settings;

    /// <summary>
    /// Creates a new XML serializer.
    /// </summary>
    /// <param name="nodeResolver">Function to resolve node IDs to nodes.</param>
    /// <param name="namespaceResolver">Function to resolve namespace IDs to URIs.</param>
    /// <param name="indent">Whether to indent the output.</param>
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
    /// Serializes a document to XML.
    /// </summary>
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
    /// Serializes a node to XML.
    /// </summary>
    public string Serialize(XdmNode node)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, _settings);

        SerializeNode(writer, node);
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    /// Serializes to a TextWriter.
    /// </summary>
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
