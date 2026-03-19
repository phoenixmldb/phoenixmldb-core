using System.Collections.Immutable;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using PhoenixmlDb.Xdm.Nodes;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for XdmDocument node.
/// </summary>
public class XdmDocumentTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(1);

    [Fact]
    public void XdmDocument_NodeKind_ReturnsDocument()
    {
        var doc = CreateDocument();

        doc.NodeKind.Should().Be(XdmNodeKind.Document);
        doc.IsDocument.Should().BeTrue();
    }

    [Fact]
    public void XdmDocument_RequiredProperties_AreSet()
    {
        var children = ImmutableArray.Create(new NodeId(2), new NodeId(3));
        var doc = new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = children,
            DocumentUri = "file:///test.xml",
            DocumentElement = new NodeId(2)
        };

        doc.Id.Should().Be(TestNodeId);
        doc.Document.Should().Be(TestDocId);
        doc.Children.Should().BeEquivalentTo(children);
        doc.DocumentUri.Should().Be("file:///test.xml");
        doc.DocumentElement.Should().Be(new NodeId(2));
    }

    [Fact]
    public void XdmDocument_Parent_IsNull()
    {
        var doc = CreateDocument();

        doc.Parent.Should().BeNull();
    }

    [Fact]
    public void XdmDocument_StringValue_ReturnsEmptyByDefault()
    {
        var doc = CreateDocument();

        doc.StringValue.Should().BeEmpty();
    }

    [Fact]
    public void XdmDocument_TypedValue_ReturnsUntypedAtomic()
    {
        var doc = CreateDocument();

        doc.TypedValue.Type.Should().Be(XdmType.UntypedAtomic);
    }

    [Fact]
    public void XdmDocument_NodeName_ReturnsNull()
    {
        var doc = CreateDocument();

        doc.NodeName.Should().BeNull();
    }

    [Fact]
    public void XdmDocument_BaseUri_ReturnsDocumentUri()
    {
        var doc = new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = XdmDocument.EmptyChildren,
            DocumentUri = "file:///test.xml"
        };

        doc.BaseUri.Should().Be("file:///test.xml");
    }

    [Fact]
    public void XdmDocument_EmptyChildren_ReturnsEmptyList()
    {
        XdmDocument.EmptyChildren.Should().BeEmpty();
    }

    [Fact]
    public void XdmDocument_IsHelpers_ReturnCorrectValues()
    {
        var doc = CreateDocument();

        doc.IsDocument.Should().BeTrue();
        doc.IsElement.Should().BeFalse();
        doc.IsAttribute.Should().BeFalse();
        doc.IsText.Should().BeFalse();
        doc.IsComment.Should().BeFalse();
        doc.IsProcessingInstruction.Should().BeFalse();
        doc.IsNamespace.Should().BeFalse();
    }

    [Fact]
    public void XdmDocument_Is_ReturnsCorrectForNodeKind()
    {
        var doc = CreateDocument();

        doc.Is(XdmNodeKind.Document).Should().BeTrue();
        doc.Is(XdmNodeKind.Element).Should().BeFalse();
    }

    private static XdmDocument CreateDocument() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Children = XdmDocument.EmptyChildren
    };
}

