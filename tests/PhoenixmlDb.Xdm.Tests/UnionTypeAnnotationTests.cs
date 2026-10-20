using System.Xml;
using System.Xml.Schema;
using AwesomeAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// A node validated against a union type is annotated with the union, its governing type, not
/// with the member type its value matched (XDM 3.1 §3.3.1.2). The member was the annotation, so
/// element(*, M) matched an element declared with the union type U (W3C XSLT match-195). The
/// typed value still comes from the member: the annotator computes it from the type and the
/// string value. An xs:ID member still makes the content an ID.
/// </summary>
public class UnionTypeAnnotationTests
{
    private const string Schema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:u" xmlns:u="urn:u" elementFormDefault="qualified">
          <xs:simpleType name="intOrDate"><xs:union memberTypes="xs:integer xs:date"/></xs:simpleType>
          <xs:simpleType name="idOrInt"><xs:union memberTypes="xs:ID xs:integer"/></xs:simpleType>
          <xs:element name="r">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="v" type="u:intOrDate"/>
                <xs:element name="k" type="u:idOrInt"/>
              </xs:sequence>
              <xs:attribute name="a" type="u:intOrDate"/>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    [Fact]
    public void A_union_typed_element_and_attribute_are_annotated_with_the_union()
    {
        var schemas = new XmlSchemaSet();
        using (var reader = XmlReader.Create(new StringReader(Schema)))
            schemas.Add("urn:u", reader);
        schemas.Compile();

        var namespaces = new Dictionary<string, NamespaceId>(StringComparer.Ordinal)
        {
            [""] = NamespaceId.None,
            ["http://www.w3.org/XML/1998/namespace"] = NamespaceId.Xml,
            ["http://www.w3.org/2000/xmlns/"] = NamespaceId.Xmlns,
        };
        var parser = new XmlDocumentParser(new DocumentId(1), new NodeId(1),
            uri => namespaces.TryGetValue(uri, out var id) ? id : namespaces[uri] = new NamespaceId((uint)(namespaces.Count + 10)))
        {
            SchemaTypeAnnotator = type => type.QualifiedName.Namespace == XmlSchema.Namespace
                ? null
                : new XdmTypeName(new NamespaceId(99), type.QualifiedName.Name),
        };

        var result = parser.Parse(new StringReader("<r xmlns='urn:u' a='7'><v>12</v><k>key1</k></r>"), null, schemas);

        var elements = result.Nodes.OfType<XdmElement>().ToDictionary(e => e.LocalName);
        elements["v"].TypeAnnotation.LocalName.Should().Be("intOrDate");
        result.Nodes.OfType<XdmAttribute>().Single(a => a.LocalName == "a").TypeAnnotation.LocalName.Should().Be("intOrDate");
        elements["k"].TypeAnnotation.LocalName.Should().Be("idOrInt");
        elements["k"].IsIdContent.Should().BeTrue("the value matched the xs:ID member");
    }
}
