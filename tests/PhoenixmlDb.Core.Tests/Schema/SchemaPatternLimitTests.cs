using System.Diagnostics;
using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// <see cref="SchemaCompileOptions.PatternMatchTimeout"/>: a pattern facet that backtracks
/// catastrophically stops at the limit, in compilation and in validation.
/// </summary>
public sealed class SchemaPatternLimitTests
{
    private static readonly Uri SchemaUri = new("urn:test:pattern.xsd");
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(100);

    // (a+)+b backtracks exponentially on a run of 'a' that no 'b' ends.
    private const string Slow = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";

    private static string Schema(string extra = "") => $"""
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:simpleType name="t">
            <xs:restriction base="xs:string">
              <xs:pattern value="(a+)+b"/>{extra}
            </xs:restriction>
          </xs:simpleType>
          <xs:element name="v" type="t"/>
        </xs:schema>
        """;

    private static CompiledSchema Compile(string schema, TimeSpan? limit) =>
        SchemaCompiler.Compile([SchemaUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(schema, SchemaUri)],
            new SchemaCompileOptions { PatternMatchTimeout = limit });

    [Fact]
    public void ValidationStopsAtTheLimit()
    {
        var schema = Compile(Schema(), Limit);
        schema.PatternMatchTimeout.Should().Be(Limit);

        var watch = Stopwatch.StartNew();
        var result = SchemaValidator.Validate(schema, $"<v>{Slow}</v>");
        watch.Stop();

        result.PatternTimedOut.Should().BeTrue();
        result.Truncated.Should().BeTrue();
        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().Contain("match time limit");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void AValueThePatternAcceptsIsStillValid()
    {
        var schema = Compile(Schema(), Limit);
        var result = SchemaValidator.Validate(schema, "<v>aaab</v>");
        result.IsValid.Should().BeTrue();
        result.PatternTimedOut.Should().BeFalse();
    }

    [Fact]
    public void AValueThePatternRejectsQuicklyIsInvalidNotTimedOut()
    {
        var schema = Compile(Schema(), Limit);
        var result = SchemaValidator.Validate(schema, "<v>xyz</v>");
        result.IsValid.Should().BeFalse();
        result.PatternTimedOut.Should().BeFalse();
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public void ASchemaWhoseOwnValuesRunPastTheLimitDoesNotCompile()
    {
        // Compiling checks the enumeration value against the pattern, with no limit of its own.
        var schema = Schema($"""<xs:enumeration value="{Slow}"/>""");
        var watch = Stopwatch.StartNew();
        var act = () => Compile(schema, Limit);
        act.Should().Throw<SchemaCompilationException>().WithMessage("*match time limit*");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void ADefaultValueIsCheckedToo()
    {
        var schema = Schema().Replace("""<xs:element name="v" type="t"/>""",
            $"""<xs:element name="v" type="t" default="{Slow}"/>""", StringComparison.Ordinal);
        var act = () => Compile(schema, Limit);
        act.Should().Throw<SchemaCompilationException>().WithMessage("*match time limit*");
    }

    [Fact]
    public void PatternsOfListItemsAndUnionMembersAreBounded()
    {
        const string schemaText = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:simpleType name="t">
                <xs:restriction base="xs:string"><xs:pattern value="(a+)+b"/></xs:restriction>
              </xs:simpleType>
              <xs:simpleType name="l"><xs:list itemType="t"/></xs:simpleType>
              <xs:simpleType name="u"><xs:union memberTypes="xs:int t"/></xs:simpleType>
              <xs:element name="list" type="l"/>
              <xs:element name="union" type="u"/>
              <xs:element name="anon">
                <xs:simpleType>
                  <xs:restriction base="xs:string"><xs:pattern value="(a+)+b"/></xs:restriction>
                </xs:simpleType>
              </xs:element>
              <xs:element name="attr">
                <xs:complexType><xs:attribute name="a" type="t"/></xs:complexType>
              </xs:element>
            </xs:schema>
            """;
        var schema = Compile(schemaText, Limit);
        foreach (var instance in new[]
                 {
                     $"<list>ab {Slow}</list>", $"<union>{Slow}</union>", $"<anon>{Slow}</anon>", $"<attr a='{Slow}'/>",
                 })
        {
            SchemaValidator.Validate(schema, instance).PatternTimedOut.Should().BeTrue(instance[..10]);
        }
    }

    [Fact]
    public void WithoutALimitNothingIsBounded()
    {
        var schema = Compile(Schema(), null);
        schema.PatternMatchTimeout.Should().BeNull();
        // A short run only: with no limit the long one would not come back.
        SchemaValidator.Validate(schema, "<v>aaaaaaaaaaaa!</v>").PatternTimedOut.Should().BeFalse();
    }

    [Fact]
    public void TheLimitMustBePositive()
    {
        var act = () => new SchemaCompileOptions { PatternMatchTimeout = TimeSpan.Zero };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheCacheKeepsSchemasWithDifferentLimitsApart()
    {
        var cache = new SchemaCache();
        SchemaSource[] sources = [SchemaSource.FromText(Schema(), SchemaUri)];
        var unbounded = cache.Get([SchemaUri], SchemaAccessGate.SuppliedOnly, sources);
        var bounded = cache.Get([SchemaUri], SchemaAccessGate.SuppliedOnly, sources,
            new SchemaCompileOptions { PatternMatchTimeout = Limit });
        bounded.Should().NotBeSameAs(unbounded);
        bounded.PatternMatchTimeout.Should().Be(Limit);
        unbounded.PatternMatchTimeout.Should().BeNull();
        // The unbounded one was not bounded by the other's compilation.
        SchemaValidator.Validate(unbounded, "<v>aaaaaaaaaaaa!</v>").PatternTimedOut.Should().BeFalse();
        SchemaValidator.Validate(bounded, $"<v>{Slow}</v>").PatternTimedOut.Should().BeTrue();
    }

    /// <summary>
    /// The compile-time check builds each pattern itself, and is only a bound on what System.Xml
    /// then does if it builds the same expression.
    /// </summary>
    [Theory]
    [InlineData(@"(a+)+b")]
    [InlineData(@"\i\c*")]
    [InlineData(@"\d{3}-\D\w\W")]
    [InlineData(@"[\i-[:]][\c-[:]]*")]
    [InlineData(@"a\\c|\\\d")]
    [InlineData(@"\I\C")]
    public void TheCompileTimeCheckBuildsTheExpressionSystemXmlBuilds(string pattern)
    {
        var set = new System.Xml.Schema.XmlSchemaSet();
        var xsd = $"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:simpleType name="p"><xs:restriction base="xs:string"><xs:pattern value="{pattern}"/></xs:restriction></xs:simpleType>
            </xs:schema>
            """;
        using (var reader = System.Xml.XmlReader.Create(new StringReader(xsd)))
            set.Add(null, reader);
        set.Compile();
        var type = (System.Xml.Schema.XmlSchemaSimpleType)set.GlobalTypes[new System.Xml.XmlQualifiedName("p")]!;
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var facets = type.Datatype!.GetType().GetProperty("Restriction", any)!.GetValue(type.Datatype)!;
        var patterns = (System.Collections.IList)facets.GetType().GetField("Patterns", any)!.GetValue(facets)!;

        SchemaPatternGuard.ToNetPattern(pattern).Should().Be(patterns[0]!.ToString());
    }
}
