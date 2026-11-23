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
    // When stripping: for each element in document order (start-tag order), whether it has a
    // text child with a non-whitespace character (mixed content). See PrescanMixedContent.
    private List<bool>? _mixedContent;
    private int _elementOrdinal;

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
    /// When <c>true</c>, every whitespace-only text node is kept. When <c>false</c> (default), a
    /// whitespace-only text node is discarded only in element-only content (indentation between
    /// elements). It is kept in mixed content (an element that also has a non-whitespace text
    /// child), under <c>xml:space="preserve"</c> until an inner <c>xml:space="default"</c>, and when
    /// it holds a CDATA section.
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
    /// Names the type annotation for a schema type the validating reader reports on an element
    /// or attribute (for a union, the member type that matched). Returning <see langword="null"/>
    /// leaves the default: the type's own qualified name, or no annotation when it has none.
    /// </summary>
    /// <remarks>
    /// The default cannot annotate a node whose type is anonymous — there is no name to record —
    /// so such a node stays <c>xs:untyped</c> and is indistinguishable from one that was never
    /// validated. A caller that owns the schemas can assign anonymous types names of its own here
    /// and resolve them again later.
    /// </remarks>
    public Func<System.Xml.Schema.XmlSchemaType, XdmTypeName?>? SchemaTypeAnnotator { get; set; }

    /// <summary>
    /// Parses XML content from a string into an XDM document tree.
    /// </summary>
    /// <param name="xml">The XML content to parse. Must be well-formed XML.</param>
    /// <param name="documentUri">Optional document URI to assign to the resulting <see cref="XdmDocument"/>.</param>
    /// <returns>A <see cref="ParseResult"/> containing the document and all parsed nodes.</returns>
    /// <exception cref="System.Xml.XmlException">The XML content is not well-formed.</exception>
    public ParseResult Parse(string xml, string? documentUri = null)
    {
        if (!_preserveWhitespace)
        {
            using var prescanSource = new StringReader(xml);
            _mixedContent = PrescanMixedContent(prescanSource);
        }
        using var reader = new StringReader(xml);
        return ParseTextReader(reader, documentUri);
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
        // Stripping needs two passes over the text (see PrescanMixedContent); a TextReader
        // cannot be rewound, so it is read once into a string.
        if (!_preserveWhitespace)
            return Parse(textReader.ReadToEnd(), documentUri);
        return ParseTextReader(textReader, documentUri);
    }

    private ParseResult ParseTextReader(TextReader textReader, string? documentUri)
    {
        var settings = new XmlReaderSettings
        {
            // Whitespace is always read; ParseElement decides what to strip (phoenixml#130).
            IgnoreWhitespace = false,
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
        if (!_preserveWhitespace)
        {
            var xml = textReader.ReadToEnd();
            using (var prescanSource = new StringReader(xml))
                _mixedContent = PrescanMixedContent(prescanSource);
            textReader = new StringReader(xml);
        }
        var settings = new XmlReaderSettings
        {
            // Whitespace is always read; ParseElement decides what to strip (phoenixml#130).
            IgnoreWhitespace = false,
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
        if (!_preserveWhitespace)
        {
            // Two passes (see PrescanMixedContent): a seekable stream is read again from where
            // it started; any other stream is copied once.
            if (!stream.CanSeek)
            {
                var copy = new MemoryStream();
                stream.CopyTo(copy);
                copy.Position = 0;
                stream = copy;
            }
            var start = stream.Position;
            _mixedContent = PrescanMixedContent(stream);
            stream.Position = start;
        }
        var settings = new XmlReaderSettings
        {
            // Whitespace is always read; ParseElement decides what to strip (phoenixml#130).
            IgnoreWhitespace = false,
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
        _elementOrdinal = 0;
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

    /// <summary>
    /// Parses the element the reader is on, with its content, into nodes; returns its id.
    /// </summary>
    /// <remarks>
    /// One loop with an explicit stack of open elements, not a call per nesting level: the
    /// recursive form used about 1.4 KB of stack per level, so how deep a document could be
    /// depended on the stack of the thread that parsed it (710 levels on a 1 MB stack), and past
    /// that it stopped with InsufficientExecutionStackException. The nodes, their ids and order,
    /// text and whitespace (phoenixml#130), line info and namespaces are what the recursive form
    /// gave: each element is opened (its id, attributes and namespace declarations), filled and
    /// closed (built and added) at the same points as before.
    /// </remarks>
    private NodeId ParseElement(XmlReader reader, NodeId? parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        var top = OpenElement(reader, parentId, lineInfo);
        if (top.IsEmpty)
            return CloseElement(top);

        var open = new Stack<ElementFrame>();
        open.Push(top);
        while (reader.Read())
        {
            var frame = open.Peek();
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement:
                    frame.FlushRun(this);
                    var closedId = CloseElement(open.Pop());
                    if (open.Count == 0)
                        return closedId;
                    open.Peek().Children.Add(closedId);
                    break;

                case XmlNodeType.Element:
                    frame.FlushRun(this);
                    var child = OpenElement(reader, frame.NodeId, lineInfo);
                    if (child.IsEmpty)
                        frame.Children.Add(CloseElement(child));
                    else
                        open.Push(child);
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    // All character data joins the run (CDATA is a serialization detail, not
                    // a node kind in XDM); FlushRun decides whether a whitespace-only run is
                    // stripped. A CDATA section is never stripped: its whitespace was written
                    // as data on purpose.
                    if (reader.NodeType == XmlNodeType.CDATA || reader.XmlSpace == XmlSpace.Preserve)
                        frame.RunStrippable = false;
                    frame.AppendRun(reader.Value, lineInfo);
                    break;

                case XmlNodeType.Comment:
                    frame.FlushRun(this);
                    frame.Children.Add(ParseComment(reader, frame.NodeId, lineInfo));
                    break;

                case XmlNodeType.ProcessingInstruction:
                    frame.FlushRun(this);
                    frame.Children.Add(ParseProcessingInstruction(reader, frame.NodeId, lineInfo));
                    break;
            }
        }

        // Defensive close: a well-formed document always ends at the outermost EndElement (handled
        // above), but a reader that stops otherwise leaves elements open; they are closed in turn,
        // innermost first, as the recursive form returned from each level.
        while (true)
        {
            var frame = open.Pop();
            frame.FlushRun(this);
            var closedId = CloseElement(frame);
            if (open.Count == 0)
                return closedId;
            open.Peek().Children.Add(closedId);
        }
    }

    /// <summary>
    /// An element being parsed: what its start tag gave, its children so far, and the run of
    /// character data not yet made a text node.
    /// </summary>
    private sealed class ElementFrame
    {
        public NodeId NodeId;
        public NodeId? ParentId;
        public NamespaceId NamespaceId;
        public string LocalName = "";
        public string? Prefix;
        public bool IsEmpty;
        public bool MixedContent;
        public bool IsIdContent;
        public System.Xml.XmlQualifiedName? SchemaTypeQName;
        public System.Xml.Schema.XmlSchemaType? SchemaType;
        public List<NodeId> Attributes = null!;
        public List<NamespaceBinding> NamespaceDecls = null!;
        public int SourceLine;
        public int SourceColumn;
        public readonly List<NodeId> Children = new();

        // Pending coalesced character-data run for the current sibling position. XmlReader raises
        // a SEPARATE character-data event at every CDATA and entity boundary (e.g.
        // "abc<![CDATA[def]]>ghi" is three events), but XDM forbids two consecutive text-node
        // siblings, so a run of consecutive character-data events is one XdmText node. A run is
        // broken only by an element, comment, or PI (or the end tag). The source line/column are
        // captured from the FIRST event contributing to the run.
        private System.Text.StringBuilder? _runBuilder;
        private int _runLine;
        private int _runCol;
        // Whether the run may be stripped: no CDATA section in it, and no part of it under
        // xml:space="preserve".
        public bool RunStrippable = true;

        public void AppendRun(string value, System.Xml.IXmlLineInfo? lineInfo)
        {
            if (value.Length == 0)
                return;
            if (_runBuilder is null)
            {
                _runBuilder = new System.Text.StringBuilder(value.Length);
                _runLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
                _runCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;
            }
            _runBuilder.Append(value);
        }

        public void FlushRun(XmlDocumentParser parser)
        {
            // PreserveWhitespace=false strips a whitespace-only text node only in element-only
            // content (indentation between elements). Whitespace in mixed content (an element
            // that also has non-whitespace text) and under xml:space="preserve" (XML 1.0
            // §2.10) is data, and is kept. XmlReaderSettings.IgnoreWhitespace dropped every
            // whitespace-only span: <p>a <b>b</b> <i>c</i></p> read back as "a bc"
            // (phoenixml#130).
            if (_runBuilder is { Length: > 0 }
                && !(RunStrippable && !parser._preserveWhitespace && !MixedContent && IsXmlWhitespace(_runBuilder)))
                Children.Add(parser.CreateTextNode(_runBuilder.ToString(), NodeId, _runLine, _runCol));
            _runBuilder = null;
            RunStrippable = true;
        }
    }

    /// <summary>
    /// What the start tag the reader is on gives: the element's id (allocated here, before its
    /// attributes'), name, schema type, attributes and namespace declarations.
    /// </summary>
    private ElementFrame OpenElement(XmlReader reader, NodeId? parentId, System.Xml.IXmlLineInfo? lineInfo)
    {
        // Capture position before MoveToFirstAttribute shifts the reader cursor.
        var sourceLine = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        var sourceCol = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;

        var nodeId = AllocateNodeId();
        var namespaceUri = reader.NamespaceURI ?? string.Empty;
        var namespaceId = _namespaceResolver(namespaceUri);
        var localName = reader.LocalName;
        var prefix = string.IsNullOrEmpty(reader.Prefix) ? null : reader.Prefix;
        var isEmpty = reader.IsEmptyElement;
        var mixedContent = _mixedContent is { } mixed && _elementOrdinal < mixed.Count && mixed[_elementOrdinal];
        _elementOrdinal++;

        // Detect xs:ID-typed simple-content elements. An element's content is ID-typed when:
        // - the schema type (or its simple-type datatype) has XmlTypeCode.Id (xs:ID or derived), OR
        // - the element has a union type and the selected MemberType is xs:ID (e.g., xs:ID | xs:integer)
        // For types derived from xs:ID by restriction, Datatype.TypeCode still reports Id.
        var isIdContent = false;
        // Captured for the XdmElement.TypeAnnotation projection below.
        // Schema-validating readers populate SchemaInfo.SchemaType only when a
        // global element declaration matched (strict/lax). The annotation is the governing
        // type: for a union type the union, not the member the value matched (XDM 3.1
        // §3.3.1.2, [type definition]). The member decides the typed value, which is computed
        // from the annotation and the string value. With the member as the annotation,
        // element(*, M) matched an element declared with the union type U (W3C XSLT match-195).
        System.Xml.XmlQualifiedName? elementSchemaTypeQName = null;
        System.Xml.Schema.XmlSchemaType? elementSchemaType = null;
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
            elementSchemaTypeQName = elemSchemaInfo.SchemaType?.QualifiedName
                ?? elemSchemaInfo.MemberType?.QualifiedName;
            elementSchemaType = elemSchemaInfo.SchemaType ?? elemSchemaInfo.MemberType;
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

        return new ElementFrame
        {
            NodeId = nodeId,
            ParentId = parentId,
            NamespaceId = namespaceId,
            LocalName = localName,
            Prefix = prefix,
            IsEmpty = isEmpty,
            MixedContent = mixedContent,
            IsIdContent = isIdContent,
            SchemaTypeQName = elementSchemaTypeQName,
            SchemaType = elementSchemaType,
            Attributes = attributes,
            NamespaceDecls = namespaceDecls,
            SourceLine = sourceLine,
            SourceColumn = sourceCol,
        };
    }

    /// <summary>Builds the element of <paramref name="frame"/> with its children, adds it, and returns its id.</summary>
    private NodeId CloseElement(ElementFrame frame)
    {
        var nodeId = frame.NodeId;
        var children = frame.Children;
        var elementSchemaType = frame.SchemaType;
        var elementSchemaTypeQName = frame.SchemaTypeQName;
        var elementTypeAnnotation = AnnotatorName(elementSchemaType)
            ?? ResolveSchemaTypeAnnotation(elementSchemaTypeQName, XdmTypeName.Untyped);

        var element = new XdmElement
        {
            Id = nodeId,
            Document = _documentId,
            Namespace = frame.NamespaceId,
            LocalName = frame.LocalName,
            Prefix = frame.Prefix,
            Parent = frame.ParentId,
            Attributes = frame.Attributes.ToImmutableArray(),
            NamespaceDeclarations = frame.NamespaceDecls.ToImmutableArray(),
            Children = children.ToImmutableArray(),
            IsIdContent = frame.IsIdContent,
            TypeAnnotation = elementTypeAnnotation,
            SourceLine = frame.SourceLine,
            SourceColumn = frame.SourceColumn
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
    private XdmTypeName? AnnotatorName(System.Xml.Schema.XmlSchemaType? schemaType)
        => schemaType is null ? null : SchemaTypeAnnotator?.Invoke(schemaType);

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

        // Pull the attribute's schema type for TypeAnnotation: the governing type, for a union
        // the union and not the member the value matched, as for an element above.
        System.Xml.XmlQualifiedName? attrSchemaTypeQName = null;
        System.Xml.Schema.XmlSchemaType? attrSchemaType = null;
        if (reader.SchemaInfo is System.Xml.Schema.IXmlSchemaInfo attrSchemaInfo)
        {
            attrSchemaTypeQName = attrSchemaInfo.SchemaType?.QualifiedName
                ?? attrSchemaInfo.MemberType?.QualifiedName;
            attrSchemaType = attrSchemaInfo.SchemaType ?? attrSchemaInfo.MemberType;
        }
        var attrTypeAnnotation = AnnotatorName(attrSchemaType)
            ?? ResolveSchemaTypeAnnotation(attrSchemaTypeQName, XdmTypeName.UntypedAtomic);

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
    /// The first of two passes when whitespace is stripped: for each element in document order
    /// (start-tag order, as <see cref="ParseElement"/> counts them), whether it has a text child
    /// with a non-whitespace character. A whitespace-only text node is stripped only from an
    /// element that has none (element-only content), and that is known only at the end tag. The
    /// node must be decided before it gets a node id: ids are allocated in document order, they
    /// give document order, and they are contiguous (a store advances by
    /// <see cref="ParseResult.NodeCount"/>). So the decision cannot wait for the end tag.
    /// </summary>
    private static List<bool> PrescanMixedContent(TextReader source)
    {
        using var reader = XmlReader.Create(source, PrescanSettings());
        return PrescanMixedContent(reader);
    }

    private static List<bool> PrescanMixedContent(Stream source)
    {
        using var reader = XmlReader.Create(source, PrescanSettings());
        return PrescanMixedContent(reader);
    }

    private static XmlReaderSettings PrescanSettings() => new()
    {
        IgnoreWhitespace = false,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        DtdProcessing = DtdProcessing.Parse, // entity references expand as in the main pass
        MaxCharactersFromEntities = 1_000_000,
        ValidationType = ValidationType.None,
        CloseInput = false,
    };

    private static List<bool> PrescanMixedContent(XmlReader reader)
    {
        var mixed = new List<bool>();
        var open = new Stack<int>(); // ordinals of the elements whose content is being read
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    mixed.Add(false);
                    if (!reader.IsEmptyElement)
                        open.Push(mixed.Count - 1);
                    break;
                case XmlNodeType.EndElement:
                    open.Pop();
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    if (open.Count > 0 && !IsXmlWhitespace(reader.Value))
                        mixed[open.Peek()] = true;
                    break;
            }
        }
        return mixed;
    }

    private static bool IsXmlWhitespace(string value)
    {
        foreach (var c in value)
            if (c is not (' ' or '\t' or '\r' or '\n'))
                return false;
        return true;
    }

    private static bool IsXmlWhitespace(System.Text.StringBuilder value)
    {
        foreach (var chunk in value.GetChunks())
            foreach (var c in chunk.Span)
                if (c is not (' ' or '\t' or '\r' or '\n'))
                    return false;
        return true;
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