/// <summary>
/// Tests for XdmElement node.
/// </summary>
public class XdmElementTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(1);
    private static readonly NodeId ParentNodeId = new(0);

    [Fact]
    public void XdmElement_NodeKind_ReturnsElement()
    {
        var element = CreateElement();

        element.NodeKind.Should().Be(XdmNodeKind.Element);
        element.IsElement.Should().BeTrue();
    }

    [Fact]
    public void XdmElement_RequiredProperties_AreSet()
    {
        var children = ImmutableArray.Create(new NodeId(2));
        var attributes = ImmutableArray.Create(new NodeId(3));
        var nsDecls = ImmutableArray.Create(new NamespaceBinding("xs", NamespaceId.Xsd));

        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xsd,
            LocalName = "element",
            Prefix = "xs",
            Parent = ParentNodeId,
            Children = children,
            Attributes = attributes,
            NamespaceDeclarations = nsDecls
        };

        element.Id.Should().Be(TestNodeId);
        element.Document.Should().Be(TestDocId);
        element.Namespace.Should().Be(NamespaceId.Xsd);
        element.LocalName.Should().Be("element");
        element.Prefix.Should().Be("xs");
        element.Parent.Should().Be(ParentNodeId);
        element.Children.Should().BeEquivalentTo(children);
        element.Attributes.Should().BeEquivalentTo(attributes);
        element.NamespaceDeclarations.Should().BeEquivalentTo(nsDecls);
    }

    [Fact]
    public void XdmElement_NodeName_ReturnsQName()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xsd,
            LocalName = "element",
            Prefix = "xs",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };

        var nodeName = element.NodeName;
        nodeName.Should().NotBeNull();
        nodeName!.Value.Namespace.Should().Be(NamespaceId.Xsd);
        nodeName.Value.LocalName.Should().Be("element");
        nodeName.Value.Prefix.Should().Be("xs");
        nodeName.Value.PrefixedName.Should().Be("xs:element");
    }

    [Fact]
    public void XdmElement_NodeName_WithoutPrefix_ReturnsLocalNameOnly()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "div",
            Prefix = null,
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        };

        element.NodeName!.Value.PrefixedName.Should().Be("div");
    }

    [Fact]
    public void XdmElement_StringValue_ReturnsEmptyByDefault()
    {
        var element = CreateElement();

        element.StringValue.Should().BeEmpty();
    }

    [Fact]
    public void XdmElement_TypedValue_ReturnsUntypedAtomic()
    {
        var element = CreateElement();

        element.TypedValue.Type.Should().Be(XdmType.UntypedAtomic);
    }

    [Fact]
    public void XdmElement_TypeAnnotation_DefaultsToUntyped()
    {
        var element = CreateElement();

        element.TypeAnnotation.Should().Be(XdmTypeName.Untyped);
    }

    [Fact]
    public void XdmElement_TypeAnnotation_CanBeSet()
    {
        var element = new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "element",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations,
            TypeAnnotation = XdmTypeName.XsString
        };

        element.TypeAnnotation.Should().Be(XdmTypeName.XsString);
    }

    [Fact]
    public void XdmElement_EmptyHelpers_ReturnEmptyCollections()
    {
        XdmElement.EmptyChildren.Should().BeEmpty();
        XdmElement.EmptyAttributes.Should().BeEmpty();
        XdmElement.EmptyNamespaceDeclarations.Should().BeEmpty();
    }

    [Fact]
    public void XdmElement_IsHelpers_ReturnCorrectValues()
    {
        var element = CreateElement();

        element.IsDocument.Should().BeFalse();
        element.IsElement.Should().BeTrue();
        element.IsAttribute.Should().BeFalse();
        element.IsText.Should().BeFalse();
    }

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
}

