using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

#pragma warning disable CA2007 // xUnit runs tests without a synchronization context

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// <see cref="XmlCatalog"/> (OASIS XML Catalogs 1.1), and a schema compiled through one: a name
/// is mapped, and what it is mapped to is read from a source or through the gate.
/// </summary>
public sealed class XmlCatalogTests
{
    private static readonly Uri CatalogUri = new("mem://test/cat/catalog.xml");

    private static string Catalog(string body, string attributes = "")
        => $"<catalog xmlns=\"urn:oasis:names:tc:entity:xmlns:xml:catalog\" {attributes}>\n{body}\n</catalog>";

    private static XmlCatalog Load(string catalog, params SchemaSource[] more)
        => XmlCatalog.Load([CatalogUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(catalog, CatalogUri), .. more]);

    [Fact]
    public void An_exact_name_is_mapped_and_a_relative_target_resolves_against_the_catalog()
    {
        var catalog = Load(Catalog("""
            <uri name="http://example.org/a.xsd" uri="local/a.xsd"/>
            <system systemId="http://example.org/b.xsd" uri="file:///abs/b.xsd"/>
            """));
        catalog.ResolveUri("http://example.org/a.xsd").Should().Be(new Uri("mem://test/cat/local/a.xsd"));
        catalog.ResolveUri("http://example.org/b.xsd").Should().Be(new Uri("file:///abs/b.xsd"));
        catalog.ResolveUri("http://example.org/c.xsd").Should().BeNull();
        catalog.ResolveUri("http://example.org/A.xsd").Should().BeNull("names compare exactly");
    }

    [Fact]
    public void An_exact_name_wins_over_a_rewrite_and_the_longest_rewrite_prefix_wins()
    {
        var catalog = Load(Catalog("""
            <rewriteURI uriStartString="http://example.org/" rewritePrefix="file:///short/"/>
            <rewriteURI uriStartString="http://example.org/schemas/" rewritePrefix="file:///long/"/>
            <uri name="http://example.org/schemas/exact.xsd" uri="file:///exact.xsd"/>
            <rewriteSystem systemIdStartString="urn:sys:" rewritePrefix="file:///sys/"/>
            """));
        catalog.ResolveUri("http://example.org/schemas/exact.xsd").Should().Be(new Uri("file:///exact.xsd"));
        catalog.ResolveUri("http://example.org/schemas/x/y.xsd").Should().Be(new Uri("file:///long/x/y.xsd"));
        catalog.ResolveUri("http://example.org/other.xsd").Should().Be(new Uri("file:///short/other.xsd"));
        catalog.ResolveUri("urn:sys:z.xsd").Should().Be(new Uri("file:///sys/z.xsd"));
    }

    [Fact]
    public void A_suffix_is_tried_after_the_rewrites_and_the_longest_wins()
    {
        var catalog = Load(Catalog("""
            <uriSuffix uriSuffix="common.xsd" uri="file:///any-common.xsd"/>
            <uriSuffix uriSuffix="/v2/common.xsd" uri="file:///v2-common.xsd"/>
            <systemSuffix systemIdSuffix="types.xsd" uri="file:///types.xsd"/>
            <rewriteURI uriStartString="http://rewritten.example/" rewritePrefix="file:///r/"/>
            """));
        catalog.ResolveUri("http://x.example/v2/common.xsd").Should().Be(new Uri("file:///v2-common.xsd"));
        catalog.ResolveUri("http://x.example/v1/common.xsd").Should().Be(new Uri("file:///any-common.xsd"));
        catalog.ResolveUri("http://x.example/types.xsd").Should().Be(new Uri("file:///types.xsd"));
        catalog.ResolveUri("http://rewritten.example/common.xsd").Should().Be(new Uri("file:///r/common.xsd"));
    }

    [Fact]
    public void A_public_identifier_compares_with_its_white_space_normalized()
    {
        var catalog = Load(Catalog("""<public publicId="-//Example//DTD  Thing   1.0//EN" uri="thing.dtd"/>"""));
        catalog.ResolvePublic(" -//Example//DTD Thing 1.0//EN\n").Should().Be(new Uri("mem://test/cat/thing.dtd"));
        catalog.ResolvePublic("-//Example//DTD Other//EN").Should().BeNull();
    }

    [Fact]
    public void Xml_base_on_the_catalog_a_group_and_an_entry_moves_the_targets()
    {
        var catalog = Load(Catalog("""
            <uri name="a" uri="a.xsd"/>
            <group xml:base="grouped/">
              <uri name="b" uri="b.xsd"/>
              <uri name="c" uri="c.xsd" xml:base="file:///entry/"/>
            </group>
            <uri name="d" uri="d.xsd"/>
            """, attributes: "xml:base=\"http://base.example/root/\""));
        catalog.ResolveUri("a").Should().Be(new Uri("http://base.example/root/a.xsd"));
        catalog.ResolveUri("b").Should().Be(new Uri("http://base.example/root/grouped/b.xsd"));
        catalog.ResolveUri("c").Should().Be(new Uri("file:///entry/c.xsd"));
        catalog.ResolveUri("d").Should().Be(new Uri("http://base.example/root/d.xsd"), "the group's base ends with the group");
    }

