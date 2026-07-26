using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

public sealed class XIncludeLimitsTests
{
    // A resolver whose every fetch is a cheap, non-fatal resource error — so a fallback chain
    // recurses (ProcessInclude → RecoverWithFallback → ExpandNode) at full speed without paying
    // for thousands of real filesystem misses.
    private sealed class AlwaysFailsResolver : IXmlResourceResolver
    {
        public XmlReader ResolveXml(System.Uri absolute) =>
            throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false, "always fails");

        public string ResolveText(System.Uri absolute, string? encoding, string? accept, string? acceptLanguage) =>
            throw new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false, "always fails");
    }

    [Fact]
    public void Deep_fallback_chain_is_bounded_at_the_DEFAULT_depth()
    {
        // The regression that the SP4 whole-branch review caught: the mechanism (depth guard) was
        // correct but the DEFAULT MaxExpansionDepth (5000) was higher than a normal ~1 MB thread
        // stack survives on the frame-heavy fallback path (ExpandNode → ProcessInclude →
        // RecoverWithFallback → ExpandNode), so with default options the guard could never fire —
        // the process StackOverflowed first. Run a fallback chain deeper than the default depth: it
        // must throw a catchable LimitExceeded, not crash the host. (A StackOverflow is uncatchable,
        // so a crash here = a failing test.) Uses the DEFAULT MaxExpansionDepth (5000) via an
        // options object that only swaps in the fast always-fail resolver.
        const string ns = "http://www.w3.org/2001/XInclude";
        var open = new System.Text.StringBuilder();
        var close = new System.Text.StringBuilder();
        for (int i = 0; i < 6000; i++)
        {
            open.Append("<xi:include href='x.xml'><xi:fallback>");
            close.Insert(0, "</xi:fallback></xi:include>");
        }
        var master = new XmlDocument { PreserveWhitespace = true };
        master.LoadXml($"<root xmlns:xi='{ns}'>{open}{close}</root>");

        var act = () => XIncludeProcessor.Expand(
            master, new System.Uri("file:///m.xml"),
            new XIncludeOptions { Resolver = new AlwaysFailsResolver() }); // DEFAULT MaxExpansionDepth (5000)

        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Limiter_depth_breach_throws_LimitExceeded()
    {
        var limiter = new XIncludeLimiter(new XIncludeOptions { MaxExpansionDepth = 2 });
        limiter.EnterExpansion(); // 1
        limiter.EnterExpansion(); // 2
        var act = () => limiter.EnterExpansion(); // 3 > 2
        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Limiter_node_budget_breach_throws_LimitExceeded()
    {
        var limiter = new XIncludeLimiter(new XIncludeOptions { MaxExpandedNodes = 3 });
        limiter.ConsumeNodes(2);
        var act = () => limiter.ConsumeNodes(2); // 4 > 3
        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Limiter_zero_means_unlimited()
    {
        var limiter = new XIncludeLimiter(new XIncludeOptions { MaxExpansionDepth = 0, MaxExpandedNodes = 0 });
        for (int i = 0; i < 100000; i++) limiter.EnterExpansion();
        limiter.ConsumeNodes(long.MaxValue / 2);
        limiter.ConsumeNodes(long.MaxValue / 2);
        // no throw
        limiter.XPathTimeoutMs.Should().Be(5000); // default
    }

    private static XmlDocument LoadDoc(string xml)
    {
        var d = new XmlDocument { PreserveWhitespace = true };
        d.LoadXml(xml);
        return d;
    }

    [Fact]
    public void Deep_plain_tree_is_bounded_not_stackoverflow()
    {
        // A tree deeper than MaxExpansionDepth must fail fatally, not StackOverflow.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 500; i++) sb.Append("<a>");
        for (int i = 0; i < 500; i++) sb.Append("</a>");
        var master = LoadDoc($"<root>{sb}</root>");
        var act = () => XIncludeProcessor.Expand(master, new System.Uri("file:///m.xml"),
            new XIncludeOptions { MaxExpansionDepth = 100 });
        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Deep_fallback_chain_is_bounded_not_stackoverflow()
    {
        // Each xi:include fetches a missing local file (non-fatal resource error → fallback), whose
        // fallback contains the next failing include, N deep. This path recurses ProcessInclude →
        // RecoverWithFallback → ExpandNode without the include-stack growing; MaxExpansionDepth must
        // still bound it.
        const string ns = "http://www.w3.org/2001/XInclude";
        var open = new System.Text.StringBuilder();
        var close = new System.Text.StringBuilder();
        for (int i = 0; i < 300; i++)
        {
            open.Append(System.Globalization.CultureInfo.InvariantCulture, $"<xi:include href='/no/such/file/{i}'><xi:fallback>");
            close.Insert(0, "</xi:fallback></xi:include>");
        }
        var master = LoadDoc($"<root xmlns:xi='{ns}'>{open}{close}</root>");
        var act = () => XIncludeProcessor.Expand(master, new System.Uri("file:///m.xml"),
            new XIncludeOptions { MaxExpansionDepth = 50 });
        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Same_document_exponential_blowup_is_bounded()
    {
        // p1 includes p0 twice, p2 includes p1 twice, … → 2^n copies. With a small node budget this
        // must fail fatally (LimitExceeded), not OOM.
        const string ns = "http://www.w3.org/2001/XInclude";
        var sb = new System.Text.StringBuilder($"<root xmlns:xi='{ns}'><part xml:id='p0'><leaf/></part>");
        for (int n = 1; n <= 30; n++)
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"<part xml:id='p{n}'><xi:include xpointer='element(p{n - 1})'/>" +
                $"<xi:include xpointer='element(p{n - 1})'/></part>");
        sb.Append("</root>");
        var master = LoadDoc(sb.ToString());
        var act = () => XIncludeProcessor.Expand(master, new System.Uri("file:///m.xml"),
            new XIncludeOptions { MaxExpandedNodes = 100_000 });
        act.Should().Throw<XIncludeException>().Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
    }

    [Fact]
    public void Benign_multi_node_splice_under_budget_succeeds()
    {
        const string ns = "http://www.w3.org/2001/XInclude";
        var master = LoadDoc(
            $"<root xmlns:xi='{ns}'><src><a/><b/></src>" +
            "<xi:include xpointer='xpath1(//src/*)'/></root>");
        var result = XIncludeProcessor.Expand(master, new System.Uri("file:///m.xml"),
            new XIncludeOptions { MaxExpandedNodes = 1000 });
        result.SelectNodes("/root/a")!.Count.Should().Be(1);
        result.SelectNodes("/root/b")!.Count.Should().Be(1);
    }

    [Fact]
    public void Oversized_xml_resource_is_a_resource_error_recoverable_by_fallback()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xi-lim-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            // A big.xml comfortably over a tiny MaxResourceBytes.
            var big = "<big>" + new string('x', 5000) + "</big>";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "big.xml"), big);
            var masterUri = new System.Uri(System.IO.Path.Combine(dir, "m.xml"));
            const string ns = "http://www.w3.org/2001/XInclude";
            var master = LoadDoc(
                $"<m xmlns:xi='{ns}'><xi:include href='big.xml'>" +
                "<xi:fallback><fb>small</fb></xi:fallback></xi:include></m>");

            var result = XIncludeProcessor.Expand(master, masterUri,
                new XIncludeOptions { MaxResourceBytes = 500 });

            // Oversized resource → resource error → fallback recovers.
            result.SelectNodes("//fb[.='small']")!.Count.Should().Be(1);
        }
        finally { System.IO.Directory.Delete(dir, recursive: true); }
    }
}
