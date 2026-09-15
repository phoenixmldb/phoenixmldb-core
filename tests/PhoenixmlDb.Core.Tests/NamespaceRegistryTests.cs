using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

public class NamespaceRegistryTests
{
    [Theory]
    [InlineData(13, "https://schemas.phoenixml.dev/2026/functions", "phx")]
    [InlineData(11, "https://schemas.phoenixml.dev/2026/meta", "dbxml")]
    [InlineData(12, "http://purl.org/dc/terms/",               "dcterms")]
    [InlineData(3,  "http://www.w3.org/2001/XMLSchema",        "xs")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "NamespaceRegistry works with string URIs throughout")]
    public void WellKnownNamespaces_ResolveToUriAndPrefix(uint id, string uri, string prefix)
    {
        var ns = new NamespaceId(id);
        NamespaceRegistry.GetUri(ns).Should().Be(uri);
        NamespaceRegistry.GetConventionalPrefix(ns).Should().Be(prefix);
    }

    [Fact]
    public void TryGetId_RoundTripsEveryWellKnownUri()
    {
        foreach (var ns in NamespaceRegistry.WellKnown)
        {
            var uri = NamespaceRegistry.GetUri(ns)!;
            NamespaceRegistry.TryGetId(uri, out var back).Should().BeTrue();
            back.Should().Be(ns);
        }
    }

    [Fact]
    public void UserNamespaces_AreNotWellKnown()
    {
        NamespaceRegistry.GetUri(new NamespaceId(NamespaceId.FirstUserNamespaceId)).Should().BeNull();
    }

    [Fact]
    public void EveryWellKnownId_IsBelowFirstUserNamespaceId()
    {
        NamespaceRegistry.WellKnown.Should()
            .OnlyContain(ns => ns.Value < NamespaceId.FirstUserNamespaceId);
    }

    // Id 9 was https://schemas.phoenixml.dev/2026/db. Stored keys encode namespace ids, so it is retired
    // rather than reassigned: it resolves to nothing, and no entry takes its value.
    [Fact]
    public void RetiredId9_ResolvesToNothing()
    {
        var retired = new NamespaceId(9);
        NamespaceRegistry.GetUri(retired).Should().BeNull();
        NamespaceRegistry.GetConventionalPrefix(retired).Should().BeNull();
        NamespaceRegistry.WellKnown.Should().NotContain(retired);
    }

    [Fact]
    public void TheRetiredDbUri_HasNoId()
        => NamespaceRegistry.TryGetId("https://schemas.phoenixml.dev/2026/db", out _).Should().BeFalse();

    [Fact]
    public void TheW3cFullTextUri_HasNoId()
        => NamespaceRegistry.TryGetId("http://www.w3.org/2007/xpath-full-text", out _).Should().BeFalse();

    [Fact]
    public void TheFunctionsNamespace_IsId13()
        => NamespaceId.PhoenixmlFunctions.Should().Be(new NamespaceId(13));

    [Fact]
    public void EachConventionalPrefix_NamesOneNamespace()
        => NamespaceRegistry.WellKnown.Select(NamespaceRegistry.GetConventionalPrefix).Should().OnlyHaveUniqueItems();
}
