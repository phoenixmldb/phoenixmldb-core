using System.Text;
using AwesomeAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// phoenixml#130. With preserveWhitespace=false the parser strips a whitespace-only text node only
/// in element-only content (indentation between elements). It keeps whitespace in mixed content
/// (an element that also has a non-whitespace text child) and under xml:space="preserve" until an
/// inner xml:space="default" (XML 1.0 §2.10). XmlReaderSettings.IgnoreWhitespace dropped every
/// whitespace-only span, so &lt;p&gt;a &lt;b&gt;b&lt;/b&gt; &lt;i&gt;c&lt;/i&gt;&lt;/p&gt; read back
/// as "a bc". With preserveWhitespace=true everything is kept, as before.
/// </summary>
public sealed class XmlDocumentParserWhitespaceTests
{
    private static readonly NodeId Start = new(100);

    private static ParseResult Parse(string xml, bool preserve = false, string source = "string")
    {
        var parser = new XmlDocumentParser(new DocumentId(1), Start, static _ => NamespaceId.None, preserve);
        switch (source)
        {
            case "text-reader":
                using (var reader = new StringReader(xml))
                    return parser.Parse(reader);
            case "seekable-stream":
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                    return parser.Parse(stream);
            case "forward-only-stream":
                using (var stream = new ForwardOnly(Encoding.UTF8.GetBytes(xml)))
                    return parser.Parse(stream);
            default:
                return parser.Parse(xml);
        }
    }

    private static XdmElement Element(ParseResult r, string name)
        => r.Nodes.OfType<XdmElement>().First(e => e.LocalName == name);

    /// <summary>The text children of an element, each in brackets, in order.</summary>
    private static string Texts(ParseResult r, string name)
    {
        var byId = r.Nodes.ToDictionary(n => n.Id);
        return string.Concat(Element(r, name).Children.Select(id => byId[id]).OfType<XdmText>().Select(t => $"[{t.Value}]"));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("text-reader")]
    [InlineData("seekable-stream")]
    [InlineData("forward-only-stream")]
    public void Mixed_content_keeps_its_whitespace_only_text_nodes(string source)
    {
        var r = Parse("<p>a <b>b</b> <i>c</i></p>", source: source);

        Element(r, "p").StringValue.Should().Be("a b c");
        Texts(r, "p").Should().Be("[a ][ ]");
    }

    [Fact]
    public void A_whitespace_node_before_the_first_non_whitespace_text_is_kept_in_mixed_content()
        => Parse("<p><b>b</b> <i>c</i> and</p>").Nodes.OfType<XdmElement>().First(e => e.LocalName == "p")
            .StringValue.Should().Be("b c and");

    [Theory]
    [InlineData("string")]
    [InlineData("forward-only-stream")]
    public void Indentation_in_element_only_content_is_stripped(string source)
    {
        var r = Parse("<root>\n  <a>x</a>\n  <b/>\n</root>", source: source);

        Texts(r, "root").Should().BeEmpty();
        Element(r, "root").StringValue.Should().Be("x");
    }

    [Fact]
    public void An_element_with_only_whitespace_has_no_text_node()
        => Texts(Parse("<root><e>   </e></root>"), "e").Should().BeEmpty();

    [Fact]
    public void Xml_space_preserve_keeps_whitespace_until_an_inner_xml_space_default()
    {
        var r = Parse("""
            <root>
              <pre xml:space="preserve"> <x/> <in xml:space="default"> <y/> </in> <z/> </pre>
            </root>
            """);

        Texts(r, "pre").Should().Be("[ ][ ][ ][ ]");
        Texts(r, "in").Should().BeEmpty();
        Texts(r, "root").Should().BeEmpty();
    }

    [Fact]
    public void A_whitespace_only_CDATA_section_is_kept()
        => Texts(Parse("<root><a/><![CDATA[  ]]><b/></root>"), "root").Should().Be("[  ]");

    [Fact]
    public void Whitespace_from_character_and_entity_references_is_treated_as_whitespace()
    {
        var doctype = """<!DOCTYPE root [ <!ENTITY sp " "> ]>""";

        Texts(Parse(doctype + "<root><a/>&sp;&#32;<b/></root>"), "root").Should().BeEmpty("element-only content");
        Texts(Parse(doctype + "<root>t<a/>&sp;&#10;<b/></root>"), "root").Should().Be("[t][ \n]", "mixed content");
    }

    [Fact]
    public void PreserveWhitespace_true_keeps_every_whitespace_node()
    {
        var r = Parse("<root>\n  <a>x</a>\n</root>", preserve: true);

        Texts(r, "root").Should().Be("[\n  ][\n]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Node_ids_are_contiguous_and_in_document_order(bool preserve)
    {
        var r = Parse("<root>\n  <p>a <b>b</b> <i>c</i></p>\n  <q> <r/> </q>\n</root>", preserve);

        var ids = r.Nodes.Select(n => n.Id.Value).OrderBy(v => v).ToList();
        ids.Should().Equal(Enumerable.Range((int)Start.Value, (int)r.NodeCount).Select(v => (ulong)v));
        // Document order: in each element, children have increasing ids, after the element's own.
        foreach (var e in r.Nodes.OfType<XdmElement>())
        {
            var children = e.Children.Select(c => c.Value).ToList();
            children.Should().BeInAscendingOrder();
            children.Should().OnlyContain(c => c > e.Id.Value);
        }
    }

    [Fact]
    public void A_parser_parses_a_second_document_with_its_own_element_numbering()
    {
        var parser = new XmlDocumentParser(new DocumentId(1), Start, static _ => NamespaceId.None);
        parser.Parse("<a><b/> <c/></a>");
        var second = parser.Parse("<p>x <b/> <c/></p>");

        second.Nodes.OfType<XdmElement>().First(e => e.LocalName == "p").StringValue.Should().Be("x  ");
    }

    private sealed class ForwardOnly(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Position { get => base.Position; set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
