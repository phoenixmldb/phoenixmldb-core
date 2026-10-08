using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// <see cref="SchemaDiagnostic.Code"/> and <see cref="SchemaDiagnostic.MessageId"/>: which rule
/// of the XSD specification a validation error reports, taken from which message the schema
/// processor raised and not from the message's text.
/// </summary>
public sealed class SchemaDiagnosticCodeTests
{
    private static readonly Uri SchemaUri = new("urn:test:codes.xsd");

    private const string Xsd = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:simpleType name="small"><xs:restriction base="xs:integer"><xs:maxInclusive value="9"/><xs:minInclusive value="1"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="ex"><xs:restriction base="xs:integer"><xs:maxExclusive value="9"/><xs:minExclusive value="1"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="pat"><xs:restriction base="xs:string"><xs:pattern value="[a-z]+"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="en"><xs:restriction base="xs:string"><xs:enumeration value="a"/><xs:enumeration value="b"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="len"><xs:restriction base="xs:string"><xs:length value="3"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="minl"><xs:restriction base="xs:string"><xs:minLength value="3"/><xs:maxLength value="4"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="dig"><xs:restriction base="xs:decimal"><xs:totalDigits value="3"/><xs:fractionDigits value="1"/></xs:restriction></xs:simpleType>
          <xs:complexType name="abs" abstract="true"><xs:sequence/></xs:complexType>
          <xs:complexType name="conc"><xs:complexContent><xs:extension base="abs"/></xs:complexContent></xs:complexType>
          <xs:element name="absel" abstract="true"/>
          <xs:element name="root">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="a" type="xs:string"/>
                <xs:element name="b" type="xs:string" minOccurs="0"/>
              </xs:sequence>
              <xs:attribute name="req" type="xs:string" use="required"/>
              <xs:attribute name="fx" type="xs:string" fixed="F"/>
              <xs:attribute name="n" type="small"/>
            </xs:complexType>
          </xs:element>
          <xs:element name="v" type="small"/><xs:element name="vx" type="ex"/><xs:element name="p" type="pat"/><xs:element name="e" type="en"/>
          <xs:element name="l" type="len"/><xs:element name="ml" type="minl"/><xs:element name="d" type="dig"/><xs:element name="i" type="xs:integer"/>
          <xs:element name="fixed" type="xs:string" fixed="F"/>
          <xs:element name="nil" type="xs:string" nillable="true"/>
          <xs:element name="nonnil" type="xs:string"/>
          <xs:element name="empty"><xs:complexType/></xs:element>
          <xs:element name="eo"><xs:complexType><xs:sequence><xs:element name="a" type="xs:string" minOccurs="0"/></xs:sequence></xs:complexType></xs:element>
          <xs:element name="ab" type="abs"/>
          <xs:element name="ids"><xs:complexType><xs:sequence><xs:element name="x" maxOccurs="unbounded"><xs:complexType><xs:attribute name="id" type="xs:ID"/><xs:attribute name="ref" type="xs:IDREF"/></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element>
          <xs:element name="keys">
            <xs:complexType><xs:sequence><xs:element name="k" maxOccurs="unbounded"><xs:complexType><xs:attribute name="id" type="xs:string"/><xs:attribute name="r" type="xs:string"/></xs:complexType></xs:element></xs:sequence></xs:complexType>
            <xs:key name="kk"><xs:selector xpath="k"/><xs:field xpath="@id"/></xs:key>
            <xs:keyref name="kr" refer="kk"><xs:selector xpath="k"/><xs:field xpath="@r"/></xs:keyref>
          </xs:element>
          <xs:element name="uniq">
            <xs:complexType><xs:sequence><xs:element name="k" maxOccurs="unbounded"><xs:complexType><xs:attribute name="id" type="xs:string"/></xs:complexType></xs:element></xs:sequence></xs:complexType>
            <xs:unique name="uu"><xs:selector xpath="k"/><xs:field xpath="@id"/></xs:unique>
          </xs:element>
        </xs:schema>
        """;

    private const string Xsi = "xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'";

    private static readonly Lazy<CompiledSchema> Schema = new(() =>
        SchemaCompiler.Compile([SchemaUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(Xsd, SchemaUri)]));

    private static SchemaDiagnostic Only(string instance, SchemaValidationOptions? options = null)
        => SchemaValidator.Validate(Schema.Value, instance, options: options).Diagnostics.Should().ContainSingle().Subject;

    [Theory]
    [InlineData("<root req='x'><z/></root>", "cvc-complex-type.2.4.a", "Sch_InvalidElementContentExpecting")]
    [InlineData("<root req='x'/>", "cvc-complex-type.2.4.b", "Sch_IncompleteContentExpecting")]
    [InlineData("<root req='x'><a/><b/><b/></root>", "cvc-complex-type.2.4.d", "Sch_InvalidElementContent")]
    [InlineData("<root><a/></root>", "cvc-complex-type.4", "Sch_MissRequiredAttribute")]
    [InlineData("<root req='x' zz='1'><a/></root>", "cvc-complex-type.3.2.2", "Sch_UndeclaredAttribute")]
    [InlineData("<root req='x' fx='G'><a/></root>", "cvc-attribute.4", "Sch_FixedAttributeValue")]
    [InlineData("<root req='x'>text<a/></root>", "cvc-complex-type.2.3", "Sch_InvalidTextInElementExpecting")]
    [InlineData("<fixed>G</fixed>", "cvc-elt.5.2.2", "Sch_FixedElementValue")]
    [InlineData("<nonnil " + Xsi + " xsi:nil='true'/>", "cvc-elt.3.1", "Sch_InvalidXsiNill")]
    [InlineData("<nil " + Xsi + " xsi:nil='true'>x</nil>", "cvc-elt.3.2.1", "Sch_ContentInNill")]
    [InlineData("<empty><a/></empty>", "cvc-complex-type.2.1", "Sch_InvalidElementInEmptyEx")]
    [InlineData("<empty>x</empty>", "cvc-complex-type.2.1", "Sch_InvalidTextInEmpty")]
    [InlineData("<ids><x id='a'/><x id='a'/></ids>", "cvc-id.2", "Sch_DupId")]
    [InlineData("<ids><x ref='zz'/></ids>", "cvc-id.1", "Sch_UndeclaredId")]
    [InlineData("<keys><k id='1' r='9'/></keys>", "cvc-identity-constraint.4.3", "Sch_UnresolvedKeyref")]
    [InlineData("<keys><k/></keys>", "cvc-identity-constraint.4.2.1", "Sch_MissingKey")]
    public void A_structure_error_names_its_rule(string instance, string code, string messageId)
    {
        var diagnostic = Only(instance);
        diagnostic.Code.Should().Be(code);
        diagnostic.MessageId.Should().Be(messageId);
    }

    [Theory]
    [InlineData("<v>99</v>", "cvc-maxInclusive-valid")]
    [InlineData("<v>0</v>", "cvc-minInclusive-valid")]
    [InlineData("<vx>9</vx>", "cvc-maxExclusive-valid")]
    [InlineData("<vx>1</vx>", "cvc-minExclusive-valid")]
    [InlineData("<p>A1</p>", "cvc-pattern-valid")]
    [InlineData("<e>z</e>", "cvc-enumeration-valid")]
    [InlineData("<l>ab</l>", "cvc-length-valid")]
    [InlineData("<ml>a</ml>", "cvc-minLength-valid")]
    [InlineData("<ml>abcde</ml>", "cvc-maxLength-valid")]
    [InlineData("<d>1234</d>", "cvc-totalDigits-valid")]
    [InlineData("<d>1.23</d>", "cvc-fractionDigits-valid")]
    // Not in the type's lexical space at all.
    [InlineData("<i>abc</i>", "cvc-datatype-valid.1.2.1")]
    public void A_value_error_names_the_facet_it_breaks(string instance, string code)
    {
        var diagnostic = Only(instance);
        diagnostic.Code.Should().Be(code);
        diagnostic.MessageId.Should().Be("Sch_ElementValueDataTypeDetailed");
    }

    [Fact]
    public void An_attribute_value_error_names_the_facet_too()
    {
        var diagnostic = Only("<root req='x' n='99'><a/></root>");
        diagnostic.Code.Should().Be("cvc-maxInclusive-valid");
        diagnostic.MessageId.Should().Be("Sch_AttributeValueDataTypeDetailed");
    }

    // An xsi:type that cannot be used leaves the element with no type, which the processor
    // reports as well: the first diagnostic is the one about the xsi:type.
    [Theory]
    [InlineData("<ab " + Xsi + " xsi:type='nope'/>", "cvc-elt.4.2", "Sch_XsiTypeNotFound")]
    [InlineData("<i " + Xsi + " xsi:type='pat'>a</i>", "cvc-elt.4.3", "Sch_XsiTypeBlockedEx")]
    public void An_xsi_type_error_names_its_rule(string instance, string code, string messageId)
    {
        var diagnostic = SchemaValidator.Validate(Schema.Value, instance).Diagnostics[0];
        diagnostic.Code.Should().Be(code);
        diagnostic.MessageId.Should().Be(messageId);
    }

    [Fact]
    public void An_undeclared_document_element_is_cvc_elt_1_and_is_reported_once()
    {
        // The schema covers the element's namespace, so the processor reports it.
        var byProcessor = Only("<nope/>");
        byProcessor.Code.Should().Be("cvc-elt.1");
        byProcessor.MessageId.Should().Be("Sch_UndeclaredElement");

        // It covers no part of this namespace, and the processor says nothing: the layer does.
        var byLayer = Only("<nope xmlns='urn:elsewhere'/>");
        byLayer.Code.Should().Be("cvc-elt.1");
        byLayer.MessageId.Should().BeNull();
    }

    [Theory]
    // One message for a duplicate key (4.2.2) and a duplicate unique value (4.1).
    [InlineData("<keys><k id='1'/><k id='1'/></keys>", "Sch_DuplicateKey")]
    [InlineData("<uniq><k id='1'/><k id='1'/></uniq>", "Sch_DuplicateKey")]
    // One message for an abstract element (cvc-elt.2) and an abstract type (cvc-type.2).
    [InlineData("<ab/>", "Sch_AbstractElement")]
    [InlineData("<absel/>", "Sch_AbstractElement")]
    public void A_message_that_covers_two_rules_has_an_id_and_no_code(string instance, string messageId)
    {
        var diagnostic = Only(instance);
        diagnostic.MessageId.Should().Be(messageId);
        diagnostic.Code.Should().BeNull();
    }

    [Fact]
    public void The_same_kind_of_error_has_the_same_id_whatever_its_text_says()
    {
        var first = Only("<v>99</v>");
        var second = Only("<vx>100</vx>");
        second.Message.Should().NotBe(first.Message);
        second.MessageId.Should().Be(first.MessageId);
    }

    [Fact]
    public void A_document_that_is_not_well_formed_has_neither()
    {
        var diagnostic = Only("<root req='x'><a></root>");
        diagnostic.Code.Should().BeNull();
        diagnostic.MessageId.Should().BeNull();
    }

    [Fact]
    public void A_compilation_error_has_an_id_and_no_code()
    {
        var uri = new Uri("urn:test:broken.xsd");
        var act = () => SchemaCompiler.Compile([uri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText("""<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="a" type="nope"/></xs:schema>""", uri)]);
        var diagnostic = act.Should().Throw<SchemaCompilationException>().Which.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.MessageId.Should().StartWith("Sch_");
        diagnostic.Code.Should().BeNull();
    }
}
