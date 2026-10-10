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

    // U+1D49C MATHEMATICAL SCRIPT CAPITAL A (Lu), U+1D49E SCRIPT CAPITAL C (Lu),
    // U+1D4A2 SCRIPT CAPITAL G (Lu), U+1D4B5 SCRIPT CAPITAL Z (Lu), U+1F600 GRINNING FACE (So).
    private const string LetterA = "\U0001D49C";
    private const string LetterC = "\U0001D49E";
    private const string LetterG = "\U0001D4A2";
    private const string LetterZ = "\U0001D4B5";
    private const string Face = "\U0001F600";

    /// <summary>
    /// Everything in a pattern that stands for one character counts a character above U+FFFF
    /// as one, and knows what it is: a negated class and a negated category matched it as two
    /// characters, and the category it belongs to did not match it at all.
    /// </summary>
    [Theory]
    // a negated class
    [InlineData("[^a]", Astral, true)]
    [InlineData("[^a]", Astral + Astral, false)]
    [InlineData("[^a]{2}", Astral + "b", true)]
    [InlineData("[^a]", "a", false)]
    [InlineData("[^a]", "b", true)]
    // a category and its negation
    [InlineData(@"\p{L}", LetterA, true)]
    [InlineData(@"\p{Lu}", LetterA, true)]
    [InlineData(@"\p{Ll}", LetterA, false)]
    [InlineData(@"\p{L}", Astral, false)]
    [InlineData(@"\P{L}", Astral, true)]
    [InlineData(@"\P{L}", LetterA, false)]
    [InlineData(@"\P{L}", Astral + Astral, false)]
    [InlineData(@"\p{Nd}", Astral, true)]
    [InlineData(@"\p{N}", Astral, true)]
    [InlineData(@"\p{So}", Face, true)]
    [InlineData(@"\p{S}", Face, true)]
    [InlineData(@"\p{L}+", "a" + LetterA + "b" + LetterC, true)]
    // the multi-character escapes
    [InlineData(@"\d", Astral, true)]
    [InlineData(@"\D", Astral, false)]
    [InlineData(@"\D", LetterA, true)]
    [InlineData(@"\D{2}", LetterA, false)]
    [InlineData(@"\S", Astral, true)]
    [InlineData(@"\S{2}", Astral, false)]
    [InlineData(@"\s", Astral, false)]
    [InlineData(@"\w", LetterA, true)]
    [InlineData(@"\w", Face, true)]
    [InlineData(@"\W", LetterA, false)]
    [InlineData(@"\W", Astral, false)]
    [InlineData(@"\I", Astral, true)]
    [InlineData(@"\I{2}", Astral, false)]
    [InlineData(@"\i", Astral, false)]
    [InlineData(@"\C", Astral, true)]
    // classes that hold them
    [InlineData(@"[\p{L}]", LetterA, true)]
    [InlineData(@"[\p{L}\d]", Astral, true)]
    [InlineData(@"[\p{L}_]", Astral, false)]
    [InlineData(@"[^\p{L}]", LetterA, false)]
    [InlineData(@"[^\p{L}]", Astral, true)]
    [InlineData(@"[^\p{L}]", Astral + Astral, false)]
    [InlineData(@"[\P{L}x]", LetterA, false)]
    [InlineData(@"[\P{L}x]", Astral, true)]
    [InlineData(@"[\S]", Astral, true)]
    // a character above U+FFFF written in the class
    [InlineData("[" + LetterA + "b]", LetterA, true)]
    [InlineData("[" + LetterA + "b]", "b", true)]
    [InlineData("[" + LetterA + "b]", LetterC, false)]
    [InlineData("[" + LetterA + "b]", Astral, false)]
    [InlineData("[" + LetterA + "-" + LetterZ + "]", LetterG, true)]
    [InlineData("[" + LetterA + "-" + LetterZ + "]", Astral, false)]
    [InlineData("[" + LetterA + "-" + LetterZ + "]", "A", false)]
    [InlineData("[a-" + LetterZ + "]", LetterG, true)]
    [InlineData("[a-" + LetterZ + "]", "z", true)]
    [InlineData("[a-" + LetterZ + "]", "A", false)]
    [InlineData("[^" + LetterA + "]", LetterA, false)]
    [InlineData("[^" + LetterA + "]", LetterC, true)]
    [InlineData("[^" + LetterA + "]", "b", true)]
    [InlineData("[" + LetterA + "]", LetterA, true)]
    // subtraction
    [InlineData(@"[\p{L}-[" + LetterA + "]]", LetterA, false)]
    [InlineData(@"[\p{L}-[" + LetterA + "]]", LetterC, true)]
    [InlineData(@"[\p{L}-[" + LetterA + "]]", "x", true)]
    [InlineData(@"[^a-[\p{L}]]", LetterA, false)]
    [InlineData(@"[^a-[\p{L}]]", Astral, true)]
    [InlineData(@"[a-z-[aeiou]]", "b", true)]
    [InlineData(@"[a-z-[aeiou]]", "a", false)]
    // a character above U+FFFF written as itself, with a quantifier
    [InlineData(LetterA + "{2}", LetterA + LetterA, true)]
    [InlineData(LetterA + "{2}", LetterA, false)]
    [InlineData(LetterA + "+", LetterA + LetterA + LetterA, true)]
    [InlineData(LetterA + "?b", "b", true)]
    [InlineData(LetterA + "?b", LetterA + "b", true)]
    // nothing above U+FFFF in the pattern or the value: as before
    [InlineData(@"\P{L}", "1", true)]
    [InlineData(@"\P{L}", "a", false)]
    [InlineData(@"\p{L}+", "abc", true)]
    [InlineData(@"\d{3}-\d{2}", "123-45", true)]
    [InlineData(@"[^\s]+", "a b", false)]
    [InlineData(@"[\-a]", "-", true)]
    [InlineData(@"[a\-]", "-", true)]
    [InlineData(@"[a-]", "-", true)]
    [InlineData(@"[-a]", "-", true)]
    [InlineData(@"\[x\]", "[x]", true)]
    public void Each_atom_that_stands_for_one_character_matches_one_code_point(string pattern, string value, bool valid)
        => Valid(pattern, value).Should().Be(valid);

    /// <summary>A pattern with nothing that can meet a character above U+FFFF is handed on as written.</summary>
    [Theory]
    [InlineData("[a-z]+")]
    [InlineData(@"\s\i\c*")]
    [InlineData(@"\n\t\-\.")]
    [InlineData(@"[A-Za-z0-9_\-]{1,8}")]
    [InlineData(@"[a-z-[aeiou]]")]
    [InlineData(@"(ab|cd)?\.\\[")]
    public void A_pattern_that_needs_no_change_is_not_changed(string pattern)
        => SchemaPatternGuard.WithWholeCharacters(pattern).Should().Be(pattern);

    [Fact]
    public void And_so_it_does_under_a_match_time_limit()
        => Valid(".", Astral, TimeSpan.FromSeconds(5)).Should().BeTrue();

    /// <summary>
    /// A schema set is compiled again each time a schema is added to it, and its patterns are
    /// rewritten once: the second call leaves a rewritten pattern as it is.
    /// </summary>
    [Fact]
    public void The_rewrite_is_not_applied_twice()
    {
        var set = new System.Xml.Schema.XmlSchemaSet();
        using var reader = System.Xml.XmlReader.Create(new StringReader("""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:simpleType name="t">
                <xs:restriction base="xs:string"><xs:pattern value="a.[^b]\P{L}"/></xs:restriction>
              </xs:simpleType>
            </xs:schema>
            """));
        set.Add(null, reader);

        string Pattern() => set.Schemas().Cast<System.Xml.Schema.XmlSchema>().Single().Items
            .OfType<System.Xml.Schema.XmlSchemaSimpleType>().Single().Content
            .Should().BeOfType<System.Xml.Schema.XmlSchemaSimpleTypeRestriction>().Subject.Facets
            .OfType<System.Xml.Schema.XmlSchemaPatternFacet>().Single().Value!;

        SchemaPatternGuard.MatchWholeCharacters(set);
        var once = Pattern();
        SchemaPatternGuard.MatchWholeCharacters(set);

        once.Should().NotBe(@"a.[^b]\P{L}");
        Pattern().Should().Be(once);
    }
}
