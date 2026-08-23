using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

public class NamespaceRegistryTests
{
    [Theory]
    [InlineData(9,  "https://schemas.phoenixml.dev/2026/db",   "phx")]
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
}
