using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for XmlDocumentParser.
/// </summary>
public class XmlDocumentParserTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId StartNodeId = new(1);

    private readonly Dictionary<string, NamespaceId> _namespaceMap = new()
    {
        { "", NamespaceId.None },
        { "http://www.w3.org/XML/1998/namespace", NamespaceId.Xml },
        { "http://www.w3.org/2000/xmlns/", NamespaceId.Xmlns },
        { "http://www.w3.org/2001/XMLSchema", NamespaceId.Xsd },
        { "http://www.w3.org/2001/XMLSchema-instance", NamespaceId.Xsi }
    };

    private uint _nextNsId = NamespaceId.FirstUserNamespaceId;

    #region Simple Document Parsing

    [Fact]
    public void Parse_SimpleDocument_ReturnsDocumentNode()
    {
        const string xml = "<root/>";
        var result = ParseXml(xml);

        result.Document.Should().NotBeNull();
        result.Document.NodeKind.Should().Be(XdmNodeKind.Document);
        result.NodeCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Parse_SimpleDocument_HasDocumentElement()
    {
        const string xml = "<root/>";
        var result = ParseXml(xml);

        result.Document.DocumentElement.Should().NotBeNull();
    }

    [Fact]
    public void Parse_SimpleDocument_WithDocumentUri_SetsUri()
    {
        const string xml = "<root/>";
        var result = ParseXml(xml, "file:///test.xml");

        result.Document.DocumentUri.Should().Be("file:///test.xml");
        result.Document.BaseUri.Should().Be("file:///test.xml");
    }

    [Fact]
    public void Parse_EmptyElement_CreatesElementNode()
    {
        const string xml = "<root/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().FirstOrDefault();
        element.Should().NotBeNull();
        element!.LocalName.Should().Be("root");
        element.Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ElementWithText_CreatesTextNode()
    {
        const string xml = "<root>Hello</root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().FirstOrDefault();
        text.Should().NotBeNull();
        text!.Value.Should().Be("Hello");
    }

    #endregion

    #region Nested Elements

    [Fact]
    public void Parse_NestedElements_CreatesHierarchy()
    {
        const string xml = "<root><child1/><child2/></root>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        elements.Should().HaveCount(3);

        var root = elements.First(e => e.LocalName == "root");
        root.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_DeeplyNested_CreatesCorrectStructure()
    {
        const string xml = "<a><b><c><d>text</d></c></b></a>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        elements.Should().HaveCount(4);

        var names = elements.Select(e => e.LocalName).ToList();
        names.Should().Contain("a");
        names.Should().Contain("b");
        names.Should().Contain("c");
        names.Should().Contain("d");
    }

    [Fact]
    public void Parse_NestedElements_SetsParentCorrectly()
    {
        const string xml = "<root><child/></root>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        var root = elements.First(e => e.LocalName == "root");
        var child = elements.First(e => e.LocalName == "child");

        child.Parent.Should().Be(root.Id);
    }

    [Fact]
    public void Parse_SiblingElements_HaveSameParent()
    {
        const string xml = "<root><child1/><child2/><child3/></root>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        var root = elements.First(e => e.LocalName == "root");
        var children = elements.Where(e => e.LocalName.StartsWith("child", StringComparison.Ordinal)).ToList();

        foreach (var child in children)
        {
            child.Parent.Should().Be(root.Id);
        }
    }

    #endregion

    #region Attributes

    [Fact]
    public void Parse_ElementWithAttribute_CreatesAttributeNode()
    {
        const string xml = "<root attr=\"value\"/>";
        var result = ParseXml(xml);

        var attr = result.Nodes.OfType<XdmAttribute>().FirstOrDefault();
        attr.Should().NotBeNull();
        attr!.LocalName.Should().Be("attr");
        attr.Value.Should().Be("value");
    }

    [Fact]
    public void Parse_ElementWithMultipleAttributes_CreatesAllAttributeNodes()
    {
        const string xml = "<root a=\"1\" b=\"2\" c=\"3\"/>";
        var result = ParseXml(xml);

        var attrs = result.Nodes.OfType<XdmAttribute>().ToList();
        attrs.Should().HaveCount(3);

        attrs.Select(a => a.LocalName).Should().BeEquivalentTo(["a", "b", "c"]);
        attrs.Select(a => a.Value).Should().BeEquivalentTo(["1", "2", "3"]);
    }

    [Fact]
    public void Parse_ElementWithAttribute_AttributeHasCorrectParent()
    {
        const string xml = "<root attr=\"value\"/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        var attr = result.Nodes.OfType<XdmAttribute>().First();

        attr.Parent.Should().Be(element.Id);
        element.Attributes.Should().Contain(attr.Id);
    }

    [Fact]
    public void Parse_AttributeWithSpecialCharacters_DecodesCorrectly()
    {
        const string xml = "<root attr=\"a &amp; b &lt; c &gt; d &quot;e&quot;\"/>";
        var result = ParseXml(xml);

        var attr = result.Nodes.OfType<XdmAttribute>().First();
        attr.Value.Should().Be("a & b < c > d \"e\"");
    }

    [Theory]
    [InlineData("<root attr=\"\"/>", "")]
    [InlineData("<root attr=\"   \"/>", "   ")]
    [InlineData("<root attr=\"hello world\"/>", "hello world")]
    public void Parse_AttributeValues_PreservesContent(string xml, string expectedValue)
    {
        var result = ParseXml(xml);

        var attr = result.Nodes.OfType<XdmAttribute>().First();
        attr.Value.Should().Be(expectedValue);
    }

    #endregion

    #region Namespaces

    [Fact]
    public void Parse_ElementWithDefaultNamespace_SetsNamespaceId()
    {
        const string xml = "<root xmlns=\"http://example.com\"/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        element.Namespace.Should().NotBe(NamespaceId.None);
    }

    [Fact]
    public void Parse_ElementWithPrefixedNamespace_SetsPrefixAndNamespace()
    {
        const string xml = "<xs:element xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        element.Prefix.Should().Be("xs");
        element.Namespace.Should().Be(NamespaceId.Xsd);
    }

    [Fact]
    public void Parse_NamespaceDeclaration_RecordsBinding()
    {
        const string xml = "<root xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        element.NamespaceDeclarations.Should().HaveCount(1);
        element.NamespaceDeclarations[0].Prefix.Should().Be("xs");
        element.NamespaceDeclarations[0].Namespace.Should().Be(NamespaceId.Xsd);
    }

    [Fact]
    public void Parse_DefaultNamespaceDeclaration_RecordsWithEmptyPrefix()
    {
        const string xml = "<root xmlns=\"http://example.com\"/>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        element.NamespaceDeclarations.Should().HaveCount(1);
        element.NamespaceDeclarations[0].Prefix.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PrefixedAttribute_HasNamespace()
    {
        const string xml = "<root xml:lang=\"en\"/>";
        var result = ParseXml(xml);

        var attr = result.Nodes.OfType<XdmAttribute>().First();
        attr.Prefix.Should().Be("xml");
        attr.LocalName.Should().Be("lang");
        attr.Namespace.Should().Be(NamespaceId.Xml);
    }

    [Fact]
    public void Parse_MultipleNamespaces_HandlesCorrectly()
    {
        const string xml = @"
            <root xmlns=""http://default.com""
                  xmlns:a=""http://a.com""
                  xmlns:b=""http://b.com"">
                <a:child/>
                <b:child/>
            </root>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        elements.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Comments

    [Fact]
    public void Parse_Comment_CreatesCommentNode()
    {
        const string xml = "<root><!-- This is a comment --></root>";
        var result = ParseXml(xml);

        var comment = result.Nodes.OfType<XdmComment>().FirstOrDefault();
        comment.Should().NotBeNull();
        comment!.Value.Should().Be(" This is a comment ");
    }

    [Fact]
    public void Parse_MultipleComments_CreatesAllCommentNodes()
    {
        const string xml = "<root><!--A--><!--B--><!--C--></root>";
        var result = ParseXml(xml);

        var comments = result.Nodes.OfType<XdmComment>().ToList();
        comments.Should().HaveCount(3);
        comments.Select(c => c.Value).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void Parse_CommentAtDocumentLevel_IsDocumentChild()
    {
        const string xml = "<!-- prolog comment --><root/>";
        var result = ParseXml(xml);

        var comment = result.Nodes.OfType<XdmComment>().FirstOrDefault();
        comment.Should().NotBeNull();
        result.Document.Children.Should().Contain(comment!.Id);
    }

    [Fact]
    public void Parse_CommentWithinElement_HasCorrectParent()
    {
        const string xml = "<root><!-- comment --></root>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        var comment = result.Nodes.OfType<XdmComment>().First();

        comment.Parent.Should().Be(element.Id);
        element.Children.Should().Contain(comment.Id);
    }

    #endregion

    #region Processing Instructions

    [Fact]
    public void Parse_ProcessingInstruction_CreatesPI()
    {
        const string xml = "<?xml-stylesheet type=\"text/xsl\" href=\"style.xsl\"?><root/>";
        var result = ParseXml(xml);

        var pi = result.Nodes.OfType<XdmProcessingInstruction>().FirstOrDefault();
        pi.Should().NotBeNull();
        pi!.Target.Should().Be("xml-stylesheet");
        pi.Value.Should().Contain("type=\"text/xsl\"");
    }

    [Fact]
    public void Parse_PIWithinElement_HasCorrectParent()
    {
        const string xml = "<root><?target data?></root>";
        var result = ParseXml(xml);

        var element = result.Nodes.OfType<XdmElement>().First();
        var pi = result.Nodes.OfType<XdmProcessingInstruction>().First();

        pi.Parent.Should().Be(element.Id);
        element.Children.Should().Contain(pi.Id);
    }

    [Fact]
    public void Parse_PIAtDocumentLevel_IsDocumentChild()
    {
        const string xml = "<?target data?><root/>";
        var result = ParseXml(xml);

        var pi = result.Nodes.OfType<XdmProcessingInstruction>().First();
        result.Document.Children.Should().Contain(pi.Id);
    }

    [Theory]
    [InlineData("<?target?>", "target", "")]
    [InlineData("<?custom value?>", "custom", "value")]
    [InlineData("<?name a=\"b\" c=\"d\"?>", "name", "a=\"b\" c=\"d\"")]
    public void Parse_PIVariants_ParseCorrectly(string piContent, string expectedTarget, string expectedValue)
    {
        var xml = $"{piContent}<root/>";
        var result = ParseXml(xml);

        var pi = result.Nodes.OfType<XdmProcessingInstruction>().First();
        pi.Target.Should().Be(expectedTarget);
        pi.Value.Should().Be(expectedValue);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void Parse_WithoutPreserveWhitespace_IgnoresWhitespaceOnlyText()
    {
        const string xml = "<root>   </root>";
        var result = ParseXml(xml, preserveWhitespace: false);

        var texts = result.Nodes.OfType<XdmText>().ToList();
        texts.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithPreserveWhitespace_KeepsWhitespaceOnlyText()
    {
        const string xml = "<root>   </root>";
        var result = ParseXml(xml, preserveWhitespace: true);

        var text = result.Nodes.OfType<XdmText>().FirstOrDefault();
        text.Should().NotBeNull();
        text!.Value.Should().Be("   ");
    }

    [Fact]
    public void Parse_MixedContent_PreservesNonWhitespaceText()
    {
        const string xml = "<root>  Hello  </root>";
        var result = ParseXml(xml, preserveWhitespace: false);

        var text = result.Nodes.OfType<XdmText>().FirstOrDefault();
        text.Should().NotBeNull();
        text!.Value.Should().Be("  Hello  ");
    }

    [Fact]
    public void Parse_NewlinesInContent_PreservesContent()
    {
        const string xml = "<root>Line1\nLine2</root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Contain("\n");
    }

    #endregion

    #region CDATA Sections

    [Fact]
    public void Parse_CDATASection_CreatesTextNode()
    {
        const string xml = "<root><![CDATA[Some <data> here]]></root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().FirstOrDefault();
        text.Should().NotBeNull();
        text!.Value.Should().Be("Some <data> here");
    }

    [Fact]
    public void Parse_CDATAWithSpecialChars_PreservesLiterally()
    {
        const string xml = "<root><![CDATA[<>&\"']]></root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("<>&\"'");
    }

    #endregion

    #region Text Content

    [Fact]
    public void Parse_EntityReferences_DecodeCorrectly()
    {
        const string xml = "<root>&lt;&gt;&amp;&quot;&apos;</root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("<>&\"'");
    }

    [Fact]
    public void Parse_NumericCharacterReferences_DecodeCorrectly()
    {
        const string xml = "<root>&#65;&#x42;&#67;</root>"; // ABC
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("ABC");
    }

    [Fact]
    public void Parse_UnicodeContent_PreservesCorrectly()
    {
        const string xml = "<root>\u00E9\u00E8\u00EA \u4E2D\u6587</root>";
        var result = ParseXml(xml);

        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("\u00E9\u00E8\u00EA \u4E2D\u6587");
    }

    [Fact]
    public void Parse_AdjacentTextNodes_AreSeparate()
    {
        // Text interrupted by comment
        const string xml = "<root>Hello<!-- comment -->World</root>";
        var result = ParseXml(xml);

        var texts = result.Nodes.OfType<XdmText>().ToList();
        texts.Should().HaveCount(2);
        texts.Select(t => t.Value).Should().BeEquivalentTo(["Hello", "World"]);
    }

    #endregion

    #region Complex Documents

    [Fact]
    public void Parse_ComplexDocument_HandlesAllNodeTypes()
    {
        const string xml = @"
            <?xml-stylesheet type=""text/xsl"" href=""style.xsl""?>
            <!-- Header comment -->
            <root xmlns=""http://example.com"" attr=""value"">
                <child1>Text content</child1>
                <!-- Inner comment -->
                <child2 a=""1"" b=""2""/>
                <?custom instruction?>
            </root>";

        var result = ParseXml(xml);

        result.Nodes.OfType<XdmDocument>().Should().HaveCount(1);
        result.Nodes.OfType<XdmElement>().Should().HaveCountGreaterThanOrEqualTo(3);
        result.Nodes.OfType<XdmAttribute>().Should().HaveCountGreaterThanOrEqualTo(3);
        result.Nodes.OfType<XdmText>().Should().HaveCountGreaterThanOrEqualTo(1);
        result.Nodes.OfType<XdmComment>().Should().HaveCount(2);
        result.Nodes.OfType<XdmProcessingInstruction>().Should().HaveCount(2);
    }

    [Fact]
    public void Parse_DocumentOrderIsCorrect()
    {
        const string xml = @"<root><a/><b/><c/></root>";
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        var root = elements.First(e => e.LocalName == "root");

        // Children should be in document order
        var childNames = root.Children
            .Select(id => elements.First(e => e.Id == id))
            .Select(e => e.LocalName)
            .ToList();

        childNames.Should().ContainInOrder("a", "b", "c");
    }

    #endregion

    #region Error Conditions

    [Fact]
    public void Parse_MalformedXml_ThrowsXmlException()
    {
        const string xml = "<root><unclosed>";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_InvalidEntityReference_ThrowsXmlException()
    {
        const string xml = "<root>&invalid;</root>";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_MismatchedTags_ThrowsXmlException()
    {
        const string xml = "<root></wrong>";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_DuplicateAttribute_ThrowsXmlException()
    {
        const string xml = "<root attr=\"1\" attr=\"2\"/>";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_EmptyString_ThrowsXmlException()
    {
        const string xml = "";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Parse_OnlyWhitespace_ThrowsXmlException()
    {
        const string xml = "   ";

        var act = () => ParseXml(xml);

        act.Should().Throw<XmlException>();
    }

    #endregion

    #region Stream and Reader Parsing

    [Fact]
    public void Parse_FromTextReader_WorksCorrectly()
    {
        const string xml = "<root>content</root>";
        using var reader = new StringReader(xml);

        var parser = CreateParser();
        var result = parser.Parse(reader);

        result.Document.Should().NotBeNull();
        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("content");
    }

    [Fact]
    public void Parse_FromStream_WorksCorrectly()
    {
        const string xml = "<root>content</root>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var parser = CreateParser();
        var result = parser.Parse(stream);

        result.Document.Should().NotBeNull();
        var text = result.Nodes.OfType<XdmText>().First();
        text.Value.Should().Be("content");
    }

    #endregion

    #region ParseResult Properties

    [Fact]
    public void ParseResult_NodeCount_MatchesNodesLength()
    {
        const string xml = "<root><child/></root>";
        var result = ParseXml(xml);

        result.NodeCount.Should().Be((uint)result.Nodes.Count);
    }

    [Fact]
    public void ParseResult_Document_IsFirstNode()
    {
        const string xml = "<root/>";
        var result = ParseXml(xml);

        result.Nodes[0].Should().BeOfType<XdmDocument>();
        result.Nodes[0].Should().BeSameAs(result.Document);
    }

    #endregion

    #region Theory Tests with Various XML Documents

    [Theory]
    [InlineData("<root/>", 1)]
    [InlineData("<a><b/></a>", 2)]
    [InlineData("<a><b><c/></b></a>", 3)]
    [InlineData("<a><b/><c/><d/></a>", 4)]
    public void Parse_ElementCount_IsCorrect(string xml, int expectedElementCount)
    {
        var result = ParseXml(xml);

        var elements = result.Nodes.OfType<XdmElement>().ToList();
        elements.Should().HaveCount(expectedElementCount);
    }

    [Theory]
    [InlineData("<e a=\"1\"/>", 1)]
    [InlineData("<e a=\"1\" b=\"2\"/>", 2)]
    [InlineData("<e a=\"1\" b=\"2\" c=\"3\"/>", 3)]
    public void Parse_AttributeCount_IsCorrect(string xml, int expectedAttrCount)
    {
        var result = ParseXml(xml);

        var attrs = result.Nodes.OfType<XdmAttribute>().ToList();
        attrs.Should().HaveCount(expectedAttrCount);
    }

    [Theory]
    [InlineData("<root>text</root>", 1)]
    [InlineData("<root>a<!-- -->b</root>", 2)]
    [InlineData("<root>a<!-- -->b<!-- -->c</root>", 3)]
    public void Parse_TextNodeCount_IsCorrect(string xml, int expectedTextCount)
    {
        var result = ParseXml(xml);

        var texts = result.Nodes.OfType<XdmText>().ToList();
        texts.Should().HaveCount(expectedTextCount);
    }

    #endregion

    #region Helper Methods

    private ParseResult ParseXml(string xml, string? documentUri = null, bool preserveWhitespace = false)
    {
        var parser = CreateParser(preserveWhitespace);
        return parser.Parse(xml, documentUri);
    }

    private XmlDocumentParser CreateParser(bool preserveWhitespace = false)
    {
        return new XmlDocumentParser(
            TestDocId,
            StartNodeId,
            ResolveNamespace,
            preserveWhitespace);
    }

    private NamespaceId ResolveNamespace(string uri)
    {
        if (_namespaceMap.TryGetValue(uri, out var id))
            return id;

        var newId = new NamespaceId(_nextNsId++);
        _namespaceMap[uri] = newId;
        return newId;
    }

    #endregion
}
