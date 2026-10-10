using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// "." in a pattern facet is one character, as XML Schema counts characters: a code point.
/// System.Xml matches one UTF-16 unit, so the pattern "." refused a value of one character
/// outside the Basic Multilingual Plane (QT3 app-CatalogCheck Catalog002, whose catalog schema
/// declares such a type for the characters of a decimal format).
/// </summary>
public sealed class SchemaPatternCharacterTests
{
    private static readonly Uri SchemaUri = new("urn:test:pattern-characters.xsd");

    // U+1D7CE MATHEMATICAL BOLD DIGIT ZERO: one character, two UTF-16 units.
    private const string Astral = "\U0001D7CE";

    private static bool Valid(string pattern, string value, TimeSpan? limit = null)
    {
        var schema = SchemaCompiler.Compile([SchemaUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText($"""
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                  <xs:simpleType name="t">
                    <xs:restriction base="xs:string">
                      <xs:pattern value="{pattern}"/>
                    </xs:restriction>
                  </xs:simpleType>
                  <xs:element name="v" type="t"/>
                </xs:schema>
                """, SchemaUri)],
            new SchemaCompileOptions { PatternMatchTimeout = limit });
        return SchemaValidator.Validate(schema, $"<v>{value}</v>").IsValid;
    }

    [Theory]
    [InlineData(".", "a", true)]
    [InlineData(".", Astral, true)]
    [InlineData(".", "ab", false)]
    [InlineData(".", Astral + Astral, false)]
    [InlineData(".", "", false)]
    [InlineData(".{2}", Astral + "a", true)]
    [InlineData(".{2}", Astral, false)]
    [InlineData("a.c", "a" + Astral + "c", true)]
    [InlineData(".*", "a" + Astral + "b", true)]
    // an escaped full stop, and one in a character class, are the character "."
    [InlineData(@"\.", ".", true)]
    [InlineData(@"\.", "a", false)]
    [InlineData("[.]", ".", true)]
    [InlineData("[.]", "a", false)]
    [InlineData(@"[a-z-[.]]\.", "a.", true)]
    public void A_full_stop_matches_one_character(string pattern, string value, bool valid)
        => Valid(pattern, value).Should().Be(valid);

    [Fact]
    public void And_so_it_does_under_a_match_time_limit()
        => Valid(".", Astral, TimeSpan.FromSeconds(5)).Should().BeTrue();

    [Fact]
    public void The_rewrite_is_not_applied_twice()
        => SchemaPatternGuard.WithWholeCharacters(SchemaPatternGuard.WithWholeCharacters("a.b"))
            .Should().Be(SchemaPatternGuard.WithWholeCharacters("a.b"));
}
