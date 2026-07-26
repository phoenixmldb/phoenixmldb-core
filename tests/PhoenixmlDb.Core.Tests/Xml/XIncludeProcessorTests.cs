using System;
using System.IO;
using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

/// <summary>
/// Tests for <see cref="XIncludeProcessor"/>: <c>parse="xml"</c> + <c>href</c> inclusion,
/// <c>parse="text"</c> textual inclusion (SP2), recursion into included content, cyclic/depth
/// fatal guards, and XPointer sub-resource selection (SP3).
/// </summary>
public sealed class XIncludeProcessorTests : IDisposable
{
    private const string XiNs = "http://www.w3.org/2001/XInclude";

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"xi-proc-{Guid.NewGuid():N}");

    public XIncludeProcessorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    private string Write(string relativeName, string content)
    {
        var path = Path.Combine(_dir, relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static XmlDocument LoadMaster(string content)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(content);
        return doc;
    }

    private Uri BaseFor(string relativeName) =>
        new Uri(Path.Combine(_dir, relativeName));

    [Fact]
    public void Include_splices_target_element_in_place()
    {
        // Master modelled on baseuri052.xml: <doc><chap>..<xi:include/>..</chap></doc>.
        Write("a.xml", "<item>two</item>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            "<doc><chap><para>one</para>" +
            $"<xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></chap></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectSingleNode("//item[.='two']").Should().NotBeNull();
        // The xi:include element itself must be gone.
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Recursive_include_expands_nested()
    {
        // a.xml itself xi:includes b.xml — expansion must recurse.
        Write("a.xml",
            $"<wrap><xi:include href=\"b.xml\" xmlns:xi=\"{XiNs}\"/></wrap>");
        Write("b.xml", "<leaf>deep</leaf>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><chap><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></chap></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectSingleNode("//wrap/leaf[.='deep']").Should().NotBeNull();
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Cyclic_include_is_fatal()
    {
        // a.xml includes a.xml → active-inclusion stack must detect the cycle.
        Write("a.xml",
            $"<wrap><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></wrap>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></doc>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.IsFatal.Should().BeTrue();
    }

    [Fact]
    public void Parse_text_splices_text_node()
    {
        Write("t.txt", "a < b & c");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"t.txt\" parse=\"text\"/></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.DocumentElement!.InnerText.Should().Be("a < b & c");
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Parse_text_read_failure_uses_fallback()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"gone.txt\" parse=\"text\">" +
            "<xi:fallback>DEFAULT</xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.DocumentElement!.InnerText.Should().Contain("DEFAULT");
    }

    [Fact]
    public void Parse_text_blocked_remote_is_fatal_not_recovered_by_fallback()
    {
        // A blocked remote fetch (AllowRemote=false) is a FATAL error and must NOT be swallowed
        // by an xi:fallback — otherwise a security-relevant block would be silently masked.
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"http://example.com/x.txt\" parse=\"text\">" +
            "<xi:fallback>SHOULD-NOT-APPEAR</xi:fallback></xi:include></m>");

        // Must throw fatally: were the fatal-rethrow filter dropped, the blocked remote would be
        // routed to RecoverWithFallback (fallback content spliced) and no exception would surface.
        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.IsFatal.Should().BeTrue();
    }

    [Fact]
    public void Xml_base_on_include_element_resolves_href_against_it()
    {
        // The real target is at <dir>/sub/a.xml — NOT at <dir>/a.xml. The xi:include element
        // carries its own xml:base="sub/" which must be folded into the in-scope base (on top
        // of the master document's own base) before href="a.xml" is resolved.
        Write("sub/a.xml", "<item>nested</item>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            "<doc><xi:include xml:base=\"sub/\" href=\"a.xml\" " +
            $"xmlns:xi=\"{XiNs}\"/></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectSingleNode("//item[.='nested']").Should().NotBeNull();
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Stamped_xml_base_serializes_and_reparses_well_formed()
    {
        // The §4.5 fixup stamps xml:base on the included element. It must use the reserved "xml"
        // prefix so the expanded DOM round-trips through OuterXml (a consumer that serializes the
        // result — e.g. the XQuery fn:doc bridge — would otherwise hit an illegal xmlns
        // redeclaration for the XML namespace).
        Write("a.xml", "<item>two</item>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        // The stamped attribute is xml:base with the canonical prefix.
        var item = (XmlElement)result.SelectSingleNode("//item")!;
        item.GetAttribute("base", "http://www.w3.org/XML/1998/namespace").Should().EndWith("a.xml");
        // Round-trip: OuterXml must be reparsable (no bogus xmlns:…=xml-namespace declaration).
        var roundTrip = new XmlDocument { PreserveWhitespace = true };
        Action reparse = () => roundTrip.LoadXml(result.OuterXml);
        reparse.Should().NotThrow();
        roundTrip.OuterXml.Should().Contain("xml:base=");
    }

    [Fact]
    public void Fragment_in_href_is_fatal()
    {
        // Deliberately also create a file literally named "a.xml#foo" (legal on this
        // filesystem). This closes off a false-negative: System.Uri's combining behavior for
        // "file" URIs whose base came from a bare path string (as BaseFor below produces)
        // silently folds an unstripped '#' into the path instead of splitting it off as a
        // fragment — so pre-fix, resolution would land on THIS file and splice its ("wrong")
        // content without ever throwing, rather than failing with a coincidental
        // file-not-found error that would make the test pass for the wrong reason.
        Write("a.xml", "<item>right</item>");
        Write("a.xml#foo", "<item>wrong</item>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"a.xml#foo\" xmlns:xi=\"{XiNs}\"/></doc>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.IsFatal.Should().BeTrue();
    }

    [Fact]
    public void Included_top_element_gets_xml_base_of_origin()
    {
        // Modelled on baseuri052.xml + dir/data1.xml: master includes dir/data1.xml, whose
        // top element <para> has no xml:base of its own, but a descendant <item> carries its
        // own xml:base="dir2/data.xml" (composes against the stamped parent base).
        Write(
            "dir/data1.xml",
            "<para><list><item>two</item>" +
            "<item xml:base=\"dir2/data.xml\">three</item></list></para>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            "<doc><chap>" +
            $"<xi:include href=\"dir/data1.xml\" xmlns:xi=\"{XiNs}\"/></chap></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var para = result.SelectSingleNode("//para") as XmlElement;
        para.Should().NotBeNull();
        para!.GetAttribute("base", "http://www.w3.org/XML/1998/namespace")
            .Should().EndWith("dir/data1.xml");

        var item = result.SelectSingleNode("//item[.='three']") as XmlElement;
        item.Should().NotBeNull();
        item!.GetAttribute("base", "http://www.w3.org/XML/1998/namespace")
            .Should().Be("dir2/data.xml");

        // The descendant's own (unmodified) xml:base composes against the stamped parent
        // base to resolve to .../dir/dir2/data.xml.
        var composed = new Uri(new Uri(para.GetAttribute("base", "http://www.w3.org/XML/1998/namespace")),
            item.GetAttribute("base", "http://www.w3.org/XML/1998/namespace"));
        composed.LocalPath.Should().EndWith(Path.Combine("dir", "dir2", "data.xml"));
    }

    [Fact]
    public void Included_element_with_own_xml_base_is_not_overwritten()
    {
        Write("dir/data.xml", "<para xml:base=\"custom/origin.xml\">five</para>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"dir/data.xml\" xmlns:xi=\"{XiNs}\"/></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var para = result.SelectSingleNode("//para") as XmlElement;
        para.Should().NotBeNull();
        para!.GetAttribute("base", "http://www.w3.org/XML/1998/namespace")
            .Should().Be("custom/origin.xml");
    }

    [Fact]
    public void Cyclic_inclusion_reports_Cyclic_kind()
    {
        // a.xml includes itself → active-inclusion stack must detect the cycle.
        Write("a.xml",
            $"<wrap><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></wrap>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"a.xml\" xmlns:xi=\"{XiNs}\"/></doc>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var ex = act.Should().Throw<XIncludeException>().Which;
        ex.IsFatal.Should().BeTrue();
        ex.Kind.Should().Be(XIncludeErrorKind.Cyclic);
    }

    [Fact]
    public void Xml_lang_propagates_from_include_context_when_absent()
    {
        Write("dir/data1.xml", "<para><item>two</item></para>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            "<doc xml:lang=\"en\"><chap>" +
            $"<xi:include href=\"dir/data1.xml\" xmlns:xi=\"{XiNs}\"/></chap></doc>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var para = result.SelectSingleNode("//para") as XmlElement;
        para.Should().NotBeNull();
        para!.GetAttribute("lang", "http://www.w3.org/XML/1998/namespace").Should().Be("en");
    }

    [Fact]
    public void Missing_target_uses_fallback_content()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<xi:include href=\"nope.xml\"><xi:fallback><f>backup</f></xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectNodes("//f[.='backup']")!.Count.Should().Be(1);
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Empty_fallback_removes_the_include()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<a/><xi:include href=\"nope.xml\"><xi:fallback/></xi:include><b/></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        // include gone; siblings intact and in order
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
        var kids = result.DocumentElement!.ChildNodes;
        kids.Count.Should().Be(2);
        kids[0]!.LocalName.Should().Be("a");
        kids[1]!.LocalName.Should().Be("b");
    }

    [Fact]
    public void Nested_fallback_include_is_expanded()
    {
        Write("in.xml", "<in>ok</in>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<xi:include href=\"nope.xml\"><xi:fallback>" +
            $"<xi:include href=\"in.xml\"/></xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectNodes("//in[.='ok']")!.Count.Should().Be(1);
    }

    [Fact]
    public void Nested_fallback_include_resolves_against_include_xml_base()
    {
        // XML Base: an xml:base on the failing xi:include applies to its fallback subtree, so a
        // relative href on a nested xi:include inside the fallback resolves against .../sub/,
        // not the master's parent base. (Without the fixup this include would miss its target.)
        Write(Path.Combine("sub", "in.xml"), "<in>ok</in>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<xi:include href=\"nope.xml\" xml:base=\"sub/\"><xi:fallback>" +
            "<xi:include href=\"in.xml\"/></xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectNodes("//in[.='ok']")!.Count.Should().Be(1);
    }

    [Fact]
    public void Successful_include_drops_its_fallback_child()
    {
        // A successful include ignores any xi:fallback child; the fallback must be dropped, not
        // spliced and not flagged as a misplaced fallback.
        Write("real.xml", "<real>here</real>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<xi:include href=\"real.xml\"><xi:fallback><oops/></xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectNodes("//real[.='here']")!.Count.Should().Be(1);
        result.GetElementsByTagName("fallback", XiNs).Count.Should().Be(0);
        result.SelectNodes("//oops")!.Count.Should().Be(0);
    }

    [Fact]
    public void No_fallback_on_missing_target_is_fatal_ResourceError()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"nope.xml\"/></m>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var ex = act.Should().Throw<XIncludeException>().Which;
        ex.IsFatal.Should().BeTrue();
        ex.Kind.Should().Be(XIncludeErrorKind.ResourceError);
    }

    [Fact]
    public void Multiple_fallbacks_is_fatal_MalformedFallback()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\">" +
            "<xi:include href=\"nope.xml\"><xi:fallback/><xi:fallback/></xi:include></m>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.MalformedFallback);
    }

    [Fact]
    public void Misplaced_fallback_is_fatal_MalformedFallback()
    {
        // xi:fallback that is not a child of xi:include
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster($"<m xmlns:xi=\"{XiNs}\"><xi:fallback/></m>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.MalformedFallback);
    }

    [Fact]
    public void Xpointer_element_selects_subresource_from_target()
    {
        Write("parts.xml", "<doc><a>one</a><b>two</b><c>three</c></doc>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"parts.xml\" xpointer=\"element(/1/2)\"/></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        // Selected b, not the whole doc: <b>two</b> present, <a>/<c> absent.
        result.SelectNodes("//b[.='two']")!.Count.Should().Be(1);
        result.SelectNodes("//a")!.Count.Should().Be(0);
        result.GetElementsByTagName("include", XiNs).Count.Should().Be(0);
    }

    [Fact]
    public void Xpointer_empty_selection_uses_fallback()
    {
        Write("parts.xml", "<doc><a/></doc>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"parts.xml\" xpointer=\"element(/1/9)\">" +
            "<xi:fallback><fb>backup</fb></xi:fallback></xi:include></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        result.SelectNodes("//fb[.='backup']")!.Count.Should().Be(1);
    }

    [Fact]
    public void Xpointer_multi_node_selection_spliced_in_order()
    {
        Write("parts.xml", "<doc><x>1</x><x>2</x></doc>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"parts.xml\" xpointer=\"xpath1(//x)\"/></m>");

        var result = XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        var xs = result.SelectNodes("/m/x")!;
        xs.Count.Should().Be(2);
        xs[0]!.InnerText.Should().Be("1");
        xs[1]!.InnerText.Should().Be("2");
    }

    [Fact]
    public void Xpointer_selecting_attribute_is_fatal()
    {
        Write("parts.xml", "<doc a='v'/>");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<m xmlns:xi=\"{XiNs}\"><xi:include href=\"parts.xml\" xpointer=\"xpath1(/doc/@a)\"/></m>");

        var act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.MalformedInclude);
    }
}
