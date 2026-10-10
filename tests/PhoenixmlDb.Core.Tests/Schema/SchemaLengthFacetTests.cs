using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// The length facets of a string type count characters as XML Schema does, by code point.
/// System.Xml counts UTF-16 units, so <c>maxLength="1"</c> refused one character outside the
/// Basic Multilingual Plane and <c>minLength="2"</c> accepted it.
/// </summary>
public sealed class SchemaLengthFacetTests
{
    private static readonly Uri SchemaUri = new("urn:test:length-facets.xsd");

    // U+1F600 GRINNING FACE: one character, two UTF-16 units.
    private const string Face = "\U0001F600";

    private static CompiledSchema Compile(string types)
        => SchemaCompiler.Compile([SchemaUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText($"""
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                  {types}
                </xs:schema>
                """, SchemaUri)],
            new SchemaCompileOptions());

    private static SchemaValidationResult Check(string facets, string value, string @base = "xs:string")
        => SchemaValidator.Validate(Compile($"""
            <xs:simpleType name="t"><xs:restriction base="{@base}">{facets}</xs:restriction></xs:simpleType>
            <xs:element name="v" type="t"/>
            """), $"<v>{value}</v>");

    [Theory]
    [InlineData("<xs:maxLength value='1'/>", Face, true)]
    [InlineData("<xs:maxLength value='1'/>", Face + Face, false)]
    [InlineData("<xs:maxLength value='1'/>", "ab", false)]
    [InlineData("<xs:maxLength value='1'/>", "", true)]
    [InlineData("<xs:maxLength value='2'/>", Face + "a", true)]
    [InlineData("<xs:length value='1'/>", Face, true)]
    [InlineData("<xs:length value='2'/>", Face, false)]
    [InlineData("<xs:length value='2'/>", Face + "a", true)]
    [InlineData("<xs:length value='0'/>", "", true)]
    [InlineData("<xs:minLength value='2'/>", Face, false)]
    [InlineData("<xs:minLength value='2'/>", Face + Face, true)]
    [InlineData("<xs:minLength value='1'/><xs:maxLength value='2'/>", Face + Face, true)]
    [InlineData("<xs:minLength value='1'/><xs:maxLength value='2'/>", Face + Face + Face, false)]
    [InlineData("<xs:minLength value='1'/><xs:maxLength value='2'/>", "", false)]
    // nothing above U+FFFF: as before
    [InlineData("<xs:maxLength value='3'/>", "abc", true)]
    [InlineData("<xs:maxLength value='3'/>", "abcd", false)]
    [InlineData("<xs:length value='3'/>", "abc", true)]
    [InlineData("<xs:length value='3'/>", "ab", false)]
    [InlineData("<xs:minLength value='3'/>", "ab", false)]
    // a line end is a character
    [InlineData("<xs:length value='3'/>", "a\nb", true)]
    // the patterns of the same restriction are alternatives, and the length holds for each
    [InlineData("<xs:pattern value='a+'/><xs:pattern value='b+'/><xs:maxLength value='2'/>", "aa", true)]
    [InlineData("<xs:pattern value='a+'/><xs:pattern value='b+'/><xs:maxLength value='2'/>", "bb", true)]
    [InlineData("<xs:pattern value='a+'/><xs:pattern value='b+'/><xs:maxLength value='2'/>", "aaa", false)]
    [InlineData("<xs:pattern value='a+'/><xs:pattern value='b+'/><xs:maxLength value='2'/>", "ab", false)]
    [InlineData("<xs:pattern value='.+'/><xs:maxLength value='2'/>", Face + Face, true)]
    [InlineData("<xs:pattern value='.+'/><xs:maxLength value='2'/>", Face + Face + Face, false)]
    public void A_length_is_a_number_of_characters(string facets, string value, bool valid)
        => Check(facets, value).IsValid.Should().Be(valid);