    [Fact]
    public void A_files_own_entries_are_tried_before_the_catalogs_it_names_as_next()
    {
        var second = new Uri("mem://test/cat/second.xml");
        var third = new Uri("mem://test/cat/third.xml");
        var catalog = Load(
            Catalog("""
                <nextCatalog catalog="second.xml"/>
                <nextCatalog catalog="third.xml"/>
                <uri name="both" uri="file:///first"/>
                """),
            SchemaSource.FromText(Catalog("""<uri name="both" uri="file:///second"/><uri name="later" uri="file:///second-later"/>"""), second),
            SchemaSource.FromText(Catalog("""<uri name="later" uri="file:///third-later"/><uri name="only" uri="file:///third-only"/>"""), third));
        catalog.ResolveUri("both").Should().Be(new Uri("file:///first"));
        catalog.ResolveUri("later").Should().Be(new Uri("file:///second-later"));
        catalog.ResolveUri("only").Should().Be(new Uri("file:///third-only"));
        catalog.Documents.Select(d => d.Uri).Should().Equal(CatalogUri, second, third);
    }

    [Fact]
    public void A_catalog_that_names_itself_is_read_once()
    {
        var catalog = Load(Catalog("""<nextCatalog catalog="catalog.xml"/><uri name="a" uri="file:///a"/>"""));
        catalog.Documents.Should().ContainSingle();
        catalog.ResolveUri("a").Should().Be(new Uri("file:///a"));
    }

    /// <summary>Most catalog files carry the OASIS document type declaration. Nothing is fetched for it.</summary>
    [Fact]
    public void A_catalog_with_the_usual_document_type_declaration_loads()
    {
        var catalog = Load("""
            <?xml version="1.0"?>
            <!DOCTYPE catalog PUBLIC "-//OASIS//DTD XML Catalogs V1.1//EN" "http://www.oasis-open.org/committees/entity/release/1.1/catalog.dtd">
            """ + Catalog("""<uri name="a" uri="file:///a"/>"""));
        catalog.ResolveUri("a").Should().Be(new Uri("file:///a"));
    }

    [Fact]
    public void Entries_the_layer_does_not_support_and_foreign_elements_are_ignored()
    {
        var catalog = Load(Catalog("""
            <delegateURI uriStartString="http://d.example/" catalog="delegated.xml"/>
            <other xmlns="urn:not-a-catalog" name="a" uri="file:///wrong"/>
            <uri name="a" uri="file:///a"/>
            """));
        catalog.ResolveUri("a").Should().Be(new Uri("file:///a"));
        catalog.ResolveUri("http://d.example/x").Should().BeNull();
        catalog.Documents.Should().ContainSingle("the delegated catalog is not read");
    }

    [Fact]
    public void A_next_catalog_that_cannot_be_read_fails_the_load()
        => FluentActions.Invoking(() => Load(Catalog("""<nextCatalog catalog="missing.xml"/>""")))
            .Should().Throw<SchemaCompilationException>().WithMessage("*'missing.xml' is not available*");

    [Fact]
    public void A_catalog_that_is_not_well_formed_fails_the_load()
        => FluentActions.Invoking(() => Load("<catalog xmlns='urn:oasis:names:tc:entity:xmlns:xml:catalog'><uri"))
            .Should().Throw<SchemaCompilationException>().WithMessage("*not well-formed*");

    [Fact]
    public void The_identity_follows_the_entries_and_their_order()
    {
        const string one = """<uri name="a" uri="file:///a"/><uri name="b" uri="file:///b"/>""";
        Load(Catalog(one)).Identity.Should().Be(Load(Catalog(one + "<!-- a comment -->")).Identity);
        Load(Catalog(one)).Identity.Should().NotBe(Load(Catalog("""<uri name="b" uri="file:///b"/><uri name="a" uri="file:///a"/>""")).Identity);
        Load(Catalog(one)).Identity.Should().NotBe(XmlCatalog.Empty.Identity);
    }

    // ---- a schema compiled through a catalog ----

    private static readonly Uri RootUri = new("mem://test/schemas/main.xsd");
    private static readonly Uri LocalXml = new("mem://test/local/w3c/xml.xsd");

    private const string XmlNamespaceSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="http://www.w3.org/XML/1998/namespace">
          <xs:include schemaLocation="xml-parts.xsd"/>
        </xs:schema>
        """;

    private const string XmlParts = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="http://www.w3.org/XML/1998/namespace">
          <xs:attribute name="lang" type="xs:language"/>
        </xs:schema>
        """;

