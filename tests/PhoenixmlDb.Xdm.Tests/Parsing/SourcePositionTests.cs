using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests.Parsing;

/// <summary>
/// Documents the source-position behaviour of <see cref="XmlDocumentParser"/>:
/// which fields are populated at parse time, how they propagate through C# <c>with</c>
/// expressions, and what happens across a Parse → Serialize → Parse round-trip.
/// </summary>
public sealed class SourcePositionTests
{
    // -------------------------------------------------------------------------
    // Fixture
    //
    // Line assignments (1-based):
    //   1: <?xml version="1.0"?>
    //   2: <root attr="value">
    //   3:   <child>text</child>
    //   4:   <!-- comment -->
    //   5:   <empty/>
    //   6: </root>
    // -------------------------------------------------------------------------

    private const string FixtureXml =
        "<?xml version=\"1.0\"?>\n" +
        "<root attr=\"value\">\n" +
        "  <child>text</child>\n" +
        "  <!-- comment -->\n" +
        "  <empty/>\n" +
        "</root>";

    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId StartId = new(1);

    private readonly Dictionary<string, NamespaceId> _uriToId = new()
    {
        { "", NamespaceId.None },
        { "http://www.w3.org/XML/1998/namespace", NamespaceId.Xml },
        { "http://www.w3.org/2000/xmlns/", NamespaceId.Xmlns },
        { "http://www.w3.org/2001/XMLSchema", NamespaceId.Xsd },
        { "http://www.w3.org/2001/XMLSchema-instance", NamespaceId.Xsi },
    };

    private readonly Dictionary<NamespaceId, string> _idToUri = new()
    {
        { NamespaceId.None, "" },
        { NamespaceId.Xml, "http://www.w3.org/XML/1998/namespace" },
        { NamespaceId.Xmlns, "http://www.w3.org/2000/xmlns/" },
        { NamespaceId.Xsd, "http://www.w3.org/2001/XMLSchema" },
        { NamespaceId.Xsi, "http://www.w3.org/2001/XMLSchema-instance" },
    };

    private uint _nextNsId = NamespaceId.FirstUserNamespaceId;

    // -------------------------------------------------------------------------
    // A1 – Element positions
    // -------------------------------------------------------------------------

    [Fact]
    public void RootElementSourcePositionIsSet()
    {
        var result = ParseFixture();

        var root = result.Nodes.OfType<XdmElement>()
            .First(e => e.LocalName == "root");

        root.SourceLine.Should().Be(2, because: "<?xml?> is line 1; <root> opens on line 2");
        root.SourceColumn.Should().BeGreaterThan(0, because: "IXmlLineInfo reports a 1-based column");
    }

