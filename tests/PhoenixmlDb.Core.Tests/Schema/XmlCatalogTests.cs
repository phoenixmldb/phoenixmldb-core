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
    public void Foreign_elements_are_ignored()
    {
        var catalog = Load(Catalog("""
            <other xmlns="urn:not-a-catalog" name="a" uri="file:///wrong"/>
            <uri name="a" uri="file:///a"/>
            """));
        catalog.ResolveUri("a").Should().Be(new Uri("file:///a"));
    }

    // ── delegation ──

    private static SchemaSource File(string name, string body)
        => SchemaSource.FromText(Catalog(body), new Uri("mem://test/cat/" + name));

    [Fact]
    public void A_delegated_name_is_looked_up_in_the_catalog_the_entry_names()
    {
        var catalog = Load(Catalog("""
            <delegateURI uriStartString="http://d.example/" catalog="delegated.xml"/>
            <delegateSystem systemIdStartString="urn:sys:" catalog="delegated.xml"/>
            <uri name="a" uri="file:///a"/>
            """),
            File("delegated.xml", """
                <uri name="http://d.example/x.xsd" uri="file:///delegated/x.xsd"/>
                <system systemId="urn:sys:y" uri="file:///delegated/y.xsd"/>
                """));
        catalog.ResolveUri("http://d.example/x.xsd").Should().Be(new Uri("file:///delegated/x.xsd"));
        catalog.ResolveUri("urn:sys:y").Should().Be(new Uri("file:///delegated/y.xsd"));
        catalog.ResolveUri("a").Should().Be(new Uri("file:///a"));
        catalog.Documents.Should().HaveCount(2, "a catalog two entries delegate to is read once");
    }

    [Fact]
    public void An_entry_of_the_catalog_itself_wins_over_delegation()
    {
        var catalog = Load(Catalog("""
            <delegateURI uriStartString="http://d.example/" catalog="delegated.xml"/>
            <uri name="http://d.example/x.xsd" uri="file:///own/x.xsd"/>
            """),
            File("delegated.xml", """<uri name="http://d.example/x.xsd" uri="file:///delegated/x.xsd"/>"""));
        catalog.ResolveUri("http://d.example/x.xsd").Should().Be(new Uri("file:///own/x.xsd"));
    }

    [Fact]
    public void Delegation_ends_the_lookup_and_the_next_catalogs_are_not_asked()
    {
        // XML Catalogs §7.2.2 step 6: when delegation finds nothing, nothing is found.
        var catalog = Load(Catalog("""
            <delegateURI uriStartString="http://d.example/" catalog="delegated.xml"/>
            <nextCatalog catalog="next.xml"/>
            """),
            File("delegated.xml", """<uri name="http://d.example/other.xsd" uri="file:///delegated/other.xsd"/>"""),
            File("next.xml", """
                <uri name="http://d.example/x.xsd" uri="file:///next/x.xsd"/>
                <uri name="http://e.example/x.xsd" uri="file:///next/e.xsd"/>
                """));
        catalog.ResolveUri("http://d.example/x.xsd").Should().BeNull();
        catalog.ResolveUri("http://e.example/x.xsd").Should().Be(new Uri("file:///next/e.xsd"), "a name no delegate entry matches goes on");
    }

    [Fact]
    public void Delegates_are_asked_longest_prefix_first()
    {
        var catalog = Load(Catalog("""
            <delegateURI uriStartString="http://d.example/" catalog="short.xml"/>
            <delegateURI uriStartString="http://d.example/deep/" catalog="long.xml"/>
            """),
            File("short.xml", """
                <uri name="http://d.example/deep/x.xsd" uri="file:///short/x.xsd"/>
                <uri name="http://d.example/deep/only-short.xsd" uri="file:///short/only.xsd"/>
                """),
            File("long.xml", """<uri name="http://d.example/deep/x.xsd" uri="file:///long/x.xsd"/>"""));
        catalog.ResolveUri("http://d.example/deep/x.xsd").Should().Be(new Uri("file:///long/x.xsd"));
        catalog.ResolveUri("http://d.example/deep/only-short.xsd").Should().Be(new Uri("file:///short/only.xsd"));
    }

    [Fact]
    public void A_catalog_that_delegates_to_itself_answers_and_does_not_loop()
    {
        var catalog = Load(Catalog("""<delegateURI uriStartString="http://d.example/" catalog="catalog.xml"/>"""));
        catalog.ResolveUri("http://d.example/x.xsd").Should().BeNull();
    }

    [Fact]
    public void A_delegated_catalog_that_cannot_be_read_fails_the_load()
        => FluentActions.Invoking(() => Load(Catalog("""<delegateURI uriStartString="http://d.example/" catalog="missing.xml"/>""")))
            .Should().Throw<SchemaCompilationException>().WithMessage("*'missing.xml' is not available*");

    [Fact]
    public void A_public_identifier_is_delegated_by_its_prefix()
    {
        var catalog = Load(Catalog("""<delegatePublic publicIdStartString="-//Example//" catalog="pub.xml"/>"""),
            File("pub.xml", """<public publicId="-//Example//DTD One//EN" uri="file:///one.dtd"/>"""));
        catalog.ResolvePublic("-//Example//DTD   One//EN").Should().Be(new Uri("file:///one.dtd"));
        catalog.ResolvePublic("-//Other//DTD One//EN").Should().BeNull();
    }

    [Fact]
    public void Two_catalogs_that_differ_only_in_what_they_delegate_to_have_different_identities()
    {
        const string top = """<delegateURI uriStartString="http://d.example/" catalog="delegated.xml"/>""";
        var first = Load(Catalog(top), File("delegated.xml", """<uri name="http://d.example/x" uri="file:///one"/>"""));
        var second = Load(Catalog(top), File("delegated.xml", """<uri name="http://d.example/x" uri="file:///two"/>"""));
        var again = Load(Catalog(top), File("delegated.xml", """<uri name="http://d.example/x" uri="file:///one"/>"""));
        second.Identity.Should().NotBe(first.Identity);
        again.Identity.Should().Be(first.Identity);
    }

    // ── prefer ──

    [Theory]
    [InlineData("", true)]
    [InlineData("prefer=\"public\"", true)]
    [InlineData("prefer=\"system\"", false)]
    public void Prefer_says_whether_a_public_entry_is_used_when_there_is_a_system_identifier(string attribute, bool used)
    {
        var catalog = Load(Catalog("""<public publicId="-//Example//DTD One//EN" uri="file:///one.dtd"/>""", attribute));
        var found = catalog.ResolveExternalIdentifier("-//Example//DTD One//EN", "http://unknown.example/one.dtd");
        if (used)
            found.Should().Be(new Uri("file:///one.dtd"));
        else
            found.Should().BeNull();
        // With no system identifier the public entry is always used.
        catalog.ResolveExternalIdentifier("-//Example//DTD One//EN", null).Should().Be(new Uri("file:///one.dtd"));
    }

    [Fact]
    public void A_group_sets_prefer_for_its_own_entries()
    {
        var catalog = Load(Catalog("""
            <group prefer="public"><public publicId="-//A//" uri="file:///a.dtd"/></group>
            <public publicId="-//B//" uri="file:///b.dtd"/>
            """, "prefer=\"system\""));
        catalog.ResolveExternalIdentifier("-//A//", "http://x.example/s").Should().Be(new Uri("file:///a.dtd"));
        catalog.ResolveExternalIdentifier("-//B//", "http://x.example/s").Should().BeNull();
    }

    [Fact]
    public void The_system_identifier_is_looked_up_before_the_public_one()
    {
        var catalog = Load(Catalog("""
            <public publicId="-//A//" uri="file:///by-public.dtd"/>
            <system systemId="http://x.example/a.dtd" uri="file:///by-system.dtd"/>
            """));
        catalog.ResolveExternalIdentifier("-//A//", "http://x.example/a.dtd").Should().Be(new Uri("file:///by-system.dtd"));
    }

    // ── normalization ──

    [Fact]
    public void Names_compare_after_the_characters_a_uri_cannot_hold_are_encoded()
    {
        var catalog = Load(Catalog("""
            <uri name="http://example.org/my schema.xsd" uri="file:///spaced.xsd"/>
            <uri name="http://example.org/caf%C3%A9.xsd" uri="file:///cafe.xsd"/>
            """));
        catalog.ResolveUri("http://example.org/my%20schema.xsd").Should().Be(new Uri("file:///spaced.xsd"));
        catalog.ResolveUri("http://example.org/my schema.xsd").Should().Be(new Uri("file:///spaced.xsd"));
        catalog.ResolveUri("http://example.org/caf\u00e9.xsd").Should().Be(new Uri("file:///cafe.xsd"));
    }

    [Theory]
    [InlineData("a b", "a%20b")]
    [InlineData("a%20b", "a%20b")]
    [InlineData("a\\b{c}|d^e`f\"g<h>", "a%5Cb%7Bc%7D%7Cd%5Ee%60f%22g%3Ch%3E")]
    [InlineData("\u00e9", "%C3%A9")]
    [InlineData("\ud83d\ude00", "%F0%9F%98%80")]
    [InlineData("http://example.org/plain.xsd?q=1#f", "http://example.org/plain.xsd?q=1#f")]
    public void Uri_normalization(string written, string normalized)
        => XmlCatalog.NormalizeUri(written).Should().Be(normalized);

    [Theory]
    [InlineData("urn:publicid:-:Example:DTD+One:EN", "-//Example//DTD One//EN")]
    [InlineData("URN:PUBLICID:a;b%3Ac%2Fd%2Be%3Bf%27g%3Fh%23i%25j", "a::b:c/d+e;f'g?h#i%j")]
    public void A_publicid_urn_is_the_public_identifier_it_stands_for(string name, string publicId)
        => XmlCatalog.UnwrapPublicIdUrn(name).Should().Be(publicId);

    [Fact]
    public void A_publicid_urn_is_looked_up_as_a_public_identifier_wherever_it_is_written()
    {
        var catalog = Load(Catalog("""
            <public publicId="-//Example//DTD One//EN" uri="file:///one.dtd"/>
            <system systemId="urn:publicid:-:Example:DTD+Two:EN" uri="file:///two.dtd"/>
            """));
        catalog.ResolveUri("urn:publicid:-:Example:DTD+One:EN").Should().Be(new Uri("file:///one.dtd"));
        catalog.ResolveExternalIdentifier(null, "urn:publicid:-:Example:DTD+One:EN").Should().Be(new Uri("file:///one.dtd"));
        catalog.ResolvePublic("-//Example//DTD Two//EN").Should().Be(new Uri("file:///two.dtd"));
        XmlCatalog.UnwrapPublicIdUrn("http://example.org/x").Should().BeNull();
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