/// <summary>
/// Tests for XdmAttribute node.
/// </summary>
public class XdmAttributeTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(2);
    private static readonly NodeId ParentNodeId = new(1);

    [Fact]
    public void XdmAttribute_NodeKind_ReturnsAttribute()
    {
        var attr = CreateAttribute();

        attr.NodeKind.Should().Be(XdmNodeKind.Attribute);
        attr.IsAttribute.Should().BeTrue();
    }

    [Fact]
    public void XdmAttribute_RequiredProperties_AreSet()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xml,
            LocalName = "lang",
            Prefix = "xml",
            Parent = ParentNodeId,
            Value = "en"
        };

        attr.Id.Should().Be(TestNodeId);
        attr.Document.Should().Be(TestDocId);
        attr.Namespace.Should().Be(NamespaceId.Xml);
        attr.LocalName.Should().Be("lang");
        attr.Prefix.Should().Be("xml");
        attr.Parent.Should().Be(ParentNodeId);
        attr.Value.Should().Be("en");
    }

    [Fact]
    public void XdmAttribute_NodeName_ReturnsQName()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.Xml,
            LocalName = "lang",
            Prefix = "xml",
            Value = "en"
        };

        var nodeName = attr.NodeName;
        nodeName.Should().NotBeNull();
        nodeName!.Value.Namespace.Should().Be(NamespaceId.Xml);
        nodeName.Value.LocalName.Should().Be("lang");
        nodeName.Value.Prefix.Should().Be("xml");
        nodeName.Value.PrefixedName.Should().Be("xml:lang");
    }

    [Fact]
    public void XdmAttribute_StringValue_ReturnsValue()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "id",
            Value = "my-id-123"
        };

        attr.StringValue.Should().Be("my-id-123");
    }

    [Fact]
    public void XdmAttribute_TypedValue_ReturnsUntypedAtomicWithValue()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "count",
            Value = "42"
        };

        attr.TypedValue.Type.Should().Be(XdmType.UntypedAtomic);
        attr.TypedValue.AsString().Should().Be("42");
    }

    [Fact]
    public void XdmAttribute_TypeAnnotation_DefaultsToUntypedAtomic()
    {
        var attr = CreateAttribute();

        attr.TypeAnnotation.Should().Be(XdmTypeName.UntypedAtomic);
    }

    [Fact]
    public void XdmAttribute_TypeAnnotation_CanBeSet()
    {
        var attr = new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "count",
            Value = "42",
            TypeAnnotation = XdmTypeName.XsInteger
        };

        attr.TypeAnnotation.Should().Be(XdmTypeName.XsInteger);
    }

    [Fact]
    public void XdmAttribute_IsHelpers_ReturnCorrectValues()
    {
        var attr = CreateAttribute();

        attr.IsDocument.Should().BeFalse();
        attr.IsElement.Should().BeFalse();
        attr.IsAttribute.Should().BeTrue();
        attr.IsText.Should().BeFalse();
    }

    private static XdmAttribute CreateAttribute() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Namespace = NamespaceId.None,
        LocalName = "class",
        Value = "test"
    };
}

/// <summary>
/// Tests for XdmText node.
/// </summary>
public class XdmTextTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(3);
    private static readonly NodeId ParentNodeId = new(1);

    [Fact]
    public void XdmText_NodeKind_ReturnsText()
    {
        var text = CreateText();

        text.NodeKind.Should().Be(XdmNodeKind.Text);
        text.IsText.Should().BeTrue();
    }

    [Fact]
    public void XdmText_RequiredProperties_AreSet()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "Hello, World!"
        };

        text.Id.Should().Be(TestNodeId);
        text.Document.Should().Be(TestDocId);
        text.Parent.Should().Be(ParentNodeId);
        text.Value.Should().Be("Hello, World!");
    }

    [Fact]
    public void XdmText_StringValue_ReturnsValue()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "Some text content"
        };

        text.StringValue.Should().Be("Some text content");
    }

    [Fact]
    public void XdmText_TypedValue_ReturnsUntypedAtomicWithValue()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "text value"
        };

        text.TypedValue.Type.Should().Be(XdmType.UntypedAtomic);
        text.TypedValue.AsString().Should().Be("text value");
    }

    [Fact]
    public void XdmText_NodeName_ReturnsNull()
    {
        var text = CreateText();

        text.NodeName.Should().BeNull();
    }

    [Fact]
    public void XdmText_IsHelpers_ReturnCorrectValues()
    {
        var text = CreateText();

        text.IsDocument.Should().BeFalse();
        text.IsElement.Should().BeFalse();
        text.IsAttribute.Should().BeFalse();
        text.IsText.Should().BeTrue();
        text.IsComment.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Simple text")]
    [InlineData("Text with\nnewlines")]
    [InlineData("Unicode: \u00E9\u00E8\u00EA")]
    [InlineData("Special: <>&\"'")]
    public void XdmText_Value_StoresVariousContent(string content)
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = content
        };

        text.Value.Should().Be(content);
        text.StringValue.Should().Be(content);
    }

    private static XdmText CreateText() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Value = "test"
    };
}

