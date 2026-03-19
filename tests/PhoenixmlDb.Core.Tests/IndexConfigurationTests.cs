using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// Tests for IndexConfiguration fluent builder.
/// </summary>
public class IndexConfigurationTests
{
    [Fact]
    public void Constructor_CreatesEmptyConfiguration()
    {
        var config = new IndexConfiguration();

        config.Indexes.Should().BeEmpty();
        config.StructuralIndexEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddNameIndex_WithNullNamespace_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddNameIndex((Uri?)null);

        result.Should().BeSameAs(config);
        config.Indexes.Should().HaveCount(1);
        config.Indexes[0].Should().BeOfType<NameIndexDefinition>();
    }

    [Fact]
    public void AddNameIndex_WithNamespace_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddNameIndex(new Uri("http://example.com/ns"));

        result.Should().BeSameAs(config);
        var indexes = config.Indexes;
        indexes.Should().HaveCount(1);

        var nameIndex = indexes[0] as NameIndexDefinition;
        nameIndex.Should().NotBeNull();
        nameIndex!.NamespaceUri.Should().NotBeNull();
        nameIndex.NamespaceUri!.ToString().Should().Be("http://example.com/ns");
    }

    [Fact]
    public void AddNameIndex_WithEmptyString_AddsIndexWithEmptyUri()
    {
        var config = new IndexConfiguration();

        var result = config.AddNameIndex(new Uri("", UriKind.RelativeOrAbsolute));

        result.Should().BeSameAs(config);
        var nameIndex = config.Indexes[0] as NameIndexDefinition;
        nameIndex.Should().NotBeNull();
    }

    [Fact]
    public void AddPathIndex_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddPathIndex("//customer/address");

        result.Should().BeSameAs(config);
        var indexes = config.Indexes;
        indexes.Should().HaveCount(1);

        var pathIndex = indexes[0] as PathIndexDefinition;
        pathIndex.Should().NotBeNull();
        pathIndex!.PathPattern.Should().Be("//customer/address");
    }

    [Theory]
    [InlineData("/root")]
    [InlineData("//element")]
    [InlineData("/root/child")]
    [InlineData("//element/@attribute")]
    [InlineData("/root/*")]
    [InlineData("//*/text()")]
    public void AddPathIndex_AcceptsVariousPatterns(string pattern)
    {
        var config = new IndexConfiguration();

        config.AddPathIndex(pattern);

        var pathIndex = config.Indexes[0] as PathIndexDefinition;
        pathIndex.Should().NotBeNull();
        pathIndex!.PathPattern.Should().Be(pattern);
    }

    [Fact]
    public void AddValueIndex_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddValueIndex("/root/price", XdmValueType.XdmDecimal);

        result.Should().BeSameAs(config);
        var indexes = config.Indexes;
        indexes.Should().HaveCount(1);

        var valueIndex = indexes[0] as ValueIndexDefinition;
        valueIndex.Should().NotBeNull();
        valueIndex!.PathPattern.Should().Be("/root/price");
        valueIndex.ValueType.Should().Be(XdmValueType.XdmDecimal);
        valueIndex.Collation.Should().BeNull();
    }

    [Fact]
    public void AddValueIndex_WithCollation_AddsIndex()
    {
        var config = new IndexConfiguration();
        var collation = "http://www.w3.org/2013/collation/UCA?lang=en";

        var result = config.AddValueIndex("/root/name", XdmValueType.XdmString, collation);

        result.Should().BeSameAs(config);
        var valueIndex = config.Indexes[0] as ValueIndexDefinition;
        valueIndex.Should().NotBeNull();
        valueIndex!.Collation.Should().Be(collation);
    }

    [Theory]
    [InlineData(XdmValueType.XdmString)]
    [InlineData(XdmValueType.XdmInteger)]
    [InlineData(XdmValueType.XdmLong)]
    [InlineData(XdmValueType.XdmDecimal)]
    [InlineData(XdmValueType.XdmDouble)]
    [InlineData(XdmValueType.XdmFloat)]
    [InlineData(XdmValueType.Boolean)]
    [InlineData(XdmValueType.DateTime)]
    [InlineData(XdmValueType.Date)]
    [InlineData(XdmValueType.Time)]
    [InlineData(XdmValueType.Duration)]
    [InlineData(XdmValueType.AnyUri)]
    [InlineData(XdmValueType.QName)]
    [InlineData(XdmValueType.Base64Binary)]
    [InlineData(XdmValueType.HexBinary)]
    public void AddValueIndex_AllXdmValueTypes(XdmValueType valueType)
    {
        var config = new IndexConfiguration();

        config.AddValueIndex("/root/field", valueType);

        var valueIndex = config.Indexes[0] as ValueIndexDefinition;
        valueIndex.Should().NotBeNull();
        valueIndex!.ValueType.Should().Be(valueType);
    }

    [Fact]
    public void AddFullTextIndex_WithDefaults_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddFullTextIndex();

        result.Should().BeSameAs(config);
        var indexes = config.Indexes;
        indexes.Should().HaveCount(1);

        var ftIndex = indexes[0] as FullTextIndexDefinition;
        ftIndex.Should().NotBeNull();
        ftIndex!.PathPattern.Should().BeNull();
        ftIndex.Options.Should().Be(FullTextIndexOptions.Default);
    }

    [Fact]
    public void AddFullTextIndex_WithPath_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddFullTextIndex("//description");

        result.Should().BeSameAs(config);
        var ftIndex = config.Indexes[0] as FullTextIndexDefinition;
        ftIndex.Should().NotBeNull();
        ftIndex!.PathPattern.Should().Be("//description");
    }

    [Fact]
    public void AddFullTextIndex_WithOptions_AddsIndex()
    {
        var config = new IndexConfiguration();
        var options = new FullTextIndexOptions
        {
            Language = "de",
            CaseSensitive = true,
            Stemming = false
        };

        var result = config.AddFullTextIndex("//content", options);

        result.Should().BeSameAs(config);
        var ftIndex = config.Indexes[0] as FullTextIndexDefinition;
        ftIndex.Should().NotBeNull();
        ftIndex!.Options.Should().Be(options);
    }

    [Fact]
    public void AddMetadataIndex_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddMetadataIndex("author");

        result.Should().BeSameAs(config);
        var indexes = config.Indexes;
        indexes.Should().HaveCount(1);

        if (indexes != null)
        {
            var metaIndex = indexes[0] as MetadataIndexDefinition;
            metaIndex.Should().NotBeNull();
            metaIndex!.MetadataName.Should().Be("author");
            metaIndex.ValueType.Should().Be(XdmValueType.XdmString);
        }
    }

    [Fact]
    public void AddMetadataIndex_WithValueType_AddsIndex()
    {
        var config = new IndexConfiguration();

        var result = config.AddMetadataIndex("score", XdmValueType.XdmInteger);

        result.Should().BeSameAs(config);
        var metaIndex = config.Indexes[0] as MetadataIndexDefinition;
        metaIndex.Should().NotBeNull();
        metaIndex!.MetadataName.Should().Be("score");
        metaIndex.ValueType.Should().Be(XdmValueType.XdmInteger);
    }

    [Theory]
    [InlineData("author")]
    [InlineData("created-date")]
    [InlineData("content_type")]
    [InlineData("category.subcategory")]
    public void AddMetadataIndex_AcceptsVariousNames(string metadataName)
    {
        var config = new IndexConfiguration();

        config.AddMetadataIndex(metadataName);

        var metaIndex = config.Indexes[0] as MetadataIndexDefinition;
        metaIndex!.MetadataName.Should().Be(metadataName);
    }

    [Fact]
    public void EnableStructuralIndex_True_EnablesIndex()
    {
        var config = new IndexConfiguration();

        var result = config.EnableStructuralIndex();

        result.Should().BeSameAs(config);
        config.StructuralIndexEnabled.Should().BeTrue();
    }

    [Fact]
    public void EnableStructuralIndex_False_DisablesIndex()
    {
        var config = new IndexConfiguration();

        var result = config.EnableStructuralIndex(false);

        result.Should().BeSameAs(config);
        config.StructuralIndexEnabled.Should().BeFalse();
    }

    [Fact]
    public void EnableStructuralIndex_DefaultParameter_EnablesIndex()
    {
        var config = new IndexConfiguration();
        config.EnableStructuralIndex(false); // First disable

        var result = config.EnableStructuralIndex(); // Enable with default

        result.Should().BeSameAs(config);
        config.StructuralIndexEnabled.Should().BeTrue();
    }

    [Fact]
    public void FluentChaining_AllMethodsChain()
    {
        var config = new IndexConfiguration()
            .AddNameIndex(new Uri("http://example.com"))
            .AddPathIndex("/root/element")
            .AddValueIndex("/root/price", XdmValueType.XdmDecimal)
            .AddFullTextIndex("//text")
            .AddMetadataIndex("author")
            .EnableStructuralIndex();

        config.Indexes.Should().HaveCount(5);
        config.StructuralIndexEnabled.Should().BeTrue();
    }

    [Fact]
    public void MultipleIndexesOfSameType_AllAdded()
    {
        var config = new IndexConfiguration()
            .AddPathIndex("/root/a")
            .AddPathIndex("/root/b")
            .AddPathIndex("/root/c");

        config.Indexes.Should().HaveCount(3);
        config.Indexes.OfType<PathIndexDefinition>().Should().HaveCount(3);
    }

    [Fact]
    public void MixedIndexTypes_AllAdded()
    {
        var config = new IndexConfiguration()
            .AddNameIndex((Uri?)null)
            .AddPathIndex("/root")
            .AddValueIndex("/price", XdmValueType.XdmDecimal)
            .AddFullTextIndex()
            .AddMetadataIndex("tag");

        var indexes = config.Indexes;
        indexes.Should().HaveCount(5);
        indexes.OfType<NameIndexDefinition>().Should().HaveCount(1);
        indexes.OfType<PathIndexDefinition>().Should().HaveCount(1);
        indexes.OfType<ValueIndexDefinition>().Should().HaveCount(1);
        indexes.OfType<FullTextIndexDefinition>().Should().HaveCount(1);
        indexes.OfType<MetadataIndexDefinition>().Should().HaveCount(1);
    }

    [Fact]
    public void GetIndexes_ReturnsReadOnlyList()
    {
        var config = new IndexConfiguration()
            .AddPathIndex("/root");

        var indexes = config.Indexes;

        indexes.Should().BeAssignableTo<IReadOnlyList<IndexDefinition>>();
    }
}

