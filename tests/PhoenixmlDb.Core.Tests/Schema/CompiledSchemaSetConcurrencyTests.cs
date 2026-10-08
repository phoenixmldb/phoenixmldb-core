using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// The shared schema layer keeps one compiled <see cref="XmlSchemaSet"/> and validates against it
/// from many threads at once. .NET does not document that as safe, so it is measured here before
/// anything relies on it: every validation made concurrently must report exactly what the same
/// validation reports alone.
/// </summary>
/// <remarks>
/// A set is compiled afresh for each round and every thread starts on it at the same instant,
/// because state a set builds on first use (content models, pattern expressions, identity
/// constraint tables) is where a race would be. The schema uses each of those. The work is a fixed
/// count, not a time, so a run means the same thing on any machine.
/// </remarks>
public sealed class CompiledSchemaSetConcurrencyTests
{
    private const string Schema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:c" xmlns="urn:c" elementFormDefault="qualified">
          <xs:simpleType name="code"><xs:restriction base="xs:string"><xs:pattern value="[A-Z]{2}-\d{3}(-[a-z]+)*"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="size"><xs:restriction base="xs:integer"><xs:minInclusive value="1"/><xs:maxInclusive value="9"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="sizeOrAuto"><xs:union memberTypes="size"><xs:simpleType><xs:restriction base="xs:string"><xs:enumeration value="auto"/></xs:restriction></xs:simpleType></xs:union></xs:simpleType>
          <xs:simpleType name="sizes"><xs:list itemType="size"/></xs:simpleType>
          <xs:element name="item" type="itemType"/>
          <xs:element name="special" type="specialType" substitutionGroup="item"/>
          <xs:complexType name="itemType">
            <xs:sequence>
              <xs:element name="code" type="code"/>
              <xs:choice minOccurs="0" maxOccurs="unbounded">
                <xs:element name="size" type="sizeOrAuto"/>
                <xs:element name="sizes" type="sizes"/>
                <xs:element name="ref"><xs:complexType><xs:attribute name="to" type="xs:string" use="required"/></xs:complexType></xs:element>
              </xs:choice>
              <xs:any namespace="##other" processContents="lax" minOccurs="0"/>
            </xs:sequence>
            <xs:attribute name="id" type="xs:ID" use="required"/>
            <xs:attribute name="state" type="xs:string" default="new"/>
          </xs:complexType>
          <xs:complexType name="specialType">
            <xs:complexContent><xs:extension base="itemType"><xs:attribute name="grade" type="size"/></xs:extension></xs:complexContent>
          </xs:complexType>
          <xs:element name="order">
            <xs:complexType>
              <xs:sequence><xs:element ref="item" maxOccurs="unbounded"/></xs:sequence>
            </xs:complexType>
            <xs:key name="byCode"><xs:selector xpath="*"/><xs:field xpath="@id"/></xs:key>
            <xs:keyref name="refs" refer="byCode"><xs:selector xpath="*/*"/><xs:field xpath="@to"/></xs:keyref>
          </xs:element>
        </xs:schema>
        """;

    private static string Order(string body) => $"""<order xmlns="urn:c">{body}</order>""";

    private static readonly string[] Documents =
    [
        Order("""<item id="a"><code>AB-123</code><size>3</size><sizes>1 2 3</sizes><ref to="a"/></item><special id="b" grade="2"><code>CD-456-x-y</code><size>auto</size></special>"""),
        Order(string.Concat(Enumerable.Range(0, 60).Select(i => $"""<item id="i{i}"><code>AB-{i:000}</code><size>{i % 9 + 1}</size><ref to="i{(i + 1) % 60}"/></item>"""))),
        Order("""<item id="a"><code>ab-123</code></item>"""),                                  // pattern
        Order("""<item id="a"><code>AB-123</code><size>12</size></item>"""),                   // union, range
        Order("""<item id="a"><code>AB-123</code><sizes>1 two 3</sizes></item>"""),            // list item
        Order("""<item id="a"><code>AB-123</code><ref to="nosuch"/></item>"""),                // keyref
        Order("""<item id="a"><code>AB-123</code></item><item id="a"><code>AB-124</code></item>"""), // key, ID
        Order("""<item id="a"><size>3</size><code>AB-123</code></item>"""),                    // content model
        Order("""<special id="a" grade="0"><code>AB-123</code></special><item><code>AB-124</code></item>"""),
        Order("""<item id="a"><code>AB-123</code><x:any xmlns:x="urn:other" q="1"><x:deep/></x:any></item>"""),
    ];

    private static XmlSchemaSet Compile()
    {
        var set = new XmlSchemaSet();
        using var reader = XmlReader.Create(new StringReader(Schema));
        set.Add("urn:c", reader);
        set.Compile();
        return set;
    }

    /// <summary>Everything one validation reports: each diagnostic in order, then the fatal error if any.</summary>
    private static string Validate(XmlSchemaSet set, string document)
    {
        var report = new System.Text.StringBuilder();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = set };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessIdentityConstraints | XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) =>
            report.Append(e.Severity).Append('|').Append(e.Exception.LineNumber).Append(':').Append(e.Exception.LinePosition)
                .Append('|').Append(e.Message).Append('\n');
        try
        {
            using var reader = XmlReader.Create(new StringReader(document), settings);
            var defaults = 0;
            while (reader.Read())
            {
                // Schema defaults and type information come from the shared set too.
                if (reader.NodeType == XmlNodeType.Element && reader.SchemaInfo?.SchemaType is { } type)
                    report.Append("type|").Append(reader.LocalName).Append('|').Append(type.QualifiedName).Append('\n');
                if (reader.NodeType == XmlNodeType.Element && reader.GetAttribute("state") == "new")
                    defaults++;
            }
            report.Append("defaults|").Append(defaults).Append('\n');
        }
        catch (XmlException ex)
        {
            report.Append("fatal|").Append(ex.Message).Append('\n');
        }
        return report.ToString();
    }

    [Fact]
    public void The_reference_reports_distinguish_the_documents()
    {
        // The comparison below means something only if the documents exercise the schema: the
        // first two are valid, each of the others is refused, and no two reports are the same.
        var set = Compile();
        var reports = Documents.Select(d => Validate(set, d)).ToArray();
        reports[0].Should().NotContain("Error|").And.Contain("type|special|urn:c:specialType").And.Contain("defaults|2");
        reports[1].Should().NotContain("Error|");
        foreach (var refused in reports.Skip(2).Take(7))
            refused.Should().Contain("Error|");
        reports[9].Should().NotContain("Error|");
        reports.Distinct().Should().HaveCount(Documents.Length);
    }

    [Fact]
    public void Concurrent_validation_against_one_compiled_set_reports_what_a_single_validation_reports()
    {
        const int rounds = 40;
        const int passesPerThread = 25;
        var threads = Math.Max(8, Environment.ProcessorCount * 2);

        var expected = Documents.Select(d => Validate(Compile(), d)).ToArray();
        var failures = new ConcurrentQueue<string>();
        long validations = 0;

        for (var round = 0; round < rounds && failures.IsEmpty; round++)
        {
            var set = Compile();
            using var start = new Barrier(threads);
            var workers = new Thread[threads];
            for (var t = 0; t < threads; t++)
            {
                var offset = t;
                workers[t] = new Thread(() =>
                {
                    try
                    {
                        start.SignalAndWait();
                        for (var pass = 0; pass < passesPerThread; pass++)
                        {
                            for (var d = 0; d < Documents.Length; d++)
                            {
                                // Each thread starts on a different document, so the first uses of
                                // the set are of different parts of it at once.
                                var index = (d + offset) % Documents.Length;
                                var actual = Validate(set, Documents[index]);
                                Interlocked.Increment(ref validations);
                                if (actual != expected[index])
                                {
                                    failures.Enqueue($"document {index}:\nexpected:\n{expected[index]}\nactual:\n{actual}");
                                    return;
                                }
                            }
                        }
                    }
#pragma warning disable CA1031 // any exception at all is the finding
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        failures.Enqueue(ex.ToString());
                    }
                });
                workers[t].Start();
            }
            foreach (var worker in workers)
                worker.Join();
        }

        failures.Should().BeEmpty();
        Interlocked.Read(ref validations).Should().Be((long)rounds * threads * passesPerThread * Documents.Length);
    }
}
