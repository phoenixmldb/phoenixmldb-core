using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Xml;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Parsing;

/// <summary>
/// Parses XML content (strings, streams, or readers) into an XDM node tree.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary mechanism for converting XML text into the in-memory XDM
/// representation used throughout PhoenixmlDb. The parser produces a <see cref="ParseResult"/>
/// containing the <see cref="XdmDocument"/> root node and a flat list of all nodes in
/// document order.
/// </para>
/// <para>
/// <b>Node ID assignment:</b> Each node receives a unique <see cref="NodeId"/> starting from
/// <c>startNodeId</c> and incrementing monotonically. The document node receives the first ID,
/// followed by elements, attributes, text nodes, etc., in document order. This sequential
/// assignment enables efficient storage and retrieval.
/// </para>
/// <para>
/// <b>Namespace interning:</b> Namespace URIs are converted to <see cref="NamespaceId"/> values
/// via the provided <c>namespaceResolver</c> function. This deduplicates namespace strings
/// across the entire database, making namespace comparisons an integer operation.
/// </para>
/// <para>
/// <b>Usage:</b> This parser is used internally by <c>PutDocumentAsync</c> when storing
/// documents, but is also available for direct use when you need to parse XML without storing it.
/// </para>
/// </remarks>
/// <example>
/// Parsing an XML string into an XDM tree:
/// <code>
/// var parser = new XmlDocumentParser(
///     documentId: new DocumentId(1),
///     startNodeId: new NodeId(1),
///     namespaceResolver: uri => namespaceTable.Intern(uri));
///
/// var result = parser.Parse("&lt;root&gt;&lt;item&gt;Hello&lt;/item&gt;&lt;/root&gt;");
/// var doc = result.Document;
/// Console.WriteLine($"Parsed {result.NodeCount} nodes");
/// </code>
/// </example>
public sealed class XmlDocumentParser
{
    private readonly Func<string, NamespaceId> _namespaceResolver;
    private readonly DocumentId _documentId;
    private ulong _nextNodeId;
    private readonly List<XdmNode> _nodes = new();
    // Id -> node index maintained alongside _nodes so child/parent lookups are O(1).
    // Without this, resolving each child by id is a linear scan of _nodes, making the
    // string-value computation of a large element O(N^2).
    private readonly Dictionary<NodeId, XdmNode> _nodesById = new();
    private readonly bool _preserveWhitespace;

    /// <summary>
    /// Appends a node to the flat node list and the id index in lockstep.
    /// All node creation must route through here so <see cref="_nodesById"/> stays in sync.
    /// </summary>
    private void AddNode(XdmNode node)
    {
        _nodes.Add(node);
        _nodesById[node.Id] = node;
    }

    /// <summary>
    /// Cached reflection accessors for the internal <c>SchemaType</c> property on
    /// validating readers. This property returns an <see cref="System.Xml.Schema.XmlSchemaDatatype"/>
    /// for DTD-validated attributes, exposing the <see cref="System.Xml.Schema.XmlSchemaDatatype.TokenizedType"/>
    /// needed to identify ID/IDREF/IDREFS attributes.
    ///
    /// We cache per-reader-type because different validation modes use different internal
    /// reader types (<c>XmlValidatingReaderImpl</c> for DTD, <c>XsdValidatingReader</c> for XSD).
    /// Using a single cached PropertyInfo across both fails at runtime when types mismatch.
    /// </summary>
    /// <remarks>
    /// The public <c>XmlReader.SchemaInfo</c> property returns null for DTD validation —
    /// DTD type info is only available through this internal property. This is a well-known
    /// limitation of the .NET XML API.
    /// </remarks>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo?> _readerSchemaTypeProperties = new();

