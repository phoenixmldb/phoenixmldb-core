using System;
using System.IO;
using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

/// <summary>
/// Tests for <see cref="XIncludeProcessor"/> — the SP1 core: <c>parse="xml"</c> +
/// <c>href</c> inclusion, recursion into included content, cyclic/depth fatal guards,
/// and the "unsupported" errors for the SP2/SP3 features (xpointer, <c>parse="text"</c>).
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
    public void Xpointer_raises_unsupported()
    {
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            "<doc><xi:include href=\"a.xml\" xpointer=\"element(/1/2)\" " +
            $"xmlns:xi=\"{XiNs}\"/></doc>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>()
            .Which.Message.Should().Contain("not supported");
    }

    [Fact]
    public void ParseText_raises_unsupported()
    {
        Write("a.txt", "plain text");
        var masterUri = BaseFor("master.xml");
        var master = LoadMaster(
            $"<doc><xi:include href=\"a.txt\" parse=\"text\" xmlns:xi=\"{XiNs}\"/></doc>");

        Action act = () => XIncludeProcessor.Expand(master, masterUri, new XIncludeOptions());

        act.Should().Throw<XIncludeException>()
            .Which.Message.Should().Contain("not supported");
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
}
