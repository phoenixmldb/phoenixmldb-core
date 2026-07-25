using System.Linq;
using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

public sealed class XPointerEvaluatorTests
{
    private static XmlDocument Doc(string xml)
    {
        var d = new XmlDocument { PreserveWhitespace = true };
        d.LoadXml(xml);
        return d;
    }

    [Fact]
    public void Shorthand_selects_element_by_xml_id()
    {
        var d = Doc("<r xmlns:xml='http://www.w3.org/XML/1998/namespace'>" +
                    "<a xml:id='p1'>one</a><b xml:id='p2'>two</b></r>");
        var nodes = XPointerEvaluator.Evaluate(d, "p2");
        nodes.Should().ContainSingle();
        nodes[0].Should().BeAssignableTo<XmlElement>();
        ((XmlElement)nodes[0]).InnerText.Should().Be("two");
    }

    [Fact]
    public void Shorthand_no_match_returns_empty()
    {
        var d = Doc("<r><a xml:id='p1'/></r>");
        XPointerEvaluator.Evaluate(d, "nope").Should().BeEmpty();
    }

    [Fact]
    public void Grammar_invalid_pointer_is_fatal()
    {
        var d = Doc("<r/>");
        // Unbalanced parens — not a shorthand (has '('), not a valid scheme part.
        var act = () => XPointerEvaluator.Evaluate(d, "element(/1");
        act.Should().Throw<XIncludeException>()
            .Which.Kind.Should().Be(XIncludeErrorKind.MalformedInclude);
    }

    [Fact]
    public void Unknown_scheme_part_is_skipped_not_fatal()
    {
        var d = Doc("<r><a xml:id='x'/></r>");
        // bogus() is unknown → skipped; whole pointer selects nothing → empty (NOT fatal).
        XPointerEvaluator.Evaluate(d, "bogus(whatever)").Should().BeEmpty();
    }
}