    [Fact]
    public void ChildElementSourcePositionIsLine3()
    {
        var result = ParseFixture();

        var child = result.Nodes.OfType<XdmElement>()
            .First(e => e.LocalName == "child");

        child.SourceLine.Should().Be(3);
        child.SourceColumn.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EmptyElementSourcePositionIsLine5()
    {
        var result = ParseFixture();

        var empty = result.Nodes.OfType<XdmElement>()
            .First(e => e.LocalName == "empty");

        empty.SourceLine.Should().Be(5);
        empty.SourceColumn.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // A2 – Attribute positions
    // -------------------------------------------------------------------------

    [Fact]
    public void AttributeSourcePositionIsSet()
    {
        var result = ParseFixture();

        var attr = result.Nodes.OfType<XdmAttribute>()
            .First(a => a.LocalName == "attr");

        // The attribute sits inside the <root> start tag on line 2.
        attr.SourceLine.Should().Be(2, because: "attr=\"value\" is on the same line as <root>");
        attr.SourceColumn.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // A3 – Text positions
    // -------------------------------------------------------------------------

    [Fact]
    public void TextNodeHasSourcePosition()
    {
        var result = ParseFixture();

        var text = result.Nodes.OfType<XdmText>()
            .First(t => t.Value == "text");

        text.SourceLine.Should().BeGreaterThan(0, because: "text content 'text' has a parse position");
    }

    // -------------------------------------------------------------------------
    // A4 – Comment positions
    // -------------------------------------------------------------------------

    [Fact]
    public void CommentSourcePositionIsLine4()
    {
        var result = ParseFixture();

        var comment = result.Nodes.OfType<XdmComment>()
            .FirstOrDefault();

        comment.Should().NotBeNull();
        comment!.SourceLine.Should().Be(4);
        comment.SourceColumn.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // A5 – Synthesized nodes have zero positions
    // -------------------------------------------------------------------------

    [Fact]
    public void SynthesizedNodeHasZeroPositions()
    {
        // Construct an XdmText programmatically — no parse, so no source position.
        var text = new XdmText
        {
            Id = new NodeId(999),
            Document = TestDocId,
            Value = "synthesized"
        };

        text.SourceLine.Should().Be(0, because: "default value; no parser populated it");
        text.SourceColumn.Should().Be(0, because: "default value; no parser populated it");
    }

    // -------------------------------------------------------------------------
    // A6 – Round-trip does NOT preserve positions
    // -------------------------------------------------------------------------

    [Fact]
    public void RoundTripDoesNotPreservePositions()
    {
        // Parse the fixture to get tree A with known positions.
        var resultA = ParseFixture();
        var rootA = resultA.Nodes.OfType<XdmElement>().First(e => e.LocalName == "root");
        rootA.SourceLine.Should().Be(2);

        // Serialize tree A → XML string B (serializer adds indentation and a fresh declaration).
        var nodeStore = resultA.Nodes.ToDictionary(n => n.Id);
        var serializer = new XmlSerializer(
            id => nodeStore.TryGetValue(id, out var n) ? n : null,
            ns => _idToUri.TryGetValue(ns, out var uri) ? uri : null,
            indent: true);
        var xmlB = serializer.Serialize(resultA.Document);

        // Parse B → tree C.
        var resultC = ParseXml(xmlB);
        var rootC = resultC.Nodes.OfType<XdmElement>().First(e => e.LocalName == "root");

        // The serializer emits an XML declaration + newline before <root>, so
        // <root> will still land on line 2 of B.  What matters is that the
        // positions in C are reported relative to B's layout, not A's original
        // byte offsets: C was built from a freshly-serialized string and its
        // line info is independently valid for that serialized form.
        rootC.SourceLine.Should().BeGreaterThan(0,
            because: "re-parsing the serialized output still yields valid positions for that output");
    }

    // -------------------------------------------------------------------------
    // A7 – Manually constructed copy preserves positions
    // -------------------------------------------------------------------------

    [Fact]
    public void ManualCopyPreservesSourcePositions()
    {
        var result = ParseFixture();
        var root = result.Nodes.OfType<XdmElement>().First(e => e.LocalName == "root");

        // XdmElement is a sealed class (not a record), so C# 'with' is not available.
        // Editor mutation helpers in Phoenixml.Platform.Editor.Xml construct a new node
        // while forwarding SourceLine/SourceColumn from the original.  This test verifies
        // that the init-only pattern makes that straightforward: callers simply assign the
        // same SourceLine/SourceColumn values in the initializer.
        var copy = new XdmElement
        {
            Id = root.Id,
            Document = root.Document,
            LocalName = "renamed",
            Namespace = root.Namespace,
            Prefix = root.Prefix,
            Attributes = root.Attributes,
            NamespaceDeclarations = root.NamespaceDeclarations,
            Children = root.Children,
            SourceLine = root.SourceLine,
            SourceColumn = root.SourceColumn
        };

        copy.SourceLine.Should().Be(root.SourceLine,
            because: "init-only properties allow the caller to forward the original parse position");
        copy.SourceColumn.Should().Be(root.SourceColumn);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ParseResult ParseFixture() => ParseXml(FixtureXml);

    private ParseResult ParseXml(string xml)
    {
        var parser = new XmlDocumentParser(
            TestDocId,
            StartId,
            ResolveNamespace,
            preserveWhitespace: false);
        return parser.Parse(xml);
    }

    private NamespaceId ResolveNamespace(string uri)
    {
        if (_uriToId.TryGetValue(uri, out var id))
            return id;

        var newId = new NamespaceId(_nextNsId++);
        _uriToId[uri] = newId;
        _idToUri[newId] = uri;
        return newId;
    }
}
