using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

public sealed class XIncludeLimitsTests
{
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
}
