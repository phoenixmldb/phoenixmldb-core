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
}