    /// <summary>
    /// Creates a new XML parser that will assign the specified document and node IDs.
    /// </summary>
    /// <param name="documentId">The <see cref="DocumentId"/> to assign to all parsed nodes.</param>
    /// <param name="startNodeId">The first <see cref="NodeId"/> to assign. Subsequent nodes receive incrementing IDs.</param>
    /// <param name="namespaceResolver">
    /// Function that converts namespace URI strings to interned <see cref="NamespaceId"/> values.
    /// This is typically backed by a database-wide namespace table.
    /// </param>
    /// <param name="preserveWhitespace">
    /// When <c>true</c>, whitespace-only text nodes are preserved. When <c>false</c> (default),
    /// they are discarded, matching the behavior of <c>strip-space</c> in XSLT.
    /// </param>
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
    /// Parses XML content from a string into an XDM document tree.
    /// </summary>
    /// <param name="xml">The XML content to parse. Must be well-formed XML.</param>
    /// <param name="documentUri">Optional document URI to assign to the resulting <see cref="XdmDocument"/>.</param>
    /// <returns>A <see cref="ParseResult"/> containing the document and all parsed nodes.</returns>
    /// <exception cref="System.Xml.XmlException">The XML content is not well-formed.</exception>
    public ParseResult Parse(string xml, string? documentUri = null)
    {
        using var reader = new StringReader(xml);
        return Parse(reader, documentUri);
    }

