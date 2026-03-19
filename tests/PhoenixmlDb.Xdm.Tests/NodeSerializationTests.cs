using System.Collections.Immutable;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Serialization;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for NodeSerializer and NodeReader binary serialization.
/// </summary>
public class NodeSerializationTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(1);
    private static readonly NodeId ParentNodeId = new(0);

    #region Document Serialization

    [Fact]
    public void Serialize_Document_WritesCorrectKind()
    {
        var doc = CreateDocument();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(doc, buffer);

        bytesWritten.Should().BeGreaterThan(0);
        buffer[0].Should().Be((byte)XdmNodeKind.Document);
    }

    [Fact]
    public void RoundTrip_EmptyDocument_PreservesProperties()
    {
        var doc = CreateDocument();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(doc, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(doc.Id, doc.Document) as XdmDocument;

        result.Should().NotBeNull();
        result!.Id.Should().Be(doc.Id);
        result.Document.Should().Be(doc.Document);
        result.Children.Should().BeEmpty();
        result.DocumentUri.Should().BeNull();
        result.DocumentElement.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_DocumentWithUri_PreservesUri()
    {
        var doc = new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = XdmDocument.EmptyChildren,
            DocumentUri = "file:///test/document.xml"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(doc, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(doc.Id, doc.Document) as XdmDocument;

        result!.DocumentUri.Should().Be("file:///test/document.xml");
    }

    [Fact]
    public void RoundTrip_DocumentWithChildren_PreservesChildren()
    {
        var children = ImmutableArray.Create(new NodeId(2), new NodeId(3), new NodeId(4));
        var doc = new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = children,
            DocumentElement = new NodeId(2)
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(doc, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(doc.Id, doc.Document) as XdmDocument;

        result!.Children.Should().BeEquivalentTo(children);
        result.DocumentElement.Should().Be(new NodeId(2));
    }

    #endregion

    #region Element Serialization

    [Fact]
    public void Serialize_Element_WritesCorrectKind()
    {
        var element = CreateElement();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);

        bytesWritten.Should().BeGreaterThan(0);
        buffer[0].Should().Be((byte)XdmNodeKind.Element);
    }

    [Fact]
    public void RoundTrip_SimpleElement_PreservesProperties()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "div",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result.Should().NotBeNull();
        result!.Id.Should().Be(element.Id);
        result.Document.Should().Be(element.Document);
        result.Namespace.Should().Be(NamespaceId.None);
        result.LocalName.Should().Be("div");
    }

    [Fact]
    public void RoundTrip_ElementWithNamespace_PreservesNamespace()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xsd,
            LocalName = "schema",
            Prefix = "xs",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Namespace.Should().Be(NamespaceId.Xsd);
        result.LocalName.Should().Be("schema");
        result.Prefix.Should().Be("xs");
    }

    [Fact]
    public void RoundTrip_ElementWithParent_PreservesParent()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "child",
            Parent = new NodeId(100),
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Parent.Should().Be(new NodeId(100));
    }

    [Fact]
    public void RoundTrip_ElementWithAttributes_PreservesAttributes()
    {
        var attrs = ImmutableArray.Create(new NodeId(10), new NodeId(11), new NodeId(12));
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "input",
            Attributes = attrs,
            Children = XdmElement.EmptyChildren,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Attributes.Should().BeEquivalentTo(attrs);
    }

    [Fact]
    public void RoundTrip_ElementWithChildren_PreservesChildren()
    {
        var children = ImmutableArray.Create(new NodeId(20), new NodeId(21));
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "parent",
            Children = children,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Children.Should().BeEquivalentTo(children);
    }

    [Fact]
    public void RoundTrip_ElementWithNamespaceDeclarations_PreservesDeclarations()
    {
        var nsDecls = ImmutableArray.Create(
            new NamespaceBinding("xs", NamespaceId.Xsd),
            new NamespaceBinding("xsi", NamespaceId.Xsi)
        );
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "root",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = nsDecls
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.NamespaceDeclarations.Should().HaveCount(2);
        result.NamespaceDeclarations[0].Prefix.Should().Be("xs");
        result.NamespaceDeclarations[0].Namespace.Should().Be(NamespaceId.Xsd);
        result.NamespaceDeclarations[1].Prefix.Should().Be("xsi");
        result.NamespaceDeclarations[1].Namespace.Should().Be(NamespaceId.Xsi);
    }

    [Fact]
    public void RoundTrip_ComplexElement_PreservesAllProperties()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xsd,
            LocalName = "element",
            Prefix = "xs",
            Parent = new NodeId(5),
            Children = ImmutableArray.Create(new NodeId(10), new NodeId(11)),
            Attributes = ImmutableArray.Create(new NodeId(20)),
            NamespaceDeclarations = ImmutableArray.Create(new NamespaceBinding("xs", NamespaceId.Xsd))
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Namespace.Should().Be(NamespaceId.Xsd);
        result.LocalName.Should().Be("element");
        result.Prefix.Should().Be("xs");
        result.Parent.Should().Be(new NodeId(5));
        result.Children.Should().HaveCount(2);
        result.Attributes.Should().HaveCount(1);
        result.NamespaceDeclarations.Should().HaveCount(1);
    }

    #endregion

    #region Attribute Serialization

    [Fact]
    public void Serialize_Attribute_WritesCorrectKind()
    {
        var attr = CreateAttribute();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(attr, buffer);

        buffer[0].Should().Be((byte)XdmNodeKind.Attribute);
    }

    [Fact]
    public void RoundTrip_SimpleAttribute_PreservesProperties()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "class",
            Value = "container"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(attr, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(attr.Id, attr.Document) as XdmAttribute;

        result.Should().NotBeNull();
        result!.Namespace.Should().Be(NamespaceId.None);
        result.LocalName.Should().Be("class");
        result.Value.Should().Be("container");
        result.Prefix.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_AttributeWithNamespace_PreservesNamespace()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xml,
            LocalName = "lang",
            Prefix = "xml",
            Parent = ParentNodeId,
            Value = "en-US"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(attr, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(attr.Id, attr.Document) as XdmAttribute;

        result!.Namespace.Should().Be(NamespaceId.Xml);
        result.LocalName.Should().Be("lang");
        result.Prefix.Should().Be("xml");
        result.Value.Should().Be("en-US");
    }

    [Fact]
    public void RoundTrip_AttributeWithParent_PreservesParent()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "id",
            Parent = new NodeId(50),
            Value = "test-id"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(attr, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(attr.Id, attr.Document) as XdmAttribute;

        result!.Parent.Should().Be(new NodeId(50));
    }

    [Theory]
    [InlineData("")]
    [InlineData("simple")]
    [InlineData("with spaces and special chars: <>&\"'")]
    [InlineData("unicode: \u00E9\u00E8\u00EA")]
    public void RoundTrip_AttributeValues_PreserveContent(string value)
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "data",
            Value = value
        };
        var buffer = new byte[4096];

        var bytesWritten = NodeSerializer.Serialize(attr, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(attr.Id, attr.Document) as XdmAttribute;

        result!.Value.Should().Be(value);
    }

    #endregion

    #region Text Serialization

    [Fact]
    public void Serialize_Text_WritesCorrectKind()
    {
        var text = CreateText();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(text, buffer);

        buffer[0].Should().Be((byte)XdmNodeKind.Text);
    }

    [Fact]
    public void RoundTrip_Text_PreservesProperties()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "Hello, World!"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(text, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(text.Id, text.Document) as XdmText;

        result.Should().NotBeNull();
        result!.Value.Should().Be("Hello, World!");
        result.Parent.Should().Be(ParentNodeId);
    }

    [Fact]
    public void RoundTrip_TextWithoutParent_WorksCorrectly()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "Orphan text"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(text, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(text.Id, text.Document) as XdmText;

        result!.Parent.Should().BeNull();
        result.Value.Should().Be("Orphan text");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple text")]
    [InlineData("Multi\nline\ntext")]
    [InlineData("   whitespace   ")]
    [InlineData("Special: <>&\"'")]
    [InlineData("\t\r\n")]
    public void RoundTrip_TextVariants_PreserveContent(string value)
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = value
        };
        var buffer = new byte[4096];

        var bytesWritten = NodeSerializer.Serialize(text, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(text.Id, text.Document) as XdmText;

        result!.Value.Should().Be(value);
    }

    #endregion

    #region Comment Serialization

    [Fact]
    public void Serialize_Comment_WritesCorrectKind()
    {
        var comment = CreateComment();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(comment, buffer);

        buffer[0].Should().Be((byte)XdmNodeKind.Comment);
    }

    [Fact]
    public void RoundTrip_Comment_PreservesProperties()
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "This is a comment"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(comment, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(comment.Id, comment.Document) as XdmComment;

        result.Should().NotBeNull();
        result!.Value.Should().Be("This is a comment");
        result.Parent.Should().Be(ParentNodeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Single line")]
    [InlineData("Multi\nline\ncomment")]
    [InlineData(" Surrounding spaces ")]
    public void RoundTrip_CommentVariants_PreserveContent(string value)
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = value
        };
        var buffer = new byte[4096];

        var bytesWritten = NodeSerializer.Serialize(comment, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(comment.Id, comment.Document) as XdmComment;

        result!.Value.Should().Be(value);
    }

    #endregion

    #region Processing Instruction Serialization

    [Fact]
    public void Serialize_ProcessingInstruction_WritesCorrectKind()
    {
        var pi = CreatePI();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(pi, buffer);

        buffer[0].Should().Be((byte)XdmNodeKind.ProcessingInstruction);
    }

    [Fact]
    public void RoundTrip_ProcessingInstruction_PreservesProperties()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Target = "xml-stylesheet",
            Value = "type=\"text/xsl\" href=\"style.xsl\""
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(pi, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(pi.Id, pi.Document) as XdmProcessingInstruction;

        result.Should().NotBeNull();
        result!.Target.Should().Be("xml-stylesheet");
        result.Value.Should().Be("type=\"text/xsl\" href=\"style.xsl\"");
        result.Parent.Should().Be(ParentNodeId);
    }

    [Theory]
    [InlineData("target", "")]
    [InlineData("custom", "data")]
    [InlineData("php", "echo 'Hello';")]
    public void RoundTrip_PIVariants_PreserveContent(string target, string value)
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = target,
            Value = value
        };
        var buffer = new byte[4096];

        var bytesWritten = NodeSerializer.Serialize(pi, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(pi.Id, pi.Document) as XdmProcessingInstruction;

        result!.Target.Should().Be(target);
        result.Value.Should().Be(value);
    }

    #endregion

    #region Namespace Serialization

    [Fact]
    public void Serialize_Namespace_WritesCorrectKind()
    {
        var ns = CreateNamespace();
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(ns, buffer);

        buffer[0].Should().Be((byte)XdmNodeKind.Namespace);
    }

    [Fact]
    public void RoundTrip_Namespace_PreservesProperties()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Prefix = "xs",
            Uri = "http://www.w3.org/2001/XMLSchema"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(ns, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(ns.Id, ns.Document) as XdmNamespace;

        result.Should().NotBeNull();
        result!.Prefix.Should().Be("xs");
        result.Uri.Should().Be("http://www.w3.org/2001/XMLSchema");
        result.Parent.Should().Be(ParentNodeId);
    }

    [Fact]
    public void RoundTrip_DefaultNamespace_PreservesEmptyPrefix()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "",
            Uri = "http://example.com"
        };
        var buffer = new byte[1024];

        var bytesWritten = NodeSerializer.Serialize(ns, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(ns.Id, ns.Document) as XdmNamespace;

        result!.Prefix.Should().BeEmpty();
        result.Uri.Should().Be("http://example.com");
    }

    #endregion

    #region Size Estimation Tests

    [Fact]
    public void EstimateSize_Document_ReturnsPositiveValue()
    {
        var doc = CreateDocument();

        var estimate = NodeSerializer.EstimateSize(doc);

        estimate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateSize_Document_IsSufficientForActualSize()
    {
        var doc = new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = ImmutableArray.Create(new NodeId(1), new NodeId(2), new NodeId(3)),
            DocumentUri = "file:///test.xml",
            DocumentElement = new NodeId(1)
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(doc);
        var actualSize = NodeSerializer.Serialize(doc, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_Element_ReturnsPositiveValue()
    {
        var element = CreateElement();

        var estimate = NodeSerializer.EstimateSize(element);

        estimate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateSize_Element_IsSufficientForActualSize()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xsd,
            LocalName = "complexElement",
            Prefix = "xs",
            Parent = ParentNodeId,
            Children = ImmutableArray.Create(new NodeId(10), new NodeId(11)),
            Attributes = ImmutableArray.Create(new NodeId(20)),
            NamespaceDeclarations = ImmutableArray.Create(
                new NamespaceBinding("xs", NamespaceId.Xsd),
                new NamespaceBinding("xsi", NamespaceId.Xsi))
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(element);
        var actualSize = NodeSerializer.Serialize(element, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_Attribute_IsSufficientForActualSize()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xml,
            LocalName = "longAttributeName",
            Prefix = "xml",
            Parent = ParentNodeId,
            Value = "This is a longer attribute value with various content."
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(attr);
        var actualSize = NodeSerializer.Serialize(attr, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_Text_IsSufficientForActualSize()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "This is some text content that might be quite long."
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(text);
        var actualSize = NodeSerializer.Serialize(text, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_Comment_IsSufficientForActualSize()
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "A comment with some text content"
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(comment);
        var actualSize = NodeSerializer.Serialize(comment, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_PI_IsSufficientForActualSize()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Target = "xml-stylesheet",
            Value = "type=\"text/xsl\" href=\"style.xsl\""
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(pi);
        var actualSize = NodeSerializer.Serialize(pi, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    [Fact]
    public void EstimateSize_Namespace_IsSufficientForActualSize()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Prefix = "longprefix",
            Uri = "http://example.com/very/long/namespace/uri"
        };
        var buffer = new byte[4096];

        var estimate = NodeSerializer.EstimateSize(ns);
        var actualSize = NodeSerializer.Serialize(ns, buffer);

        estimate.Should().BeGreaterThanOrEqualTo(actualSize);
    }

    #endregion

    #region Reader PeekNodeKind Tests

    [Fact]
    public void PeekNodeKind_Document_ReturnsDocument()
    {
        var doc = CreateDocument();
        var buffer = new byte[1024];
        NodeSerializer.Serialize(doc, buffer);

        var reader = new NodeReader(buffer);
        var kind = reader.PeekNodeKind();

        kind.Should().Be(XdmNodeKind.Document);
    }

    [Fact]
    public void PeekNodeKind_Element_ReturnsElement()
    {
        var element = CreateElement();
        var buffer = new byte[1024];
        NodeSerializer.Serialize(element, buffer);

        var reader = new NodeReader(buffer);
        var kind = reader.PeekNodeKind();

        kind.Should().Be(XdmNodeKind.Element);
    }

    [Fact]
    public void PeekNodeKind_DoesNotAdvancePosition()
    {
        var element = CreateElement();
        var buffer = new byte[1024];
        NodeSerializer.Serialize(element, buffer);

        var reader = new NodeReader(buffer);
        _ = reader.PeekNodeKind();
        var positionAfterPeek = reader.Position;

        positionAfterPeek.Should().Be(0);
    }

    #endregion

    #region Reader Position Tests

    [Fact]
    public void Position_AfterRead_ReflectsBytesConsumed()
    {
        var element = CreateElement();
        var buffer = new byte[1024];
        var bytesWritten = NodeSerializer.Serialize(element, buffer);

        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        _ = reader.Read(element.Id, element.Document);

        reader.Position.Should().Be(bytesWritten);
    }

    #endregion

    #region VarInt Encoding Tests

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void RoundTrip_ElementWithLargeNodeIds_PreservesIds(uint nodeIdValue)
    {
        var element = new XdmElement
        {
            Id = new NodeId(nodeIdValue),
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "test",
            Parent = new NodeId(nodeIdValue + 1),
            Children = ImmutableArray.Create(new NodeId(nodeIdValue + 2)),
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };
        var buffer = new byte[4096];

        var bytesWritten = NodeSerializer.Serialize(element, buffer);
        var reader = new NodeReader(buffer.AsSpan(0, bytesWritten));
        var result = reader.Read(element.Id, element.Document) as XdmElement;

        result!.Id.Should().Be(new NodeId(nodeIdValue));
        result.Parent.Should().Be(new NodeId(nodeIdValue + 1));
        result.Children[0].Should().Be(new NodeId(nodeIdValue + 2));
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Read_UnknownNodeKind_ThrowsInvalidDataException()
    {
        var buffer = new byte[] { 255, 0 }; // Unknown node kind
        var reader = new NodeReader(buffer);

        Assert.Throws<InvalidDataException>(() =>
        {
            var r = new NodeReader(buffer);
            r.Read(TestNodeId, TestDocId);
        });
    }

    #endregion

    #region Helper Methods

    private static XdmDocument CreateDocument() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Children = XdmDocument.EmptyChildren
    };

    private static XdmElement CreateElement() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Namespace = NamespaceId.None,
        LocalName = "element",
        Children = XdmElement.EmptyChildren,
        Attributes = XdmElement.EmptyAttributes,
        NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
    };

    private static XdmAttribute CreateAttribute() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Namespace = NamespaceId.None,
        LocalName = "attr",
        Value = "value"
    };

    private static XdmText CreateText() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Value = "text"
    };

    private static XdmComment CreateComment() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Value = "comment"
    };

    private static XdmProcessingInstruction CreatePI() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Target = "target",
        Value = "data"
    };

    private static XdmNamespace CreateNamespace() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Prefix = "ns",
        Uri = "http://example.com"
    };

    #endregion
}
