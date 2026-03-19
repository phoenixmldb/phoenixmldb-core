using System.Collections.Immutable;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for XmlSerializer.
/// </summary>
public class XmlSerializerTests
{
    private static readonly DocumentId TestDocId = new(1);
    private ulong _nextNodeId = 1;

    private readonly Dictionary<NodeId, XdmNode> _nodeStore = new();
    private readonly Dictionary<NamespaceId, string> _namespaceUris = new()
    {
        { NamespaceId.None, "" },
        { NamespaceId.Xml, "http://www.w3.org/XML/1998/namespace" },
        { NamespaceId.Xmlns, "http://www.w3.org/2000/xmlns/" },
        { NamespaceId.Xsd, "http://www.w3.org/2001/XMLSchema" },
        { NamespaceId.Xsi, "http://www.w3.org/2001/XMLSchema-instance" },
        { NamespaceId.Fn, "http://www.w3.org/2005/xpath-functions" }
    };

    #region Simple Element Serialization

    [Fact]
    public void Serialize_SimpleElement_ProducesValidXml()
    {
        var element = CreateElement("root");
        var serializer = CreateSerializer();

        var xml = serializer.Serialize(element);

        xml.Should().Contain("<root");
        // Should produce either <root /> or <root></root>
        (xml.Contains("<root />", StringComparison.Ordinal) || xml.Contains("</root>", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void Serialize_EmptyElement_ProducesValidXml()
    {
        var element = CreateElement("empty");
        var serializer = CreateSerializer();

        var xml = serializer.Serialize(element);

        // Should produce either <empty /> or <empty></empty>
        (xml.Contains("<empty />", StringComparison.Ordinal) || xml.Contains("<empty></empty>", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void Serialize_ElementWithText_IncludesTextContent()
    {
        var element = CreateElement("greeting");
        var text = CreateText("Hello, World!", element.Id);
        element = SetChildren(element, text.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("Hello, World!");
    }

    #endregion

    #region Document Serialization

    [Fact]
    public void Serialize_Document_ProducesValidXml()
    {
        var element = CreateElement("root");
        var doc = CreateDocument(element.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(doc);

        xml.Should().Contain("<?xml");
        xml.Should().Contain("<root");
    }

    [Fact]
    public void Serialize_DocumentWithProlog_IncludesDeclaration()
    {
        var element = CreateElement("root");
        var doc = CreateDocument(element.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(doc);

        // StringBuilder-based XmlWriter produces utf-16, stream-based produces utf-8
        (xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal) ||
         xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void Serialize_DocumentWithComment_IncludesComment()
    {
        var element = CreateElement("root");
        var comment = CreateComment(" A comment ", null);
        var doc = CreateDocument(element.Id, comment.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(doc);

        xml.Should().Contain("<!-- A comment -->");
    }

    [Fact]
    public void Serialize_DocumentWithPI_IncludesPI()
    {
        var element = CreateElement("root");
        var pi = CreatePI("custom", "data here", null);
        var doc = CreateDocument(element.Id, pi.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(doc);

        xml.Should().Contain("<?custom data here?>");
    }

    #endregion

    #region Attribute Serialization

    [Fact]
    public void Serialize_ElementWithAttribute_IncludesAttribute()
    {
        var element = CreateElement("root");
        var attr = CreateAttribute("class", "test-class", element.Id);
        element = SetAttributes(element, attr.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("class=\"test-class\"");
    }

    [Fact]
    public void Serialize_ElementWithMultipleAttributes_IncludesAll()
    {
        var element = CreateElement("input");
        var attr1 = CreateAttribute("type", "text", element.Id);
        var attr2 = CreateAttribute("name", "username", element.Id);
        var attr3 = CreateAttribute("value", "test", element.Id);
        element = SetAttributes(element, attr1.Id, attr2.Id, attr3.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("type=\"text\"");
        xml.Should().Contain("name=\"username\"");
        xml.Should().Contain("value=\"test\"");
    }

    [Fact]
    public void Serialize_AttributeWithSpecialChars_EscapesCorrectly()
    {
        var element = CreateElement("root");
        var attr = CreateAttribute("data", "a & b < c > d \"e\"", element.Id);
        element = SetAttributes(element, attr.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("&amp;");
        xml.Should().Contain("&lt;");
        xml.Should().Contain("&gt;");
        xml.Should().Contain("&quot;");
    }

    [Fact]
    public void Serialize_PrefixedAttribute_IncludesPrefix()
    {
        var element = CreateElement("root");
        var attr = CreateAttribute("lang", "en", element.Id, NamespaceId.Xml, "xml");
        element = SetAttributes(element, attr.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("xml:lang=\"en\"");
    }

    #endregion

    #region Namespace Serialization

    [Fact]
    public void Serialize_ElementWithNamespace_IncludesNamespaceDeclaration()
    {
        var element = CreateElement("root", NamespaceId.Xsd, "xs");
        AddNamespaceDeclaration(element, "xs", NamespaceId.Xsd);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"");
        xml.Should().Contain("xs:root");
    }

    [Fact]
    public void Serialize_ElementWithDefaultNamespace_IncludesXmlns()
    {
        var nsId = new NamespaceId(100);
        _namespaceUris[nsId] = "http://example.com";

        var element = CreateElement("root", nsId, null);
        AddNamespaceDeclaration(element, "", nsId);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("xmlns=\"http://example.com\"");
    }

    [Fact]
    public void Serialize_NestedElementsWithDifferentNamespaces_HandlesCorrectly()
    {
        var ns1 = new NamespaceId(100);
        var ns2 = new NamespaceId(101);
        _namespaceUris[ns1] = "http://ns1.com";
        _namespaceUris[ns2] = "http://ns2.com";

        var parent = CreateElement("parent", ns1, "a");
        AddNamespaceDeclaration(parent, "a", ns1);

        var child = CreateElement("child", ns2, "b", parent.Id);
        AddNamespaceDeclaration(child, "b", ns2);
        parent = SetChildren(parent, child.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(parent);

        xml.Should().Contain("xmlns:a=\"http://ns1.com\"");
        xml.Should().Contain("xmlns:b=\"http://ns2.com\"");
        xml.Should().Contain("a:parent");
        xml.Should().Contain("b:child");
    }

    #endregion

    #region Comment Serialization

    [Fact]
    public void Serialize_Comment_ProducesValidSyntax()
    {
        var element = CreateElement("root");
        var comment = CreateComment(" This is a comment ", element.Id);
        element = SetChildren(element, comment.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("<!-- This is a comment -->");
    }

    [Fact]
    public void Serialize_EmptyComment_ProducesValidSyntax()
    {
        var element = CreateElement("root");
        var comment = CreateComment("", element.Id);
        element = SetChildren(element, comment.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("<!---->");
    }

    #endregion

    #region Processing Instruction Serialization

    [Fact]
    public void Serialize_ProcessingInstruction_ProducesValidSyntax()
    {
        var element = CreateElement("root");
        var pi = CreatePI("target", "data content", element.Id);
        element = SetChildren(element, pi.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("<?target data content?>");
    }

    [Fact]
    public void Serialize_PIWithoutData_ProducesValidSyntax()
    {
        var element = CreateElement("root");
        var pi = CreatePI("target", "", element.Id);
        element = SetChildren(element, pi.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("<?target");
        xml.Should().Contain("?>");
    }

    #endregion

    #region Text Content Serialization

    [Fact]
    public void Serialize_TextWithSpecialChars_EscapesCorrectly()
    {
        var element = CreateElement("root");
        var text = CreateText("a < b > c & d", element.Id);
        element = SetChildren(element, text.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("a &lt; b &gt; c &amp; d");
    }

    [Fact]
    public void Serialize_TextWithWhitespace_PreservesWhitespace()
    {
        var element = CreateElement("root");
        var text = CreateText("  spaces  and\nnewline  ", element.Id);
        element = SetChildren(element, text.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("  spaces  and");
        xml.Should().Contain("newline  ");
    }

    [Fact]
    public void Serialize_UnicodeText_PreservesCharacters()
    {
        var element = CreateElement("root");
        var text = CreateText("\u00E9\u00E8\u00EA \u4E2D\u6587", element.Id);
        element = SetChildren(element, text.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().Contain("\u00E9\u00E8\u00EA \u4E2D\u6587");
    }

    #endregion

    #region Nested Structure Serialization

    [Fact]
    public void Serialize_NestedElements_ProducesCorrectStructure()
    {
        var root = CreateElement("root");
        var child1 = CreateElement("child1", NamespaceId.None, null, root.Id);
        var child2 = CreateElement("child2", NamespaceId.None, null, root.Id);
        root = SetChildren(root, child1.Id, child2.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(root);

        var child1Index = xml.IndexOf("<child1", StringComparison.Ordinal);
        var child2Index = xml.IndexOf("<child2", StringComparison.Ordinal);
        child1Index.Should().BeLessThan(child2Index);
    }

    [Fact]
    public void Serialize_DeeplyNestedStructure_WorksCorrectly()
    {
        var a = CreateElement("a");
        var b = CreateElement("b", NamespaceId.None, null, a.Id);
        var c = CreateElement("c", NamespaceId.None, null, b.Id);
        var d = CreateElement("d", NamespaceId.None, null, c.Id);

        a = SetChildren(a, b.Id);
        b = SetChildren(b, c.Id);
        c = SetChildren(c, d.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(a);

        xml.Should().Contain("<a>");
        xml.Should().Contain("<b>");
        xml.Should().Contain("<c>");
        xml.Should().Contain("<d");
    }

    [Fact]
    public void Serialize_MixedContent_PreservesOrder()
    {
        var root = CreateElement("root");
        var text1 = CreateText("Before ", root.Id);
        var child = CreateElement("child", NamespaceId.None, null, root.Id);
        var text2 = CreateText(" After", root.Id);
        root = SetChildren(root, text1.Id, child.Id, text2.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(root);

        xml.Should().Contain("Before <child");
        // Accept both self-closing and explicit closing forms
        (xml.Contains("</child> After", StringComparison.Ordinal) || xml.Contains("<child /> After", StringComparison.Ordinal)).Should().BeTrue();
    }

    #endregion

    #region Indentation Tests

    [Fact]
    public void Serialize_WithIndent_ProducesFormattedOutput()
    {
        var root = CreateElement("root");
        var child = CreateElement("child", NamespaceId.None, null, root.Id);
        root = SetChildren(root, child.Id);

        var serializer = CreateSerializer(indent: true);
        var xml = serializer.Serialize(root);

        xml.Should().Contain("\n");
        // Indented output should have whitespace before child elements
    }

    [Fact]
    public void Serialize_WithoutIndent_ProducesCompactOutput()
    {
        var root = CreateElement("root");
        var child = CreateElement("child", NamespaceId.None, null, root.Id);
        root = SetChildren(root, child.Id);

        var serializer = CreateSerializer(indent: false);
        var xml = serializer.Serialize(root);

        // Should not have formatting newlines between elements
        xml.Should().Contain("><child");
    }

    #endregion

    #region Round-Trip Tests

    [Theory]
    [InlineData("<root/>")]
    [InlineData("<root>text</root>")]
    [InlineData("<root attr=\"value\"/>")]
    [InlineData("<root><child/></root>")]
    [InlineData("<root><!-- comment --></root>")]
    public void RoundTrip_SimpleDocuments_PreservesStructure(string originalXml)
    {
        // Parse
        var parser = CreateParser();
        var parseResult = parser.Parse(originalXml);

        // Store nodes
        foreach (var node in parseResult.Nodes)
        {
            _nodeStore[node.Id] = node;
        }

        // Serialize
        var serializer = CreateSerializer();
        var serializedXml = serializer.Serialize(parseResult.Document);

        // Parse again
        var parser2 = new XmlDocumentParser(
            new DocumentId(2),
            new NodeId(1000),
            uri => ResolveNamespace(uri),
            false);
        var reparsed = parser2.Parse(serializedXml);

        // Compare structure
        reparsed.Nodes.OfType<XdmElement>().Count().Should()
            .Be(parseResult.Nodes.OfType<XdmElement>().Count());
    }

    [Fact]
    public void RoundTrip_ComplexDocument_PreservesContent()
    {
        const string originalXml = @"
            <root xmlns=""http://example.com"" attr=""value"">
                <child1>Text content</child1>
                <child2 a=""1"" b=""2""/>
                <!-- comment -->
                <?target data?>
            </root>";

        var parser = CreateParser();
        var parseResult = parser.Parse(originalXml);

        foreach (var node in parseResult.Nodes)
        {
            _nodeStore[node.Id] = node;
        }

        var serializer = CreateSerializer();
        var serializedXml = serializer.Serialize(parseResult.Document);

        // Verify key content is preserved
        serializedXml.Should().Contain("xmlns=\"http://example.com\"");
        serializedXml.Should().Contain("attr=\"value\"");
        serializedXml.Should().Contain("Text content");
        serializedXml.Should().Contain("child2");
        serializedXml.Should().Contain("<!-- comment -->");
        serializedXml.Should().Contain("<?target data?>");
    }

    [Fact]
    public void RoundTrip_WithAttributes_PreservesAttributeValues()
    {
        const string originalXml = "<element a=\"1\" b=\"2\" c=\"3\"/>";

        var parser = CreateParser();
        var parseResult = parser.Parse(originalXml);

        foreach (var node in parseResult.Nodes)
        {
            _nodeStore[node.Id] = node;
        }

        var serializer = CreateSerializer();
        var serializedXml = serializer.Serialize(parseResult.Document);

        serializedXml.Should().Contain("a=\"1\"");
        serializedXml.Should().Contain("b=\"2\"");
        serializedXml.Should().Contain("c=\"3\"");
    }

    [Fact]
    public void RoundTrip_WithNamespaces_PreservesNamespaces()
    {
        const string originalXml = @"<xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema""/>";

        var parser = CreateParser();
        var parseResult = parser.Parse(originalXml);

        foreach (var node in parseResult.Nodes)
        {
            _nodeStore[node.Id] = node;
        }

        var serializer = CreateSerializer();
        var serializedXml = serializer.Serialize(parseResult.Document);

        serializedXml.Should().Contain("xs:schema");
        serializedXml.Should().Contain("xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"");
    }

    #endregion

    #region TextWriter Serialization

    [Fact]
    public void Serialize_ToTextWriter_WorksCorrectly()
    {
        var element = CreateElement("root");
        var text = CreateText("content", element.Id);
        element = SetChildren(element, text.Id);
        var doc = CreateDocument(element.Id);

        var serializer = CreateSerializer();
        using var writer = new StringWriter();
        serializer.Serialize(doc, writer);
        var xml = writer.ToString();

        xml.Should().Contain("<root>content</root>");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Serialize_ElementWithNoChildren_ProducesValidXml()
    {
        var element = CreateElement("empty");

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        xml.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Serialize_NullNodeResolver_HandlesGracefully()
    {
        var element = CreateElement("root");
        var childId = new NodeId(9999); // Non-existent
        element = SetChildren(element, childId);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(element);

        // Should not throw, just skip missing nodes
        xml.Should().Contain("<root");
    }

    [Fact]
    public void Serialize_DocumentWithMultipleTopLevelNodes_IncludesAll()
    {
        var comment = CreateComment(" prolog ", null);
        var element = CreateElement("root");
        var pi = CreatePI("target", "data", null);
        var doc = CreateDocument(comment.Id, element.Id, pi.Id);

        var serializer = CreateSerializer();
        var xml = serializer.Serialize(doc);

        xml.Should().Contain("<!-- prolog -->");
        xml.Should().Contain("<root");
        xml.Should().Contain("<?target data?>");
    }

    #endregion

    #region Helper Methods

    private XdmElement CreateElement(
        string localName,
        NamespaceId ns = default,
        string? prefix = null,
        NodeId? parent = null)
    {
        if (ns == default) ns = NamespaceId.None;

        var nodeId = new NodeId(_nextNodeId++);
        var element = new XdmElement
        {
            Id = nodeId,
            Document = TestDocId,
            Namespace = ns,
            LocalName = localName,
            Prefix = prefix,
            Parent = parent,
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };

        _nodeStore[nodeId] = element;
        return element;
    }

    private XdmAttribute CreateAttribute(
        string localName,
        string value,
        NodeId parent,
        NamespaceId ns = default,
        string? prefix = null)
    {
        if (ns == default) ns = NamespaceId.None;

        var nodeId = new NodeId(_nextNodeId++);
        var attr = new XdmAttribute
        {
            Id = nodeId,
            Document = TestDocId,
            Namespace = ns,
            LocalName = localName,
            Prefix = prefix,
            Parent = parent,
            Value = value
        };

        _nodeStore[nodeId] = attr;
        return attr;
    }

    private XdmText CreateText(string value, NodeId parent)
    {
        var nodeId = new NodeId(_nextNodeId++);
        var text = new XdmText
        {
            Id = nodeId,
            Document = TestDocId,
            Parent = parent,
            Value = value
        };

        _nodeStore[nodeId] = text;
        return text;
    }

    private XdmComment CreateComment(string value, NodeId? parent)
    {
        var nodeId = new NodeId(_nextNodeId++);
        var comment = new XdmComment
        {
            Id = nodeId,
            Document = TestDocId,
            Parent = parent,
            Value = value
        };

        _nodeStore[nodeId] = comment;
        return comment;
    }

    private XdmProcessingInstruction CreatePI(string target, string value, NodeId? parent)
    {
        var nodeId = new NodeId(_nextNodeId++);
        var pi = new XdmProcessingInstruction
        {
            Id = nodeId,
            Document = TestDocId,
            Parent = parent,
            Target = target,
            Value = value
        };

        _nodeStore[nodeId] = pi;
        return pi;
    }

    private XdmDocument CreateDocument(params NodeId[] childIds)
    {
        var nodeId = new NodeId(_nextNodeId++);
        var elementId = childIds.FirstOrDefault(id =>
            _nodeStore.TryGetValue(id, out var n) && n is XdmElement);

        var doc = new XdmDocument
        {
            Id = nodeId,
            Document = TestDocId,
            Children = childIds.ToImmutableArray(),
            DocumentElement = elementId != default ? elementId : null
        };

        _nodeStore[nodeId] = doc;
        return doc;
    }

    private XdmElement SetChildren(XdmElement element, params NodeId[] childIds)
    {
        // Create a new element with updated children
        var updated = new XdmElement
        {
            Id = element.Id,
            Document = element.Document,
            Namespace = element.Namespace,
            LocalName = element.LocalName,
            Prefix = element.Prefix,
            Parent = element.Parent,
            Children = childIds.ToImmutableArray(),
            Attributes = element.Attributes,
            NamespaceDeclarations = element.NamespaceDeclarations
        };

        _nodeStore[element.Id] = updated;
        return updated;
    }

    private XdmElement SetAttributes(XdmElement element, params NodeId[] attrIds)
    {
        var updated = new XdmElement
        {
            Id = element.Id,
            Document = element.Document,
            Namespace = element.Namespace,
            LocalName = element.LocalName,
            Prefix = element.Prefix,
            Parent = element.Parent,
            Children = element.Children,
            Attributes = attrIds.ToImmutableArray(),
            NamespaceDeclarations = element.NamespaceDeclarations
        };

        _nodeStore[element.Id] = updated;
        return updated;
    }

    private void AddNamespaceDeclaration(XdmElement element, string prefix, NamespaceId ns)
    {
        var currentNsDecls = element.NamespaceDeclarations.ToList();
        currentNsDecls.Add(new NamespaceBinding(prefix, ns));

        var updated = new XdmElement
        {
            Id = element.Id,
            Document = element.Document,
            Namespace = element.Namespace,
            LocalName = element.LocalName,
            Prefix = element.Prefix,
            Parent = element.Parent,
            Children = element.Children,
            Attributes = element.Attributes,
            NamespaceDeclarations = currentNsDecls.ToImmutableArray()
        };

        _nodeStore[element.Id] = updated;
    }

    private XmlSerializer CreateSerializer(bool indent = false)
    {
        return new XmlSerializer(
            id => _nodeStore.TryGetValue(id, out var node) ? node : null,
            ns => _namespaceUris.TryGetValue(ns, out var uri) ? uri : null,
            indent);
    }

    private XmlDocumentParser CreateParser()
    {
        return new XmlDocumentParser(
            TestDocId,
            new NodeId(1),
            ResolveNamespace,
            false);
    }

    private NamespaceId ResolveNamespace(string uri)
    {
        foreach (var kvp in _namespaceUris)
        {
            if (kvp.Value == uri)
                return kvp.Key;
        }

        var newId = new NamespaceId((uint)(100 + _namespaceUris.Count));
        _namespaceUris[newId] = uri;
        return newId;
    }

    #endregion
}
