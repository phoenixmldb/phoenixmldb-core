using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Xml;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Parsing;

/// <summary>
/// Parses XML content into XDM nodes.
/// </summary>
public sealed class XmlDocumentParser
{
    private readonly Func<string, NamespaceId> _namespaceResolver;
    private readonly DocumentId _documentId;
    private ulong _nextNodeId;
    private readonly List<XdmNode> _nodes = new();
    private readonly bool _preserveWhitespace;

    /// <summary>
    /// Creates a new XML parser.
    /// </summary>
    /// <param name="documentId">The document ID for all nodes.</param>
    /// <param name="startNodeId">The starting node ID.</param>
    /// <param name="namespaceResolver">Function to resolve namespace URIs to IDs.</param>
    /// <param name="preserveWhitespace">Whether to preserve whitespace-only text nodes.</param>
    public XmlDocumentParser(
        DocumentId documentId,
        NodeId startNodeId,
        Func<string, NamespaceId> namespaceResolver,
        bool preserveWhitespace = false)
    {
        _documentId = documentId;
        _nextNodeId = startNodeId.Value;
        _namespaceResolver = namespaceResolver;
        _preserveWhitespace = preserveWhitespace;
    }

    /// <summary>
    /// Parses XML content from a string.
    /// </summary>
    public ParseResult Parse(string xml, string? documentUri = null)
    {
        using var reader = new StringReader(xml);
        return Parse(reader, documentUri);
    }

