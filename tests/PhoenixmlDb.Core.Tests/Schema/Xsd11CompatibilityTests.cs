using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// A schema written for XSD 1.1 loads on the XSD 1.0 processor the layer uses: the parts it marks
/// as 1.1-only are left out (XSD 1.1 §4.2.2), and the built-in types 1.1 added read as the 1.0
/// types they restrict.
/// </summary>
public sealed class Xsd11CompatibilityTests
{
    private static readonly Uri RootUri = new("mem://test/vc/main.xsd");
    private static readonly Uri PartUri = new("mem://test/vc/part.xsd");
    private static readonly SchemaCompileOptions Off = new() { Xsd11Compatibility = false };

    private static string Schema(string body) => $"""
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:vc="http://www.w3.org/2007/XMLSchema-versioning"
                   targetNamespace="urn:v" xmlns="urn:v" elementFormDefault="qualified">
        {body}
        </xs:schema>
        """;

    private static CompiledSchema Compile(string schema, SchemaCompileOptions? options = null, params SchemaSource[] more)
        => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(schema, RootUri), .. more], options);

    private static bool Valid(CompiledSchema schema, string instance) => SchemaValidator.Validate(schema, instance).IsValid;

    private const string WithAssert = """
        <xs:element name="range">
          <xs:complexType>
            <xs:attribute name="min" type="xs:integer"/>
            <xs:attribute name="max" type="xs:integer"/>
            <xs:assert test="@min le @max" vc:minVersion="1.1"/>
          </xs:complexType>
        </xs:element>
        """;

    [Fact]
    public void A_part_marked_for_1_1_and_later_is_left_out()
    {
        var schema = Compile(Schema(WithAssert));
        Valid(schema, "<range xmlns='urn:v' min='1' max='2'/>").Should().BeTrue();
        Valid(schema, "<range xmlns='urn:v' min='x'/>").Should().BeFalse();
    }

    [Fact]
    public void Without_the_option_such_a_schema_does_not_compile()
        => FluentActions.Invoking(() => Compile(Schema(WithAssert), Off)).Should().Throw<SchemaCompilationException>();

    /// <summary>
    /// minVersion is inclusive and maxVersion exclusive: for a 1.0 processor an element stays when
    /// minVersion ≤ 1.0 &lt; maxVersion.
    /// </summary>
    [Theory]
    [InlineData("vc:minVersion='1.0'", true)]
    [InlineData("vc:minVersion='1.1'", false)]
    [InlineData("vc:maxVersion='1.1'", true)]
    [InlineData("vc:maxVersion='1.0'", false)]
    [InlineData("vc:minVersion='1.0' vc:maxVersion='1.1'", true)]
    [InlineData("vc:minVersion='1.1' vc:maxVersion='3.0'", false)]
    [InlineData("", true)]
    public void An_element_is_kept_when_version_1_0_is_in_its_range(string versioning, bool kept)
    {
        var schema = Compile(Schema($"<xs:element name='a' type='xs:string' {versioning}/><xs:element name='always' type='xs:string'/>"));
        Valid(schema, "<always xmlns='urn:v'/>").Should().BeTrue();
        Valid(schema, "<a xmlns='urn:v'/>").Should().Be(kept);
    }

    /// <summary>Two declarations of one element, one for each version; a 1.0 processor sees one.</summary>
    [Fact]
    public void Alternatives_for_the_two_versions_do_not_collide()
    {
        var schema = Compile(Schema("""
            <xs:element name="d" type="xs:string" vc:maxVersion="1.1"/>
            <xs:element name="d" type="xs:integer" vc:minVersion="1.1"/>
            """));
        Valid(schema, "<d xmlns='urn:v'>text</d>").Should().BeTrue();
    }

    [Fact]
    public void A_built_in_type_that_1_1_added_reads_as_the_type_it_restricts()
    {
        var schema = Compile(Schema("""
            <xs:element name="wait" type="xs:dayTimeDuration"/>
            <xs:element name="at" type="xs:dateTimeStamp"/>
            <xs:simpleType name="short"><xs:restriction base="xs:yearMonthDuration"/></xs:simpleType>
            <xs:element name="term" type="short"/>
            <xs:simpleType name="waits"><xs:list itemType="xs:dayTimeDuration"/></xs:simpleType>
            <xs:element name="waits" type="waits"/>
            """));
        Valid(schema, "<wait xmlns='urn:v'>P1DT2H</wait>").Should().BeTrue();
        Valid(schema, "<wait xmlns='urn:v'>soon</wait>").Should().BeFalse();
        Valid(schema, "<at xmlns='urn:v'>2026-10-07T12:00:00Z</at>").Should().BeTrue();
        Valid(schema, "<term xmlns='urn:v'>P2Y</term>").Should().BeTrue();
        Valid(schema, "<waits xmlns='urn:v'>P1D PT1H</waits>").Should().BeTrue();
        Valid(schema, "<waits xmlns='urn:v'>P1D later</waits>").Should().BeFalse();
        // The limit of the mapping: the 1.1 type's own constraint is not applied.
        Valid(schema, "<wait xmlns='urn:v'>P1Y</wait>").Should().BeTrue();
    }

    [Fact]
    public void A_type_of_the_same_local_name_in_another_namespace_is_left_alone()
    {
        var schema = Compile(Schema("""
            <xs:simpleType name="dayTimeDuration"><xs:restriction base="xs:integer"/></xs:simpleType>
            <xs:element name="own" type="dayTimeDuration"/>
            """));
        Valid(schema, "<own xmlns='urn:v'>5</own>").Should().BeTrue();
        Valid(schema, "<own xmlns='urn:v'>P1D</own>").Should().BeFalse();
    }

    private sealed class CountingGate : ISchemaAccessGate
    {
        public List<string> Asked { get; } = [];
        public string Identity => "counting";
        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request.Location);
            return default;
        }
        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken) => default;
    }

    /// <summary>An include the document marks as 1.1-only is not part of the schema, so it is not fetched.</summary>
    [Fact]
    public void A_reference_that_is_left_out_is_not_fetched()
    {
        var gate = new CountingGate();
        var schema = SchemaCompiler.Compile([RootUri], gate, [SchemaSource.FromText(Schema("""
            <xs:include schemaLocation="only-1.1.xsd" vc:minVersion="1.1"/>
            <xs:element name="a" type="xs:string"/>
            """), RootUri)]);
        gate.Asked.Should().BeEmpty();
        schema.Documents.Should().ContainSingle();
    }

    [Fact]
    public void An_included_document_is_treated_the_same_way()
    {
        var schema = Compile(Schema("<xs:include schemaLocation='part.xsd'/><xs:element name='wait' type='waitType'/>"), null,
            SchemaSource.FromText(Schema("""
                <xs:simpleType name="waitType"><xs:restriction base="xs:dayTimeDuration"/></xs:simpleType>
                <xs:element name="gone" type="xs:string" vc:minVersion="1.1"/>
                """), PartUri));
        Valid(schema, "<wait xmlns='urn:v'>PT5M</wait>").Should().BeTrue();
        Valid(schema, "<gone xmlns='urn:v'/>").Should().BeFalse();
    }

    [Fact]
    public void A_document_in_utf_16_is_handled()
    {
        var text = "<?xml version=\"1.0\" encoding=\"utf-16\"?>" + Schema(WithAssert);
        var bytes = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(text)).ToArray();
        var schema = SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromStore(RootUri, _ => new(new MemoryStream(bytes)), "1")]);
        Valid(schema, "<range xmlns='urn:v' min='1' max='2'/>").Should().BeTrue();
    }

    /// <summary>A document with nothing to rewrite is compiled as it was read, so its positions are its own.</summary>
    [Fact]
    public void A_document_that_needs_nothing_keeps_its_line_numbers()
    {
        const string broken = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:v'>\n\n\n  <xs:element name='b' type='nosuch'/>\n</xs:schema>";
        var act = () => Compile(broken);
        act.Should().Throw<SchemaCompilationException>().Which.Diagnostics
            .Should().Contain(d => d.Severity == SchemaSeverity.Error).And.OnlyContain(d => d.Line == 4);
    }

    [Fact]
    public async Task The_option_is_part_of_what_a_cached_schema_is_found_by()
    {
        var cache = new SchemaCache();
        var sources = new[] { SchemaSource.FromText(Schema("<xs:element name='a' type='xs:string'/>"), RootUri) };
        var on = await cache.GetAsync([RootUri], SchemaAccessGate.SuppliedOnly, sources);
        var off = await cache.GetAsync([RootUri], SchemaAccessGate.SuppliedOnly, sources, Off);
        off.Should().NotBeSameAs(on);
    }

    // ── a schema that cannot be read as XSD 1.0 at all ──

    /// <summary>
    /// The W3C schema for XSLT 3.0 is written this way. With the root gone nothing is left, and
    /// the schema parser said "Root element is missing".
    /// </summary>
    [Fact]
    public void A_schema_whose_root_is_marked_for_1_1_says_that_it_requires_1_1()
    {
        const string schema = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:vc="http://www.w3.org/2007/XMLSchema-versioning"
                       targetNamespace="urn:v" vc:minVersion="1.1">
              <xs:element name="e" type="xs:string"/>
            </xs:schema>
            """;

        FluentActions.Invoking(() => Compile(schema)).Should().Throw<SchemaCompilationException>()
            .WithMessage("*mem://test/vc/main.xsd*requires XSD 1.1*vc:minVersion=\"1.1\"*XSD 1.0*");
    }

    [Theory]
    [InlineData("""<xs:complexType name="t"><xs:attribute name="a" type="xs:integer"/><xs:assert test="@a gt 0"/></xs:complexType>""", "xs:assert (1)")]
    [InlineData("""<xs:simpleType name="t"><xs:restriction base="xs:integer"><xs:assertion test="$value gt 0"/></xs:restriction></xs:simpleType>""", "xs:assertion (1)")]
    [InlineData("""<xs:element name="e" type="xs:string"><xs:alternative test="@k = 1" type="xs:integer"/></xs:element>""", "xs:alternative (1)")]
    [InlineData("""<xs:complexType name="t"><xs:openContent><xs:any/></xs:openContent><xs:sequence/></xs:complexType>""", "xs:openContent (1)")]
    [InlineData("""<xs:override schemaLocation="part.xsd"/>""", "xs:override (1)")]
    public void A_1_1_construct_with_no_guard_is_named(string body, string named)
        => FluentActions.Invoking(() => Compile(Schema(body))).Should().Throw<SchemaCompilationException>()
            .WithMessage($"*requires XSD 1.1*{named}*no vc:minVersion guard*");

    [Fact]
    public void The_message_counts_each_construct()
        => FluentActions.Invoking(() => Compile(Schema("""
            <xs:complexType name="a"><xs:assert test="true()"/><xs:assert test="true()"/></xs:complexType>
            <xs:complexType name="b"><xs:openContent><xs:any/></xs:openContent><xs:sequence/></xs:complexType>
            """))).Should().Throw<SchemaCompilationException>().WithMessage("*xs:assert (2), xs:openContent (1)*");

    /// <summary>A word of the 1.1 vocabulary in text or in a name is not a construct.</summary>
    [Fact]
    public void A_schema_that_only_mentions_such_a_name_loads()
    {
        var schema = Compile(Schema("""
            <xs:element name="assert" type="xs:string">
              <xs:annotation><xs:documentation>An override of the alternative, see openContent.</xs:documentation></xs:annotation>
            </xs:element>
            """));

        Valid(schema, "<assert xmlns='urn:v'>x</assert>").Should().BeTrue();
    }

    [Fact]
    public void An_included_document_that_requires_1_1_is_the_one_named()
    {
        const string part = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:v">
              <xs:complexType name="t"><xs:assert test="true()"/></xs:complexType>
            </xs:schema>
            """;

        FluentActions.Invoking(() => Compile(Schema("""<xs:include schemaLocation="part.xsd"/>"""), null, SchemaSource.FromText(part, PartUri)))
            .Should().Throw<SchemaCompilationException>().WithMessage("*mem://test/vc/part.xsd*requires XSD 1.1*xs:assert (1)*");
    }
}
