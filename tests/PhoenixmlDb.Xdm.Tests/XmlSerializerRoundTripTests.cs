using System.Collections.Immutable;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using PhoenixmlDb.Xdm.Nodes;
using PhoenixmlDb.Xdm.Parsing;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Round-trip tests that verify XmlSerializer produces correct encoding declarations,
/// indentation behaviour, and content fidelity after a parse → serialize → re-parse cycle.
/// </summary>
public class XmlSerializerRoundTripTests
{
    private static readonly DocumentId TestDocId = new(1);
    private static readonly NodeId StartNodeId = new(1);

    // Bidirectional maps: URI ↔ NamespaceId
    private readonly Dictionary<string, NamespaceId> _uriToId = new()
    {
        { "", NamespaceId.None },
        { "http://www.w3.org/XML/1998/namespace", NamespaceId.Xml },
        { "http://www.w3.org/2000/xmlns/", NamespaceId.Xmlns },
        { "http://www.w3.org/2001/XMLSchema", NamespaceId.Xsd },
        { "http://www.w3.org/2001/XMLSchema-instance", NamespaceId.Xsi },
        { "http://www.w3.org/2005/xpath-functions", NamespaceId.Fn }
    };

    private readonly Dictionary<NamespaceId, string> _idToUri = new()
    {
        { NamespaceId.None, "" },
        { NamespaceId.Xml, "http://www.w3.org/XML/1998/namespace" },
        { NamespaceId.Xmlns, "http://www.w3.org/2000/xmlns/" },
        { NamespaceId.Xsd, "http://www.w3.org/2001/XMLSchema" },
        { NamespaceId.Xsi, "http://www.w3.org/2001/XMLSchema-instance" },
        { NamespaceId.Fn, "http://www.w3.org/2005/xpath-functions" }
    };

    private uint _nextNsId = NamespaceId.FirstUserNamespaceId;

    // -------------------------------------------------------------------------
    // 1. Encoding declaration
    // -------------------------------------------------------------------------

    [Fact]
    public void SerializeToString_DeclaresUtf8_NotUtf16()
    {
        const string xml = "<root><child>text</child></root>";
        var (doc, nodeStore) = ParseToStore(xml);
        var serializer = CreateSerializer(nodeStore);

        var result = serializer.Serialize(doc);

        result.Should().StartWith(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            because: "Utf8StringWriter overrides the underlying Encoding so XmlWriter emits utf-8");
        result.Should().NotContainEquivalentOf("utf-16",
            because: "the old StringBuilder path incorrectly declared utf-16");
    }

    // -------------------------------------------------------------------------
    // 2. Indentation: default constructor → indented
    // -------------------------------------------------------------------------

    [Fact]
    public void SerializeToString_IndentsByDefault()
    {
        const string xml = "<root><a/><b/></root>";
        var (doc, nodeStore) = ParseToStore(xml);

        // Construct without specifying indent — the default is now true
        var serializer = new XmlSerializer(
            id => nodeStore.TryGetValue(id, out var n) ? n : null,
            ns => _idToUri.TryGetValue(ns, out var uri) ? uri : null);

        var result = serializer.Serialize(doc);

        result.Should().Contain("\n",
            because: "indent defaults to true; child elements should be on separate lines");
    }

    // -------------------------------------------------------------------------
    // 3. Compact output when indent: false
    // -------------------------------------------------------------------------

    [Fact]
    public void SerializeToString_CompactWhenIndentFalse()
    {
        const string xml = "<root><a/><b/></root>";
        var (doc, nodeStore) = ParseToStore(xml);
        var serializer = CreateSerializer(nodeStore, indent: false);

        var result = serializer.Serialize(doc);

        // After stripping the XML declaration line, the content should have no line breaks
        // between elements (compact mode).
        var afterDecl = result.Contains("?>")
            ? result[(result.IndexOf("?>", StringComparison.Ordinal) + 2)..]
            : result;

        afterDecl.Should().NotContain("\n",
            because: "indent: false should produce compact single-line output");
    }

    // -------------------------------------------------------------------------
    // 4. Round-trip: schema-inference catalog fixture
    // -------------------------------------------------------------------------

    [Fact]
    public void SerializeToString_RoundTripsSchemaInferenceCatalog()
    {
        const string catalogXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <catalog>
              <category id="xslt">
                <product featured="true">
                  <title>XSLT Cookbook</title>
                  <price currency="USD">39.99</price>
                </product>
                <product featured="false">
                  <title>XSLT 2.0 and XPath 2.0</title>
                  <price currency="USD">59.99</price>
                </product>
              </category>
              <category id="xquery">
                <product featured="true">
                  <title>XQuery</title>
                  <price currency="EUR">44.50</price>
                </product>
              </category>
            </catalog>
            """;

        var (doc, nodeStore) = ParseToStore(catalogXml);
        var serializer = CreateSerializer(nodeStore);

        var serialized = serializer.Serialize(doc);

        // Must be well-formed: re-parse without throwing
        var (reparsedDoc, _) = ParseToStore(serialized);
        reparsedDoc.Should().NotBeNull();

        // Key content must survive the round-trip
        serialized.Should().Contain("XSLT Cookbook");
        serialized.Should().Contain("featured=\"true\"");
        serialized.Should().Contain("currency=\"USD\"");
        serialized.Should().Contain("encoding=\"utf-8\"");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Parses <paramref name="xml"/> and returns the document + a flat node store.</summary>
    private (XdmDocument doc, Dictionary<NodeId, XdmNode> nodeStore) ParseToStore(string xml)
    {
        var parser = new XmlDocumentParser(
            TestDocId,
            StartNodeId,
            ResolveNamespace,
            preserveWhitespace: false);

        var result = parser.Parse(xml);

        var store = result.Nodes.ToDictionary(n => n.Id);
        return (result.Document, store);
    }

    private XmlSerializer CreateSerializer(
        Dictionary<NodeId, XdmNode> nodeStore,
        bool indent = true)
    {
        return new XmlSerializer(
            id => nodeStore.TryGetValue(id, out var n) ? n : null,
            ns => _idToUri.TryGetValue(ns, out var uri) ? uri : null,
            indent);
    }

    private NamespaceId ResolveNamespace(string uri)
    {
        if (_uriToId.TryGetValue(uri, out var id))
            return id;

        var newId = new NamespaceId(_nextNsId++);
        _uriToId[uri] = newId;
        _idToUri[newId] = uri;
        return newId;
    }
}
