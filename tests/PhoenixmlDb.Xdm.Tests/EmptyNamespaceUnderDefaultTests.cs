using System.Xml.Linq;
using FluentAssertions;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// An element in no namespace, under an ancestor with a default namespace, is written with its own
/// xmlns="" (phoenixmldb-core#6). The serializer opened it with the one-argument WriteStartElement —
/// which takes the AMBIENT default namespace — and then wrote the recorded xmlns="" declaration, so
/// the two contradicted each other in one start tag and XmlWriter threw "The prefix '' cannot be
/// redefined from 'urn:d' to ''".
/// </summary>
public class EmptyNamespaceUnderDefaultTests
{
    private static XDocument RoundTrip(string xml)
    {
        var helper = new XmlSerializerRoundTripTests();
        return XDocument.Parse(helper.SerializeForTest(xml));
    }

    [Fact]
    public void Inner_empty_namespace_under_a_default_namespace_round_trips()
    {
        var doc = RoundTrip("<doc xmlns=\"urn:d\"><e xmlns=\"\"/></doc>");
        doc.Root!.Name.NamespaceName.Should().Be("urn:d");
        doc.Root.Elements().Single().Name.NamespaceName.Should().Be("", "e undeclares the default namespace");
    }

    [Fact]
    public void Nested_undeclaration_keeps_descendants_out_of_the_default_namespace()
    {
        var doc = RoundTrip("<doc xmlns=\"urn:d\"><e xmlns=\"\"><f/></e><g/></doc>");
        doc.Descendants().Single(x => x.Name.LocalName == "f").Name.NamespaceName.Should().Be("");
        doc.Descendants().Single(x => x.Name.LocalName == "g").Name.NamespaceName.Should().Be("urn:d");
    }

    [Fact]
    public void A_no_namespace_document_gains_no_declaration()
        => new XmlSerializerRoundTripTests().SerializeForTest("<root><a/></root>").Should().NotContain("xmlns");
}