/// <summary>
/// Tests for XdmComment node.
/// </summary>
public class XdmCommentTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(4);
    private static readonly NodeId ParentNodeId = new(1);

    [Fact]
    public void XdmComment_NodeKind_ReturnsComment()
    {
        var comment = CreateComment();

        comment.NodeKind.Should().Be(XdmNodeKind.Comment);
        comment.IsComment.Should().BeTrue();
    }

    [Fact]
    public void XdmComment_RequiredProperties_AreSet()
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Value = "This is a comment"
        };

        comment.Id.Should().Be(TestNodeId);
        comment.Document.Should().Be(TestDocId);
        comment.Parent.Should().Be(ParentNodeId);
        comment.Value.Should().Be("This is a comment");
    }

    [Fact]
    public void XdmComment_StringValue_ReturnsValue()
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "Comment content"
        };

        comment.StringValue.Should().Be("Comment content");
    }

    [Fact]
    public void XdmComment_TypedValue_ReturnsStringWithValue()
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "comment text"
        };

        comment.TypedValue.Type.Should().Be(XdmType.XsString);
        comment.TypedValue.AsString().Should().Be("comment text");
    }

    [Fact]
    public void XdmComment_NodeName_ReturnsNull()
    {
        var comment = CreateComment();

        comment.NodeName.Should().BeNull();
    }

    [Fact]
    public void XdmComment_IsHelpers_ReturnCorrectValues()
    {
        var comment = CreateComment();

        comment.IsDocument.Should().BeFalse();
        comment.IsElement.Should().BeFalse();
        comment.IsAttribute.Should().BeFalse();
        comment.IsText.Should().BeFalse();
        comment.IsComment.Should().BeTrue();
        comment.IsProcessingInstruction.Should().BeFalse();
    }

    [Theory]
    [InlineData("Simple comment")]
    [InlineData("Multi\nline\ncomment")]
    [InlineData(" Comment with leading/trailing spaces ")]
    public void XdmComment_Value_StoresVariousContent(string content)
    {
        var comment = new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = content
        };

        comment.Value.Should().Be(content);
    }

    private static XdmComment CreateComment() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Value = "test comment"
    };
}