/// <summary>
/// Tests for XdmValueType enum.
/// </summary>
public class XdmValueTypeTests
{
    [Fact]
    public void AllValuesAreDefined()
    {
        var values = Enum.GetValues<XdmValueType>();

        values.Should().HaveCount(15);
        values.Should().Contain(XdmValueType.XdmString);
        values.Should().Contain(XdmValueType.XdmInteger);
        values.Should().Contain(XdmValueType.XdmLong);
        values.Should().Contain(XdmValueType.XdmDecimal);
        values.Should().Contain(XdmValueType.XdmDouble);
        values.Should().Contain(XdmValueType.XdmFloat);
        values.Should().Contain(XdmValueType.Boolean);
        values.Should().Contain(XdmValueType.DateTime);
        values.Should().Contain(XdmValueType.Date);
        values.Should().Contain(XdmValueType.Time);
        values.Should().Contain(XdmValueType.Duration);
        values.Should().Contain(XdmValueType.AnyUri);
        values.Should().Contain(XdmValueType.QName);
        values.Should().Contain(XdmValueType.Base64Binary);
        values.Should().Contain(XdmValueType.HexBinary);
    }

    [Fact]
    public void DefaultValue_IsXdmString()
    {
        var defaultValue = default(XdmValueType);
        defaultValue.Should().Be(XdmValueType.XdmString);
    }
}