    /// <summary>
    /// Parses XML content from a <see cref="TextReader"/> into an XDM document tree.
    /// </summary>
    /// <param name="textReader">A reader positioned at the start of the XML content.</param>
    /// <param name="documentUri">Optional document URI to assign to the resulting <see cref="XdmDocument"/>.</param>
    /// <returns>A <see cref="ParseResult"/> containing the document and all parsed nodes.</returns>
    /// <exception cref="System.Xml.XmlException">The XML content is not well-formed.</exception>
    public ParseResult Parse(TextReader textReader, string? documentUri = null)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = !_preserveWhitespace,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            DtdProcessing = DtdProcessing.Parse,
            // Cap internal-entity expansion (billion-laughs DoS on untrusted input). Matches the
            // sibling parsers (StylesheetParser, XmlConversionFunctions); omitted here originally.
            MaxCharactersFromEntities = 1_000_000,
            ValidationType = ValidationType.DTD
        };
        // Suppress validation event exceptions — we want DTD type info but not validation failures
        settings.ValidationEventHandler += static (_, _) => { };

        using var reader = XmlReader.Create(textReader, settings);
        return ParseInternal(reader, documentUri);
    }

    /// <summary>
    /// Parses XML content with XSD schema validation, populating type annotations
    /// (including <c>xs:ID</c> / <c>xs:IDREF</c>) from the schema.
    /// </summary>
    /// <param name="textReader">A reader positioned at the start of the XML content.</param>
    /// <param name="documentUri">Optional document URI to assign to the resulting <see cref="XdmDocument"/>.</param>
    /// <param name="schemas">The XSD schema set to validate against.</param>
    /// <returns>A <see cref="ParseResult"/> containing the document and all parsed nodes.</returns>
    public ParseResult Parse(TextReader textReader, string? documentUri, System.Xml.Schema.XmlSchemaSet schemas)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = !_preserveWhitespace,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            DtdProcessing = DtdProcessing.Parse,
            // Cap internal-entity expansion (billion-laughs DoS on untrusted input). Matches the
            // sibling parsers (StylesheetParser, XmlConversionFunctions); omitted here originally.
            MaxCharactersFromEntities = 1_000_000,
            ValidationType = ValidationType.Schema
        };
        settings.Schemas = schemas;
        // Suppress validation event exceptions — we want schema type info but not validation failures
        settings.ValidationEventHandler += static (_, _) => { };

        using var reader = XmlReader.Create(textReader, settings);
        return ParseInternal(reader, documentUri);
    }

    /// <summary>
    /// Parses XML content from a <see cref="Stream"/> into an XDM document tree.
    /// </summary>
    /// <param name="stream">A stream containing the XML content. The stream's encoding is auto-detected.</param>
    /// <param name="documentUri">Optional document URI to assign to the resulting <see cref="XdmDocument"/>.</param>
    /// <returns>A <see cref="ParseResult"/> containing the document and all parsed nodes.</returns>
    /// <exception cref="System.Xml.XmlException">The XML content is not well-formed.</exception>
    public ParseResult Parse(Stream stream, string? documentUri = null)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = !_preserveWhitespace,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            DtdProcessing = DtdProcessing.Parse,
            // Cap internal-entity expansion (billion-laughs DoS on untrusted input). Matches the
            // sibling parsers (StylesheetParser, XmlConversionFunctions); omitted here originally.
            MaxCharactersFromEntities = 1_000_000,
            ValidationType = ValidationType.DTD
        };
        // Suppress validation event exceptions — we want DTD type info but not validation failures
        settings.ValidationEventHandler += static (_, _) => { };

        using var reader = XmlReader.Create(stream, settings);
        return ParseInternal(reader, documentUri);
    }

    private ParseResult ParseInternal(XmlReader reader, string? documentUri)
    {
        var lineInfo = reader as System.Xml.IXmlLineInfo;
        var documentNodeId = AllocateNodeId();
        var documentChildren = new List<NodeId>();
        NodeId? documentElement = null;

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    var elementId = ParseElement(reader, null, lineInfo);
                    documentChildren.Add(elementId);
                    documentElement ??= elementId;
                    break;

                case XmlNodeType.Comment:
                    documentChildren.Add(ParseComment(reader, null, lineInfo));
                    break;

                case XmlNodeType.ProcessingInstruction:
                    documentChildren.Add(ParseProcessingInstruction(reader, null, lineInfo));
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
        document._stringValue = ComputeStringValue(documentChildren);

        // Set the parent of top-level children to the document node.
        // This enables base-uri() inheritance: child → ... → document → DocumentUri.
        foreach (var childId in documentChildren)
        {
            var child = _nodesById.GetValueOrDefault(childId);
            if (child != null)
                child.Parent = documentNodeId;
        }

        // Insert document at the beginning
        _nodes.Insert(0, document);
        _nodesById[document.Id] = document;

        return new ParseResult
        {
            Document = document,
            Nodes = _nodes.ToImmutableArray(),
            NodeCount = (uint)_nodes.Count
        };
    }

    private NodeId ParseElement(XmlReader reader, NodeId? parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        // One frame per nesting level: a deeply nested (untrusted) document would overflow the
        // native stack — an uncatchable StackOverflowException that crashes the host. Throw a
        // catchable InsufficientExecutionStackException before the stack is exhausted instead.
        System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();

        // Capture position before MoveToFirstAttribute shifts the reader cursor.
        var sourceLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        var sourceCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;

        var nodeId = AllocateNodeId();
        var namespaceUri = reader.NamespaceURI ?? string.Empty;
        var namespaceId = _namespaceResolver(namespaceUri);
        var localName = reader.LocalName;
        var prefix = string.IsNullOrEmpty(reader.Prefix) ? null : reader.Prefix;
        var isEmpty = reader.IsEmptyElement;

        // Detect xs:ID-typed simple-content elements. An element's content is ID-typed when:
        // - the schema type (or its simple-type datatype) has XmlTypeCode.Id (xs:ID or derived), OR
        // - the element has a union type and the selected MemberType is xs:ID (e.g., xs:ID | xs:integer)
        // For types derived from xs:ID by restriction, Datatype.TypeCode still reports Id.
        var isIdContent = false;
        // Captured for the XdmElement.TypeAnnotation projection below.
        // Schema-validating readers populate SchemaInfo.SchemaType only when a
        // global element declaration matched (strict/lax). For union types, the
        // MemberType is the selected branch — that's what XQuery's data-model
        // expects on the element node (XDM §5.10.7).
        System.Xml.XmlQualifiedName? elementSchemaTypeQName = null;
        if (reader.SchemaInfo is System.Xml.Schema.IXmlSchemaInfo elemSchemaInfo)
        {
            var schemaType = elemSchemaInfo.SchemaType;
            if (schemaType != null)
            {
                if (schemaType.TypeCode == System.Xml.Schema.XmlTypeCode.Id)
                    isIdContent = true;
                else if (schemaType.Datatype?.TypeCode == System.Xml.Schema.XmlTypeCode.Id)
                    isIdContent = true;
            }
            // Check union member type — for xs:ID-in-a-union, MemberType is the selected member
            if (!isIdContent && elemSchemaInfo.MemberType != null)
            {
                var memberType = elemSchemaInfo.MemberType;
                if (memberType.TypeCode == System.Xml.Schema.XmlTypeCode.Id ||
                    memberType.Datatype?.TypeCode == System.Xml.Schema.XmlTypeCode.Id)
                    isIdContent = true;
            }
            // Prefer MemberType for unions so the annotation reflects the actually-matched branch.
            elementSchemaTypeQName = elemSchemaInfo.MemberType?.QualifiedName
                ?? elemSchemaInfo.SchemaType?.QualifiedName;
        }

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
                    var attrId = ParseAttribute(reader, nodeId, lineInfo);
                    attributes.Add(attrId);
                }
            } while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        // Collect children.
        //
        // XmlReader raises a SEPARATE character-data event at every CDATA and entity
        // boundary (e.g. "abc<![CDATA[def]]>ghi" is three events), but XDM forbids two
        // consecutive text-node siblings — a text node's value must be a complete run of
        // character data. So we coalesce a run of consecutive character-data events
        // (Text, CDATA, and, when preserving, Whitespace/SignificantWhitespace) into a
        // SINGLE XdmText node. A run is broken only by an element, comment, or PI (or the
        // end tag) — those are the node kinds that legitimately separate text nodes.
        var children = new List<NodeId>();

        if (!isEmpty)
        {
            // Pending coalesced character-data run for the current sibling position.
            // The source line/column are captured from the FIRST event contributing to the run.
            System.Text.StringBuilder? runBuilder = null;
            var runLine = 0;
            var runCol = 0;

            void AppendRun(string value)
            {
                if (value.Length == 0)
                    return;
                if (runBuilder is null)
                {
                    runBuilder = new System.Text.StringBuilder(value.Length);
                    runLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
                    runCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;
                }
                runBuilder.Append(value);
            }

            void FlushRun()
            {
                if (runBuilder is { Length: > 0 })
                    children.Add(CreateTextNode(runBuilder.ToString(), nodeId, runLine, runCol));
                runBuilder = null;
            }

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    FlushRun();
                    break;
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        FlushRun();
                        children.Add(ParseElement(reader, nodeId, lineInfo));
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        // Text/CDATA are always significant character data (CDATA is a
                        // serialization detail, not a distinct node kind in XDM) — accumulate.
                        AppendRun(reader.Value);
                        break;

                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        // Whitespace-only spans join the run only when preserving. When stripping
                        // (_preserveWhitespace == false) they are dropped AND transparent to the
                        // run: they neither contribute text nor break an adjacent Text/CDATA run.
                        // This keeps the pre-coalescing stripping behaviour while still honouring
                        // the XDM "no adjacent text nodes" invariant if a strip creates adjacency.
                        if (_preserveWhitespace)
                            AppendRun(reader.Value);
                        break;

                    case XmlNodeType.Comment:
                        FlushRun();
                        children.Add(ParseComment(reader, nodeId, lineInfo));
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        FlushRun();
                        children.Add(ParseProcessingInstruction(reader, nodeId, lineInfo));
                        break;
                }
            }

            // Defensive flush: a well-formed document always ends the loop at EndElement (handled
            // above), but guard against a trailing run being lost if the reader stops otherwise.
            FlushRun();
        }

        var elementTypeAnnotation = ResolveSchemaTypeAnnotation(elementSchemaTypeQName, XdmTypeName.Untyped);

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
            Children = children.ToImmutableArray(),
            IsIdContent = isIdContent,
            TypeAnnotation = elementTypeAnnotation,
            SourceLine = sourceLine,
            SourceColumn = sourceCol
        };

        // Compute string value from descendant text nodes (XDM §5.7.2).
        // This must be done at parse time because the shredded storage model
        // doesn't give elements access to a node provider at runtime.
        element._stringValue = ComputeStringValue(children);

        AddNode(element);
        return nodeId;
    }

    /// <summary>
    /// Computes the string value of an element from its children (text + nested elements).
    /// Walks _nodes to resolve child NodeIds.
    /// </summary>
    /// <summary>
    /// Maps an <see cref="System.Xml.XmlQualifiedName"/> from the XmlReader's SchemaInfo
    /// to an <see cref="XdmTypeName"/> suitable for <see cref="XdmElement.TypeAnnotation"/>
    /// or <see cref="XdmAttribute.TypeAnnotation"/>. Returns <paramref name="fallback"/>
    /// when no schema type was reported, when the type is anonymous (no QualifiedName),
    /// or when the qualified name has no local part.
    /// </summary>
    private XdmTypeName ResolveSchemaTypeAnnotation(System.Xml.XmlQualifiedName? schemaTypeQName, XdmTypeName fallback)
    {
        if (schemaTypeQName == null || string.IsNullOrEmpty(schemaTypeQName.Name))
            return fallback;
        var nsUri = schemaTypeQName.Namespace ?? "";
        // xsd built-ins use the well-known NamespaceId.Xsd interning slot so that
        // downstream type-matching (e.g. instance-of, atomization) sees a stable id.
        var nsId = nsUri == "http://www.w3.org/2001/XMLSchema"
            ? NamespaceId.Xsd
            : _namespaceResolver(nsUri);
        return new XdmTypeName(nsId, schemaTypeQName.Name);
    }

    private string ComputeStringValue(List<NodeId> children)
    {
        if (children.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        foreach (var childId in children)
        {
            var child = _nodesById.GetValueOrDefault(childId);
            if (child is XdmText text)
                sb.Append(text.Value);
            else if (child is XdmElement childElem)
                sb.Append(childElem.StringValue); // Already computed (bottom-up)
        }
        return sb.ToString();
    }

    private NodeId ParseAttribute(XmlReader reader, NodeId parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        // Position is already on this attribute (after MoveToFirstAttribute / MoveToNextAttribute).
        var sourceLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        var sourceCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;

        var nodeId = AllocateNodeId();
        var namespaceUri = reader.NamespaceURI ?? string.Empty;
        var namespaceId = _namespaceResolver(namespaceUri);
        var localName = reader.LocalName;
        var prefix = string.IsNullOrEmpty(reader.Prefix) ? null : reader.Prefix;
        var value = reader.Value;

        // xml:id attributes are always ID attributes per the xml:id specification.
        var isId = localName == "id" &&
                   namespaceUri == "http://www.w3.org/XML/1998/namespace";

        var isIdRef = false;

        // Check DTD/Schema type information for ID/IDREF declarations.
        // For XSD validation, SchemaInfo.SchemaType is populated.
        if (reader.SchemaInfo is System.Xml.Schema.IXmlSchemaInfo schemaInfo &&
            schemaInfo.SchemaType != null)
        {
            if (!isId && schemaInfo.SchemaType.TypeCode == System.Xml.Schema.XmlTypeCode.Id)
                isId = true;

            if (schemaInfo.SchemaType.TypeCode == System.Xml.Schema.XmlTypeCode.Idref ||
                schemaInfo.SchemaType.QualifiedName?.Name == "IDREFS")
                isIdRef = true;
        }
        else
        {
            // For DTD validation, the public SchemaInfo property returns null.
            // The DTD type info is only available through the internal SchemaType property
            // on XmlValidatingReaderImpl, which returns an XmlSchemaDatatype with TokenizedType.
            // Cache per-reader-type because DTD and XSD validation use different internal types.
            var readerType = reader.GetType();
            var schemaTypeProp = _readerSchemaTypeProperties.GetOrAdd(readerType, static t =>
                t.GetProperty("SchemaType",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic));

            if (schemaTypeProp?.GetValue(reader) is System.Xml.Schema.XmlSchemaDatatype datatype)
            {
                if (!isId && datatype.TokenizedType == System.Xml.XmlTokenizedType.ID)
                    isId = true;

                if (datatype.TokenizedType == System.Xml.XmlTokenizedType.IDREF ||
                    datatype.TokenizedType == System.Xml.XmlTokenizedType.IDREFS)
                    isIdRef = true;
            }
        }

        // Pull the attribute's schema type for TypeAnnotation. XSD-validated attributes
        // expose their typed value through SchemaInfo; treat the union MemberType as the
        // chosen branch so the annotation matches the value actually consumed by atomization.
        System.Xml.XmlQualifiedName? attrSchemaTypeQName = null;
        if (reader.SchemaInfo is System.Xml.Schema.IXmlSchemaInfo attrSchemaInfo)
        {
            attrSchemaTypeQName = attrSchemaInfo.MemberType?.QualifiedName
                ?? attrSchemaInfo.SchemaType?.QualifiedName;
        }
        var attrTypeAnnotation = ResolveSchemaTypeAnnotation(attrSchemaTypeQName, XdmTypeName.UntypedAtomic);

        var attribute = new XdmAttribute
        {
            Id = nodeId,
            Document = _documentId,
            Namespace = namespaceId,
            LocalName = localName,
            Prefix = prefix,
            Parent = parentId,
            Value = value,
            IsId = isId,
            IsIdRef = isIdRef,
            TypeAnnotation = attrTypeAnnotation,
            SourceLine = sourceLine,
            SourceColumn = sourceCol
        };

        AddNode(attribute);
        return nodeId;
    }

    /// <summary>
    /// Creates a single text node from an already-coalesced run of character data.
    /// The caller supplies the concatenated <paramref name="value"/> and the source
    /// position of the FIRST character-data event in the run (see the coalescing loop
    /// in <see cref="ParseElement"/>). The value is always non-empty by construction.
    /// </summary>
    private NodeId CreateTextNode(string value, NodeId? parentId, int sourceLine, int sourceCol)
    {
        var nodeId = AllocateNodeId();

        var text = new XdmText
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Value = value,
            SourceLine = sourceLine,
            SourceColumn = sourceCol
        };

        AddNode(text);
        return nodeId;
    }

    private NodeId ParseComment(XmlReader reader, NodeId? parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        var sourceLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        var sourceCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;

        var nodeId = AllocateNodeId();
        var value = reader.Value;

        var comment = new XdmComment
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Value = value,
            SourceLine = sourceLine,
            SourceColumn = sourceCol
        };

        AddNode(comment);
        return nodeId;
    }

    private NodeId ParseProcessingInstruction(XmlReader reader, NodeId? parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        var sourceLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        var sourceCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;

        var nodeId = AllocateNodeId();
        var target = reader.Name;
        var value = reader.Value;

        var pi = new XdmProcessingInstruction
        {
            Id = nodeId,
            Document = _documentId,
            Parent = parentId,
            Target = target,
            Value = value,
            SourceLine = sourceLine,
            SourceColumn = sourceCol
        };

        AddNode(pi);
        return nodeId;
    }

    private NodeId AllocateNodeId()
    {
        return new NodeId(_nextNodeId++);
    }
}

/// <summary>
/// The result of parsing an XML document via <see cref="XmlDocumentParser"/>.
/// </summary>
/// <remarks>
/// <para>
/// Contains the complete parse output: the <see cref="Document"/> root node and a flat
/// list of <see cref="Nodes"/> in document order. The flat node list is used by the storage
/// layer to efficiently persist all nodes in a single batch write.
/// </para>
/// <para>
/// The <see cref="NodeCount"/> is provided as a convenience for pre-allocating storage.
/// It always equals <c>Nodes.Count</c>.
/// </para>
/// </remarks>
public sealed class ParseResult
{
    /// <summary>
    /// The root <see cref="XdmDocument"/> node of the parsed tree.
    /// </summary>
    public required XdmDocument Document { get; init; }

    /// <summary>
    /// All parsed nodes in document order, including the document node itself.
    /// </summary>
    /// <remarks>
    /// The document node is at index 0, followed by elements, attributes, text nodes,
    /// comments, and processing instructions in the order they appear in the source XML.
    /// </remarks>
    public required IReadOnlyList<XdmNode> Nodes { get; init; }

    /// <summary>
    /// The total number of nodes in <see cref="Nodes"/>.
    /// </summary>
    public required uint NodeCount { get; init; }
}