/// <summary>
/// Tests for XdmProcessingInstruction node.
/// </summary>
public class XdmProcessingInstructionTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(5);
    private static readonly NodeId ParentNodeId = new(0);

    [Fact]
    public void XdmProcessingInstruction_NodeKind_ReturnsProcessingInstruction()
    {
        var pi = CreatePI();

        pi.NodeKind.Should().Be(XdmNodeKind.ProcessingInstruction);
        pi.IsProcessingInstruction.Should().BeTrue();
    }

    [Fact]
    public void XdmProcessingInstruction_RequiredProperties_AreSet()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Target = "xml-stylesheet",
            Value = "type=\"text/xsl\" href=\"style.xsl\""
        };

        pi.Id.Should().Be(TestNodeId);
        pi.Document.Should().Be(TestDocId);
        pi.Parent.Should().Be(ParentNodeId);
        pi.Target.Should().Be("xml-stylesheet");
        pi.Value.Should().Be("type=\"text/xsl\" href=\"style.xsl\"");
    }

    [Fact]
    public void XdmProcessingInstruction_NodeName_ReturnsQNameWithTarget()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = "my-target",
            Value = "data"
        };

        var nodeName = pi.NodeName;
        nodeName.Should().NotBeNull();
        nodeName!.Value.Namespace.Should().Be(NamespaceId.None);
        nodeName!.Value.LocalName.Should().Be("my-target");
    }

    [Fact]
    public void XdmProcessingInstruction_StringValue_ReturnsValue()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = "target",
            Value = "instruction content"
        };

        pi.StringValue.Should().Be("instruction content");
    }

    [Fact]
    public void XdmProcessingInstruction_TypedValue_ReturnsStringWithValue()
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = "target",
            Value = "pi data"
        };

        pi.TypedValue.Type.Should().Be(XdmType.XsString);
        pi.TypedValue.AsString().Should().Be("pi data");
    }

    [Fact]
    public void XdmProcessingInstruction_IsHelpers_ReturnCorrectValues()
    {
        var pi = CreatePI();

        pi.IsDocument.Should().BeFalse();
        pi.IsElement.Should().BeFalse();
        pi.IsAttribute.Should().BeFalse();
        pi.IsText.Should().BeFalse();
        pi.IsComment.Should().BeFalse();
        pi.IsProcessingInstruction.Should().BeTrue();
        pi.IsNamespace.Should().BeFalse();
    }

    [Theory]
    [InlineData("xml-stylesheet", "type=\"text/css\" href=\"style.css\"")]
    [InlineData("php", "echo 'Hello';")]
    [InlineData("custom", "")]
    public void XdmProcessingInstruction_TargetAndValue_StoreCorrectly(string target, string value)
    {
        var pi = new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = target,
            Value = value
        };

        pi.Target.Should().Be(target);
        pi.Value.Should().Be(value);
    }

    private static XdmProcessingInstruction CreatePI() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Target = "test",
        Value = "data"
    };
}

/// <summary>
/// Tests for XdmNamespace node.
/// </summary>
public class XdmNamespaceTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(6);
    private static readonly NodeId ParentNodeId = new(1);

    [Fact]
    public void XdmNamespace_NodeKind_ReturnsNamespace()
    {
        var ns = CreateNamespace();

        ns.NodeKind.Should().Be(XdmNodeKind.Namespace);
        ns.IsNamespace.Should().BeTrue();
    }

    [Fact]
    public void XdmNamespace_RequiredProperties_AreSet()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Parent = ParentNodeId,
            Prefix = "xs",
            Uri = "http://www.w3.org/2001/XMLSchema"
        };

        ns.Id.Should().Be(TestNodeId);
        ns.Document.Should().Be(TestDocId);
        ns.Parent.Should().Be(ParentNodeId);
        ns.Prefix.Should().Be("xs");
        ns.Uri.Should().Be("http://www.w3.org/2001/XMLSchema");
    }

    [Fact]
    public void XdmNamespace_DefaultNamespace_HasEmptyPrefix()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "",
            Uri = "http://example.com/ns"
        };

        ns.Prefix.Should().BeEmpty();
    }

    [Fact]
    public void XdmNamespace_NodeName_ReturnsQNameWithPrefix()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "xs",
            Uri = "http://www.w3.org/2001/XMLSchema"
        };

        var nodeName = ns.NodeName;
        nodeName.Should().NotBeNull();
        nodeName!.Value.Namespace.Should().Be(NamespaceId.None);
        nodeName!.Value.LocalName.Should().Be("xs");
    }

    [Fact]
    public void XdmNamespace_StringValue_ReturnsUri()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "xs",
            Uri = "http://www.w3.org/2001/XMLSchema"
        };

        ns.StringValue.Should().Be("http://www.w3.org/2001/XMLSchema");
    }

    [Fact]
    public void XdmNamespace_TypedValue_ReturnsStringWithUri()
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "xs",
            Uri = "http://www.w3.org/2001/XMLSchema"
        };

        ns.TypedValue.Type.Should().Be(XdmType.XsString);
        ns.TypedValue.AsString().Should().Be("http://www.w3.org/2001/XMLSchema");
    }

    [Fact]
    public void XdmNamespace_IsHelpers_ReturnCorrectValues()
    {
        var ns = CreateNamespace();

        ns.IsDocument.Should().BeFalse();
        ns.IsElement.Should().BeFalse();
        ns.IsAttribute.Should().BeFalse();
        ns.IsText.Should().BeFalse();
        ns.IsComment.Should().BeFalse();
        ns.IsProcessingInstruction.Should().BeFalse();
        ns.IsNamespace.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "http://example.com/default")]
    [InlineData("html", "http://www.w3.org/1999/xhtml")]
    [InlineData("svg", "http://www.w3.org/2000/svg")]
    [InlineData("xlink", "http://www.w3.org/1999/xlink")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "XdmNamespace.Uri is a string property")]
    public void XdmNamespace_PrefixAndUri_StoreCorrectly(string prefix, string namespaceUri)
    {
        var ns = new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = prefix,
            Uri = namespaceUri
        };

        ns.Prefix.Should().Be(prefix);
        ns.Uri.Should().Be(namespaceUri);
    }

    private static XdmNamespace CreateNamespace() => new()
    {
        Id = TestNodeId,
        Document = TestDocId,
        Prefix = "test",
        Uri = "http://test.com"
    };
}