/// <summary>
/// Tests for FullTextIndexOptions.
/// </summary>
public class FullTextIndexOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = FullTextIndexOptions.Default;

        options.Language.Should().Be("en");
        options.CaseSensitive.Should().BeFalse();
        options.Stemming.Should().BeTrue();
        options.StopWords.Should().BeNull();
    }

    [Fact]
    public void NewInstance_HasDefaultValues()
    {
        var options = new FullTextIndexOptions();

        options.Language.Should().Be("en");
        options.CaseSensitive.Should().BeFalse();
        options.Stemming.Should().BeTrue();
        options.StopWords.Should().BeNull();
    }

    [Fact]
    public void WithInit_SetsProperties()
    {
        var stopWords = new HashSet<string> { "the", "a", "an" };
        var options = new FullTextIndexOptions
        {
            Language = "de",
            CaseSensitive = true,
            Stemming = false,
            StopWords = stopWords
        };

        options.Language.Should().Be("de");
        options.CaseSensitive.Should().BeTrue();
        options.Stemming.Should().BeFalse();
        options.StopWords.Should().BeSameAs(stopWords);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    public void AcceptsVariousLanguages(string language)
    {
        var options = new FullTextIndexOptions { Language = language };
        options.Language.Should().Be(language);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var options1 = new FullTextIndexOptions
        {
            Language = "en",
            CaseSensitive = true,
            Stemming = false
        };
        var options2 = new FullTextIndexOptions
        {
            Language = "en",
            CaseSensitive = true,
            Stemming = false
        };

        options1.Should().Be(options2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var options1 = new FullTextIndexOptions { Language = "en" };
        var options2 = new FullTextIndexOptions { Language = "de" };

        options1.Should().NotBe(options2);
    }
}

/// <summary>
/// Tests for index definition record types.
/// </summary>
public class IndexDefinitionTests
{
    [Fact]
    public void NameIndexDefinition_IsIndexDefinition()
    {
        var index = new NameIndexDefinition();
        index.Should().BeAssignableTo<IndexDefinition>();
    }

    [Fact]
    public void PathIndexDefinition_IsIndexDefinition()
    {
        var index = new PathIndexDefinition { PathPattern = "/root" };
        index.Should().BeAssignableTo<IndexDefinition>();
    }

    [Fact]
    public void ValueIndexDefinition_IsIndexDefinition()
    {
        var index = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmString
        };
        index.Should().BeAssignableTo<IndexDefinition>();
    }

    [Fact]
    public void FullTextIndexDefinition_IsIndexDefinition()
    {
        var index = new FullTextIndexDefinition
        {
            Options = FullTextIndexOptions.Default
        };
        index.Should().BeAssignableTo<IndexDefinition>();
    }

    [Fact]
    public void MetadataIndexDefinition_IsIndexDefinition()
    {
        var index = new MetadataIndexDefinition
        {
            MetadataName = "author",
            ValueType = XdmValueType.XdmString
        };
        index.Should().BeAssignableTo<IndexDefinition>();
    }

    [Fact]
    public void NameIndexDefinition_Equality()
    {
        var index1 = new NameIndexDefinition { NamespaceUri = new Uri("http://example.com") };
        var index2 = new NameIndexDefinition { NamespaceUri = new Uri("http://example.com") };

        index1.Should().Be(index2);
    }

    [Fact]
    public void PathIndexDefinition_Equality()
    {
        var index1 = new PathIndexDefinition { PathPattern = "/root" };
        var index2 = new PathIndexDefinition { PathPattern = "/root" };

        index1.Should().Be(index2);
    }

    [Fact]
    public void ValueIndexDefinition_Equality()
    {
        var index1 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmDecimal,
            Collation = null
        };
        var index2 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmDecimal,
            Collation = null
        };

        index1.Should().Be(index2);
    }

    [Fact]
    public void FullTextIndexDefinition_Equality()
    {
        var options = FullTextIndexOptions.Default;
        var index1 = new FullTextIndexDefinition { PathPattern = "//text", Options = options };
        var index2 = new FullTextIndexDefinition { PathPattern = "//text", Options = options };

        index1.Should().Be(index2);
    }

    [Fact]
    public void MetadataIndexDefinition_Equality()
    {
        var index1 = new MetadataIndexDefinition
        {
            MetadataName = "author",
            ValueType = XdmValueType.XdmString
        };
        var index2 = new MetadataIndexDefinition
        {
            MetadataName = "author",
            ValueType = XdmValueType.XdmString
        };

        index1.Should().Be(index2);
    }

    [Fact]
    public void ValueIndexDefinition_WithCollation_Equality()
    {
        var collation = "http://www.w3.org/2013/collation/UCA?lang=en";
        var index1 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmString,
            Collation = collation
        };
        var index2 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmString,
            Collation = collation
        };

        index1.Should().Be(index2);
    }

    [Fact]
    public void ValueIndexDefinition_DifferentCollation_NotEqual()
    {
        var index1 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmString,
            Collation = "collation1"
        };
        var index2 = new ValueIndexDefinition
        {
            PathPattern = "/root",
            ValueType = XdmValueType.XdmString,
            Collation = "collation2"
        };

        index1.Should().NotBe(index2);
    }
}
