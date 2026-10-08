using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary><see cref="SchemaValidator"/>: one validation call, one result shape.</summary>
public sealed class SchemaValidatorTests
{
    private static readonly Uri SchemaUri = new("mem://test/v/order.xsd");
    private static readonly Uri InstanceUri = new("mem://test/v/order.xml");

    private const string Schema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:o" xmlns="urn:o" elementFormDefault="qualified">
          <xs:simpleType name="size"><xs:restriction base="xs:integer"><xs:maxInclusive value="9"/></xs:restriction></xs:simpleType>
          <xs:element name="order">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="item" maxOccurs="unbounded">
                  <xs:complexType>
                    <xs:sequence><xs:element name="size" type="size"/><xs:element name="ref" type="xs:string" minOccurs="0"/></xs:sequence>
                    <xs:attribute name="id" type="xs:string" use="required"/>
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
            </xs:complexType>
            <xs:key name="ids"><xs:selector xpath="*"/><xs:field xpath="@id"/></xs:key>
          </xs:element>
        </xs:schema>
        """;

    private static readonly CompiledSchema Compiled =
        SchemaCompiler.Compile([SchemaUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(Schema, SchemaUri)]);

    private static string Order(string body) => $"<order xmlns=\"urn:o\">\n{body}\n</order>";

    private const string GoodItem = """<item id="a"><size>3</size></item>""";

    [Fact]
    public void A_valid_document_has_no_diagnostics()
    {
        var result = SchemaValidator.Validate(Compiled, Order(GoodItem), InstanceUri);
        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public void An_error_says_what_where_and_in_which_document()
    {
        var result = SchemaValidator.Validate(Compiled, Order("""<item id="a"><size>12</size></item>"""), InstanceUri);
        result.IsValid.Should().BeFalse();
        var error = result.Diagnostics.Should().ContainSingle().Which;
        error.Severity.Should().Be(SchemaSeverity.Error);
        error.SourceUri.Should().Be(InstanceUri.AbsoluteUri);
        error.Line.Should().Be(2);
        error.Column.Should().BeGreaterThan(0);
        error.Message.Should().Contain("size");
    }

    [Fact]
    public void Every_error_is_reported_in_document_order()
    {
        var result = SchemaValidator.Validate(Compiled,
            Order("<item id=\"a\"><size>12</size></item>\n<item><size>x</size></item>"));
        // The size out of range; then for the second item the missing id, its size that is not a
        // number, and the key that has no value without the id.
        result.Diagnostics.Should().HaveCount(4);
        result.Diagnostics.Select(d => d.Line).Should().BeInAscendingOrder();
        result.Diagnostics.Should().OnlyContain(d => d.SourceUri == null);
    }

    /// <summary>
    /// The schema processor says nothing about an element it has no declaration for. Validated
    /// against a schema for another vocabulary, any document would be valid.
    /// </summary>
    [Theory]
    [InlineData("<invoice xmlns='urn:other'><anything/></invoice>", "'invoice' in namespace 'urn:other'")]
    [InlineData("<order><item/></order>", "'order' in no namespace")]
    public void A_document_element_the_schema_does_not_declare_is_an_error(string document, string named)
    {
        var result = SchemaValidator.Validate(Compiled, document);
        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().Contain("no declaration").And.Contain(named);

        SchemaValidator.Validate(Compiled, document, options: new SchemaValidationOptions { RequireDeclaredRoot = false })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Identity_constraints_are_checked_unless_turned_off()
    {
        var twice = Order(GoodItem + GoodItem);
        SchemaValidator.Validate(Compiled, twice).IsValid.Should().BeFalse();
        SchemaValidator.Validate(Compiled, twice, options: new SchemaValidationOptions { CheckIdentityConstraints = false })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_document_that_is_not_well_formed_is_an_error_and_cut_short()
    {
        var result = SchemaValidator.Validate(Compiled, "<order xmlns='urn:o'><item id='a'><size>3</size></item>", InstanceUri);
        result.IsValid.Should().BeFalse();
        result.Truncated.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Severity == SchemaSeverity.Error && d.Line > 0);
    }

    [Fact]
    public void A_document_type_declaration_is_refused()
    {
        var result = SchemaValidator.Validate(Compiled, "<!DOCTYPE order [<!ENTITY e 'x'>]>" + Order(GoodItem));
        result.IsValid.Should().BeFalse();
        result.Truncated.Should().BeTrue();
    }

    /// <summary>
    /// The instance names a schema of its own that would accept it. It is not followed: the
    /// element is still judged by the schema that was passed in.
    /// </summary>
    [Fact]
    public void What_the_instance_says_about_its_schema_is_not_followed()
    {
        var hinted = """
            <order xmlns="urn:o" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                   xsi:schemaLocation="urn:o file:///nonexistent/permissive.xsd">
            <item id="a"><size>12</size></item>
            </order>
            """;
        var result = SchemaValidator.Validate(Compiled, hinted);
        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().Contain("size");
    }

    [Fact]
    public void Diagnostics_stop_at_the_limit_and_the_result_says_so()
    {
        var many = Order(string.Concat(Enumerable.Range(0, 50).Select(i => $"<item id=\"i{i}\"><size>99</size></item>\n")));
        var all = SchemaValidator.Validate(Compiled, many);
        all.Diagnostics.Should().HaveCount(50);
        all.Truncated.Should().BeFalse();

        var some = SchemaValidator.Validate(Compiled, many, options: new SchemaValidationOptions { MaxDiagnostics = 5 });
        some.Diagnostics.Should().HaveCount(5);
        some.Truncated.Should().BeTrue();
        some.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void Depth_is_limited_when_the_host_sets_a_limit(int maxDepth, bool valid)
    {
        // order (0) > item (1) > size (2)
        var result = SchemaValidator.Validate(Compiled, Order(GoodItem), options: new SchemaValidationOptions { MaxDepth = maxDepth });
        result.IsValid.Should().Be(valid);
        result.Truncated.Should().Be(!valid);
        if (!valid)
            result.Diagnostics.Should().ContainSingle().Which.Message.Should().Contain("levels deep");
    }

    [Fact]
    public void A_cancelled_token_stops_the_validation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        FluentActions.Invoking(() => SchemaValidator.Validate(Compiled, Order(GoodItem), cancellationToken: cts.Token))
            .Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void A_stream_and_a_reader_validate_as_the_text_does()
    {
        var text = Order("""<item id="a"><size>12</size></item>""");
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        using var reader = new StringReader(text);
        var expected = SchemaValidator.Validate(Compiled, text, InstanceUri).Diagnostics;
        SchemaValidator.Validate(Compiled, stream, InstanceUri).Diagnostics.Should().Equal(expected);
        SchemaValidator.Validate(Compiled, reader, InstanceUri).Diagnostics.Should().Equal(expected);
        stream.CanRead.Should().BeTrue("the caller's stream is the caller's to close");
    }

    /// <summary>
    /// A stored schema is named by whatever absolute URI its host gives it, and the layer applies
    /// only standard relative resolution to it: a reference keeps the path it is under, and an
    /// "@" or a number in that path means nothing to the layer.
    /// </summary>
    [Fact]
    public void A_relative_reference_keeps_the_path_prefix_of_an_opaque_uri()
    {
        var root = new Uri("stored:///@orders.xsd/3/orders.xsd");
        const string rootText = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:s" xmlns="urn:s">
              <xs:include schemaLocation="common.xsd"/>
              <xs:include schemaLocation="parts/more.xsd"/>
              <xs:element name="s" type="size"/>
            </xs:schema>
            """;
        const string common = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:s" xmlns="urn:s">
              <xs:simpleType name="size"><xs:restriction base="xs:integer"/></xs:simpleType>
            </xs:schema>
            """;
        const string more = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:s" xmlns="urn:s">
              <xs:include schemaLocation="../common.xsd"/>
            </xs:schema>
            """;
        var schema = SchemaCompiler.Compile([root], SchemaAccessGate.SuppliedOnly,
        [
            SchemaSource.FromStore(root, _ => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rootText))), "3"),
            SchemaSource.FromText(common, new Uri("stored:///@orders.xsd/3/common.xsd")),
            SchemaSource.FromText(more, new Uri("stored:///@orders.xsd/3/parts/more.xsd")),
        ]);

        schema.Documents.Select(d => d.Uri.AbsoluteUri).Should().BeEquivalentTo(
            "stored:///@orders.xsd/3/orders.xsd", "stored:///@orders.xsd/3/common.xsd", "stored:///@orders.xsd/3/parts/more.xsd");
        SchemaValidator.Validate(schema, "<s xmlns='urn:s'>4</s>").IsValid.Should().BeTrue();
    }
}