/// <summary>
/// Tests for XdmNode base class behavior.
/// </summary>
public class XdmNodeBaseTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId TestNodeId = new(1);

    [Theory]
    [InlineData(XdmNodeKind.Document)]
    [InlineData(XdmNodeKind.Element)]
    [InlineData(XdmNodeKind.Attribute)]
    [InlineData(XdmNodeKind.Text)]
    [InlineData(XdmNodeKind.Comment)]
    [InlineData(XdmNodeKind.ProcessingInstruction)]
    [InlineData(XdmNodeKind.Namespace)]
    public void Is_ForMatchingKind_ReturnsTrue(XdmNodeKind kind)
    {
        var node = CreateNodeOfKind(kind);

        node.Is(kind).Should().BeTrue();
    }

    [Theory]
    [InlineData(XdmNodeKind.Document, XdmNodeKind.Element)]
    [InlineData(XdmNodeKind.Element, XdmNodeKind.Document)]
    [InlineData(XdmNodeKind.Attribute, XdmNodeKind.Text)]
    [InlineData(XdmNodeKind.Text, XdmNodeKind.Comment)]
    public void Is_ForNonMatchingKind_ReturnsFalse(XdmNodeKind actualKind, XdmNodeKind testKind)
    {
        var node = CreateNodeOfKind(actualKind);

        node.Is(testKind).Should().BeFalse();
    }

    [Fact]
    public void BaseUri_Default_ReturnsNull()
    {
        var text = new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "test"
        };

        text.BaseUri.Should().BeNull();
    }

    private static XdmNode CreateNodeOfKind(XdmNodeKind kind) => kind switch
    {
        XdmNodeKind.Document => new XdmDocument
        {
            Id = TestNodeId,
            Document = TestDocId,
            Children = XdmDocument.EmptyChildren
        },
        XdmNodeKind.Element => new XdmElement
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "element",
            Children = XdmElement.EmptyChildren,
            Attributes = XdmElement.EmptyAttributes,
            NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
        },
        XdmNodeKind.Attribute => new XdmAttribute
        {
            Id = TestNodeId,
            Document = TestDocId,
            Namespace = NamespaceId.None,
            LocalName = "attr",
            Value = "value"
        },
        XdmNodeKind.Text => new XdmText
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "text"
        },
        XdmNodeKind.Comment => new XdmComment
        {
            Id = TestNodeId,
            Document = TestDocId,
            Value = "comment"
        },
        XdmNodeKind.ProcessingInstruction => new XdmProcessingInstruction
        {
            Id = TestNodeId,
            Document = TestDocId,
            Target = "target",
            Value = "data"
        },
        XdmNodeKind.Namespace => new XdmNamespace
        {
            Id = TestNodeId,
            Document = TestDocId,
            Prefix = "ns",
            Uri = "http://example.com"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