    [Theory]
    // the length is that of the value after its white space is normalised
    [InlineData("xs:token", "<xs:maxLength value='2'/>", "  ab  ", true)]
    [InlineData("xs:token", "<xs:maxLength value='2'/>", "abc", false)]
    [InlineData("xs:token", "<xs:length value='3'/>", " a  b ", true)]
    [InlineData("xs:normalizedString", "<xs:length value='3'/>", "a\tb", true)]
    [InlineData("xs:anyURI", "<xs:maxLength value='8'/>", "urn:x:" + Face, true)]
    [InlineData("xs:NCName", "<xs:maxLength value='2'/>", "abc", false)]
    // a number of octets, not of characters
    [InlineData("xs:hexBinary", "<xs:length value='2'/>", "0A0B", true)]
    [InlineData("xs:hexBinary", "<xs:length value='2'/>", "0A", false)]
    [InlineData("xs:base64Binary", "<xs:maxLength value='3'/>", "AAAA", true)]
    public void Of_the_types_whose_length_is_one(string @base, string facets, string value, bool valid)
        => Check(facets, value, @base).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("a " + Face, true)]
    [InlineData("a", false)]
    [InlineData("a b c", false)]
    public void The_length_of_a_list_is_a_number_of_items(string value, bool valid)
        => SchemaValidator.Validate(Compile("""
            <xs:simpleType name="words"><xs:list itemType="xs:string"/></xs:simpleType>
            <xs:simpleType name="t"><xs:restriction base="words"><xs:length value="2"/></xs:restriction></xs:simpleType>
            <xs:element name="v" type="t"/>
            """), $"<v>{value}</v>").IsValid.Should().Be(valid);

    [Theory]
    [InlineData(Face + Face + Face, true)]
    [InlineData(Face + Face + Face + Face, false)]
    [InlineData("abcd", false)]
    public void A_derived_type_keeps_the_length_of_its_base_and_adds_its_own(string value, bool valid)
        => SchemaValidator.Validate(Compile("""
            <xs:simpleType name="five"><xs:restriction base="xs:string"><xs:maxLength value="5"/></xs:restriction></xs:simpleType>
            <xs:simpleType name="t"><xs:restriction base="five"><xs:maxLength value="3"/></xs:restriction></xs:simpleType>
            <xs:element name="v" type="t"/>
            """), $"<v>{value}</v>").IsValid.Should().Be(valid);

    [Theory]
    [InlineData("<v code='" + Face + "'>" + Face + Face + "</v>", true)]
    [InlineData("<v code='" + Face + Face + "'>a</v>", false)]
    [InlineData("<v code='a'>" + Face + Face + Face + "</v>", false)]
    public void In_an_attribute_and_in_simple_content(string document, bool valid)
        => SchemaValidator.Validate(Compile("""
            <xs:simpleType name="one"><xs:restriction base="xs:string"><xs:length value="1"/></xs:restriction></xs:simpleType>
            <xs:complexType name="coded">
              <xs:simpleContent><xs:extension base="xs:string"><xs:attribute name="code" type="one"/></xs:extension></xs:simpleContent>
            </xs:complexType>
            <xs:complexType name="short">
              <xs:simpleContent><xs:restriction base="coded"><xs:maxLength value="2"/></xs:restriction></xs:simpleContent>
            </xs:complexType>
            <xs:element name="v" type="short"/>
            """), document).IsValid.Should().Be(valid);

    /// <summary>
    /// The rules that relate the facets of a type to those of its base are checked on the facets
    /// as written: a derived type may not allow more than its base.
    /// </summary>
    [Fact]
    public void A_schema_that_widens_a_length_is_still_refused()
    {
        var act = () => Compile("""
            <xs:simpleType name="five"><xs:restriction base="xs:string"><xs:maxLength value="5"/></xs:restriction></xs:simpleType>
            <xs:simpleType name="t"><xs:restriction base="five"><xs:maxLength value="9"/></xs:restriction></xs:simpleType>
            """);
        act.Should().Throw<SchemaCompilationException>();
    }

    /// <summary>The message names the facet, as it did, and not the pattern that stands for it.</summary>
    [Theory]
    [InlineData("<xs:maxLength value='2'/>", "abc", "The actual length is greater than the MaxLength value.")]
    [InlineData("<xs:maxLength value='2'/>", Face + Face + Face, "The actual length is greater than the MaxLength value.")]
    [InlineData("<xs:minLength value='2'/>", "a", "The actual length is less than the MinLength value.")]
    [InlineData("<xs:minLength value='2'/>", Face, "The actual length is less than the MinLength value.")]
    [InlineData("<xs:length value='2'/>", "abc", "The actual length is not equal to the specified length.")]
    [InlineData("<xs:pattern value='a+'/><xs:maxLength value='2'/>", "aaa", "The actual length is greater than the MaxLength value.")]
    [InlineData("<xs:pattern value='a+'/><xs:maxLength value='2'/>", "b", "The Pattern constraint failed.")]
    public void The_message_is_that_of_the_facet(string facets, string value, string words)
    {
        var result = Check(facets, value);
        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().EndWith(words);
    }

    [Theory]
    [InlineData("<xs:maxLength value='2'/>", "abc", "cvc-maxLength-valid")]
    [InlineData("<xs:minLength value='2'/>", "a", "cvc-minLength-valid")]
    [InlineData("<xs:length value='2'/>", "a", "cvc-length-valid")]
    [InlineData("<xs:pattern value='a+'/><xs:maxLength value='2'/>", "b", "cvc-pattern-valid")]
    public void And_the_rule_is_that_of_the_facet(string facets, string value, string rule)
        => Check(facets, value).Diagnostics.Should().ContainSingle().Which.Code.Should().Be(rule);

    [Fact]
    public void And_so_for_an_attribute()
    {
        var result = SchemaValidator.Validate(Compile("""
            <xs:element name="v">
              <xs:complexType>
                <xs:attribute name="code">
                  <xs:simpleType><xs:restriction base="xs:string"><xs:maxLength value="1"/></xs:restriction></xs:simpleType>
                </xs:attribute>
              </xs:complexType>
            </xs:element>
            """), "<v code='ab'/>");
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().EndWith("The actual length is greater than the MaxLength value.");
    }
}