    /// <summary>
    /// Parses XML content from a TextReader.
    /// </summary>
    public ParseResult Parse(TextReader textReader, string? documentUri = null)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = !_preserveWhitespace,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            DtdProcessing = DtdProcessing.Ignore
        };

        using var reader = XmlReader.Create(textReader, settings);
        return ParseInternal(reader, documentUri);
    }

    /// <summary>
    /// Parses XML content from a stream.
    /// </summary>
    public ParseResult Parse(Stream stream, string? documentUri = null)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = !_preserveWhitespace,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            DtdProcessing = DtdProcessing.Ignore
        };

        using var reader = XmlReader.Create(stream, settings);
        return ParseInternal(reader, documentUri);
    }

    private ParseResult ParseInternal(XmlReader reader, string? documentUri)
    {
        var documentNodeId = AllocateNodeId();
        var documentChildren = new List<NodeId>();
        NodeId? documentElement = null;

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    var elementId = ParseElement(reader, null);
                    documentChildren.Add(elementId);
                    documentElement ??= elementId;
                    break;

                case XmlNodeType.Comment:
                    documentChildren.Add(ParseComment(reader, null));
                    break;

                case XmlNodeType.ProcessingInstruction:
                    documentChildren.Add(ParseProcessingInstruction(reader, null));
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    // Per XDM spec, document nodes can only have element, comment, and PI children.
                    // Text nodes at the document level are discarded.
                    break;
            }
        }

        var document = new XdmDocument
        {
            Id = documentNodeId,
            Document = _documentId,
            DocumentUri = documentUri,
            DocumentElement = documentElement,
            Children = documentChildren.ToImmutableArray()
        };

        // Insert document at the beginning
        _nodes.Insert(0, document);

        return new ParseResult
        {
            Document = document,
            Nodes = _nodes.ToImmutableArray(),
            NodeCount = (uint)_nodes.Count
        };
    }

    private NodeId ParseElement(XmlReader reader, NodeId? parentId)
    {
        var nodeId = AllocateNodeId();
        var namespaceUri = reader.NamespaceURI ?? string.Empty;
        var namespaceId = _namespaceResolver(namespaceUri);
        var localName = reader.LocalName;
        var prefix = string.IsNullOrEmpty(reader.Prefix) ? null : reader.Prefix;
        var isEmpty = reader.IsEmptyElement;

        // Collect attributes
        var attributes = new List<NodeId>();
        var namespaceDecls = new List<NamespaceBinding>();

        if (reader.HasAttributes)
        {
            reader.MoveToFirstAttribute();
            do
            {
                if (reader.Prefix == "xmlns" || (reader.Prefix == "" && reader.LocalName == "xmlns"))
                {
                    // This is a namespace declaration
                    var nsPrefix = reader.Prefix == "xmlns" ? reader.LocalName : "";
                    var nsUri = reader.Value;
                    var nsId = _namespaceResolver(nsUri);
                    namespaceDecls.Add(new NamespaceBinding(nsPrefix, nsId));
                }
                else
                {
                    // Regular attribute
                    var attrId = ParseAttribute(reader, nodeId);
                    attributes.Add(attrId);
                }
            } while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        // Collect children
        var children = new List<NodeId>();

        if (!isEmpty)
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        children.Add(ParseElement(reader, nodeId));
                        break;

                    case XmlNodeType.EndElement:
                        goto endElement;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        var text = reader.Value;
                        if (!string.IsNullOrEmpty(text))
                            children.Add(ParseText(reader, nodeId));
                        break;

                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        if (_preserveWhitespace)
                            children.Add(ParseText(reader, nodeId));
                        break;

                    case XmlNodeType.Comment:
                        children.Add(ParseComment(reader, nodeId));
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        children.Add(ParseProcessingInstruction(reader, nodeId));
                        break;
                }
            }
        }

        endElement:

        var element = new XdmElement
        {
            Id = nodeId,
            Document = _documentId,
            Namespace = namespaceId,
            LocalName = localName,
            Prefix = prefix,
            Parent = parentId,
            Attributes = attributes.ToImmutableArray(),
            NamespaceDeclarations = namespaceDecls.ToImmutableArray(),
            Children = children.ToImmutableArray()
        };

        _nodes.Add(element);
        return nodeId;
    }

    private NodeId ParseAttribute(XmlReader reader, NodeId parentId)
    {
        var nodeId = AllocateNodeId();
        var namespaceUri = reader.NamespaceURI ?? string.Empty;
        var namespaceId = _namespaceResolver(namespaceUri);
        var localName = reader.LocalName;
        var prefix = string.IsNullOrEmpty(reader.Prefix) ? null : reader.Prefix;
        var value = reader.Value;

        var attribute = new XdmAttribute
        {
            Id = nodeId,
            Document = _documentId,
            Namespace = namespaceId,
            LocalName = localName,
            Prefix = prefix,
            Parent = parentId,
            Value = value
        };

        _nodes.Add(attribute);
        return nodeId;
    }

    private NodeId ParseText(XmlReader reader, NodeId? parentId)
    {
        var nodeId = AllocateNodeId();
        var value = reader.Value;

        var text = new XdmText
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Value = value
        };

        _nodes.Add(text);
        return nodeId;
    }

    private NodeId ParseComment(XmlReader reader, NodeId? parentId)
    {
        var nodeId = AllocateNodeId();
        var value = reader.Value;

        var comment = new XdmComment
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Value = value
        };

        _nodes.Add(comment);
        return nodeId;
    }

    private NodeId ParseProcessingInstruction(XmlReader reader, NodeId? parentId)
    {
        var nodeId = AllocateNodeId();
        var target = reader.Name;
        var value = reader.Value;

        var pi = new XdmProcessingInstruction
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Target = target,
            Value = value
        };

        _nodes.Add(pi);
        return nodeId;
    }

    private NodeId AllocateNodeId()
    {
        return new NodeId(_nextNodeId++);
    }
}

/// <summary>
/// Result of parsing an XML document.
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// The document node.
    /// </summary>
    public required XdmDocument Document { get; init; }

    /// <summary>
    /// All nodes in document order.
    /// </summary>
    public required IReadOnlyList<XdmNode> Nodes { get; init; }

    /// <summary>
    /// Total number of nodes.
    /// </summary>
    public required uint NodeCount { get; init; }
}