    private static string Importing(string import) => $"""
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:c" xmlns="urn:c" elementFormDefault="qualified">
          {import}
          <xs:element name="p"><xs:complexType><xs:attribute ref="xml:lang" use="required"/></xs:complexType></xs:element>
        </xs:schema>
        """;

    private sealed class RefusingGate : ISchemaAccessGate
    {
        public List<Uri> Asked { get; } = [];
        public string Identity => "refusing";
        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request.Uri);
            return default;
        }
        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken) => default;
    }

    private static readonly SchemaSource[] LocalCopies =
    [
        SchemaSource.FromText(XmlNamespaceSchema, LocalXml),
        SchemaSource.FromText(XmlParts, new Uri("mem://test/local/w3c/xml-parts.xsd")),
    ];

    private static readonly XmlCatalog ToLocal = Load(Catalog(
        """<uri name="http://www.w3.org/2001/xml.xsd" uri="mem://test/local/w3c/xml.xsd"/>"""));

    /// <summary>
    /// The schema names a document on the network. The catalog maps it to a local copy, the gate
    /// is never asked for the network address, and the copy's own relative include is found next
    /// to the copy.
    /// </summary>
    [Fact]
    public void A_mapped_location_is_read_from_where_the_catalog_says()
    {
        var gate = new RefusingGate();
        var schema = SchemaCompiler.Compile([RootUri], gate,
            [SchemaSource.FromText(Importing("""<xs:import namespace="http://www.w3.org/XML/1998/namespace" schemaLocation="http://www.w3.org/2001/xml.xsd"/>"""), RootUri), .. LocalCopies],
            new SchemaCompileOptions { Catalog = ToLocal });

        gate.Asked.Should().BeEmpty();
        schema.Documents.Select(d => d.Uri.AbsoluteUri).Should().BeEquivalentTo(
            RootUri.AbsoluteUri, "mem://test/local/w3c/xml.xsd", "mem://test/local/w3c/xml-parts.xsd");
        SchemaValidator.Validate(schema, "<p xmlns='urn:c' xml:lang='en'/>").IsValid.Should().BeTrue();
        SchemaValidator.Validate(schema, "<p xmlns='urn:c' xml:lang='not a language'/>").IsValid.Should().BeFalse();
    }

    [Fact]
    public void Without_the_catalog_the_same_schema_asks_the_gate_for_the_network_address()
    {
        var gate = new RefusingGate();
        var act = () => SchemaCompiler.Compile([RootUri], gate,
            [SchemaSource.FromText(Importing("""<xs:import namespace="http://www.w3.org/XML/1998/namespace" schemaLocation="http://www.w3.org/2001/xml.xsd"/>"""), RootUri), .. LocalCopies]);
        act.Should().Throw<SchemaCompilationException>();
        gate.Asked.Should().Equal(new Uri("http://www.w3.org/2001/xml.xsd"));
    }

    [Fact]
    public void An_import_that_names_only_a_namespace_is_found_through_the_catalog()
    {
        var byNamespace = Load(Catalog("""<uri name="http://www.w3.org/XML/1998/namespace" uri="mem://test/local/w3c/xml.xsd"/>"""));
        var sources = new[] { SchemaSource.FromText(Importing("""<xs:import namespace="http://www.w3.org/XML/1998/namespace"/>"""), RootUri) }.Concat(LocalCopies).ToArray();

        var schema = SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, sources, new SchemaCompileOptions { Catalog = byNamespace });

        schema.Documents.Should().HaveCount(3);
        SchemaValidator.Validate(schema, "<p xmlns='urn:c' xml:lang='not a language'/>").IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_root_is_mapped_too()
    {
        var published = new Uri("http://schemas.example/main.xsd");
        var catalog = Load(Catalog($"""<uri name="{published}" uri="{RootUri}"/>"""));
        var schema = SchemaCompiler.Compile([published], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:r'><xs:element name='r' type='xs:string'/></xs:schema>", RootUri)],
            new SchemaCompileOptions { Catalog = catalog });
        schema.Roots.Should().Equal(published);
        schema.Documents.Should().ContainSingle().Which.Uri.Should().Be(RootUri);
        SchemaValidator.Validate(schema, "<r xmlns='urn:r'/>").IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_catalog_is_part_of_what_a_cached_schema_is_found_by()
    {
        var cache = new SchemaCache();
        var sources = new[] { SchemaSource.FromText("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:r'/>", RootUri) };
        var none = await cache.GetAsync([RootUri], SchemaAccessGate.SuppliedOnly, sources);
        var same = await cache.GetAsync([RootUri], SchemaAccessGate.SuppliedOnly, sources, new SchemaCompileOptions());
        var with = await cache.GetAsync([RootUri], SchemaAccessGate.SuppliedOnly, sources, new SchemaCompileOptions { Catalog = ToLocal });
        same.Should().BeSameAs(none);
        with.Should().NotBeSameAs(none);
    }
}
