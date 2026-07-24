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
}
