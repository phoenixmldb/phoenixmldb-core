using AwesomeAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

// CA2234 asks for the Uri overload of AddNameIndex. The string overload is the one that keeps a
// namespace name unchanged; these tests exercise it on purpose.
#pragma warning disable CA2234

/// <summary>
/// A name index keeps its namespace name exactly as given (phoenixml#141). It was stored as a
/// <see cref="Uri"/>, which normalizes: <c>http://example.com</c> became <c>http://example.com/</c>,
/// and an index declared for that namespace never matched its elements, because a namespace name
/// is compared as a string. Any non-empty string is a namespace name; <c>null</c> and the empty
/// string mean all namespaces.
/// </summary>
public class NameIndexNamespaceNameTests
{
    private static string? NameOf(IndexConfiguration config)
        => config.Indexes.OfType<NameIndexDefinition>().Single().NamespaceName;

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("HTTP://Example.COM/ns")]
    [InlineData("urn:example:names")]
    [InlineData("my-ns")]
    [InlineData("http://example.com/a b")]
    public void A_namespace_name_is_kept_exactly_as_given(string name)
        => NameOf(new IndexConfiguration().AddNameIndex(name)).Should().Be(name);

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("HTTP://Example.COM/ns")]
    [InlineData("urn:example:names")]
    public void A_Uri_gives_its_original_string_not_its_normalized_form(string name)
        => NameOf(new IndexConfiguration().AddNameIndex(new Uri(name))).Should().Be(name);

    [Fact]
    public void A_relative_name_is_accepted()
        => NameOf(new IndexConfiguration().AddNameIndex(new Uri("my-ns", UriKind.Relative))).Should().Be("my-ns");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_namespace_name_is_all_namespaces(string? name)
        => NameOf(new IndexConfiguration().AddNameIndex(name)).Should().BeNull();

    [Fact]
    public void An_empty_Uri_is_all_namespaces()
        => NameOf(new IndexConfiguration().AddNameIndex(new Uri("", UriKind.RelativeOrAbsolute))).Should().BeNull();

    [Fact]
    public void The_same_name_from_a_string_and_from_a_Uri_gives_equal_definitions()
        => new IndexConfiguration().AddNameIndex("http://example.com").Indexes[0]
            .Should().Be(new IndexConfiguration().AddNameIndex(new Uri("http://example.com")).Indexes[0]);

#pragma warning disable CS0618 // NamespaceUri is obsolete; these tests pin its mapping to NamespaceName.
    [Fact]
    public void The_obsolete_Uri_property_gives_the_same_string_back()
    {
        var index = new NameIndexDefinition { NamespaceUri = new Uri("http://example.com") };

        index.NamespaceName.Should().Be("http://example.com");
        index.NamespaceUri!.OriginalString.Should().Be("http://example.com");
    }

    [Fact]
    public void The_obsolete_Uri_property_reads_a_name_that_was_set_as_a_string()
        => new NameIndexDefinition { NamespaceName = "my-ns" }.NamespaceUri!.OriginalString.Should().Be("my-ns");

    [Fact]
    public void The_obsolete_Uri_property_is_null_for_a_name_that_is_not_a_URI_reference()
    {
        var index = new NameIndexDefinition { NamespaceName = "http://[" };

        index.NamespaceUri.Should().BeNull();
        index.ToString().Should().Contain("http://[");
    }
#pragma warning restore CS0618
}
