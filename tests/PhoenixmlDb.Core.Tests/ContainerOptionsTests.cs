using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// Tests for ContainerOptions configuration.
/// </summary>
public class ContainerOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var options = new ContainerOptions();

        options.Indexes.Should().NotBeNull();
        options.DefaultNamespaces.Should().NotBeNull();
        options.DefaultNamespaces.Should().BeEmpty();
        options.ValidationMode.Should().Be(ValidationMode.None);
        options.PreserveWhitespace.Should().BeFalse();
    }

    [Fact]
    public void Indexes_IsNotNull()
    {
        var options = new ContainerOptions();

        options.Indexes.Should().NotBeNull();
        options.Indexes.Should().BeOfType<IndexConfiguration>();
    }

    [Fact]
    public void Indexes_CanBeConfigured()
    {
        var options = new ContainerOptions();

        options.Indexes
            .AddPathIndex("/root")
            .AddValueIndex("/price", XdmValueType.XdmDecimal);

        options.Indexes.Indexes.Should().HaveCount(2);
    }

    [Fact]
    public void DefaultNamespaces_CanBePopulated()
    {
        var options = new ContainerOptions();

        options.DefaultNamespaces["xs"] = "http://www.w3.org/2001/XMLSchema";
        options.DefaultNamespaces["xsi"] = "http://www.w3.org/2001/XMLSchema-instance";

        options.DefaultNamespaces.Should().HaveCount(2);
        options.DefaultNamespaces["xs"].Should().Be("http://www.w3.org/2001/XMLSchema");
        options.DefaultNamespaces["xsi"].Should().Be("http://www.w3.org/2001/XMLSchema-instance");
    }

    [Theory]
    [InlineData("")]
    [InlineData("prefix")]
    [InlineData("xml")]
    [InlineData("xs")]
    public void DefaultNamespaces_AcceptsVariousPrefixes(string prefix)
    {
        var options = new ContainerOptions();
        options.DefaultNamespaces[prefix] = "http://example.com";

        options.DefaultNamespaces.Should().ContainKey(prefix);
    }

    [Fact]
    public void ValidationMode_DefaultIsNone()
    {
        var options = new ContainerOptions();
        options.ValidationMode.Should().Be(ValidationMode.None);
    }

    [Theory]
    [InlineData(ValidationMode.None)]
    [InlineData(ValidationMode.Schema)]
    [InlineData(ValidationMode.WellFormed)]
    public void ValidationMode_CanBeSet(ValidationMode mode)
    {
        var options = new ContainerOptions();

        options.ValidationMode = mode;

        options.ValidationMode.Should().Be(mode);
    }

    [Fact]
    public void PreserveWhitespace_DefaultIsFalse()
    {
        var options = new ContainerOptions();
        options.PreserveWhitespace.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreserveWhitespace_CanBeSet(bool preserve)
    {
        var options = new ContainerOptions();

        options.PreserveWhitespace = preserve;

        options.PreserveWhitespace.Should().Be(preserve);
    }

    [Fact]
    public void FullConfiguration_AllPropertiesWork()
    {
        var options = new ContainerOptions
        {
            ValidationMode = ValidationMode.Schema,
            PreserveWhitespace = true
        };

        options.DefaultNamespaces["xs"] = "http://www.w3.org/2001/XMLSchema";

        options.Indexes
            .AddNameIndex((Uri?)null)
            .AddPathIndex("//element")
            .EnableStructuralIndex();

        options.ValidationMode.Should().Be(ValidationMode.Schema);
        options.PreserveWhitespace.Should().BeTrue();
        options.DefaultNamespaces.Should().ContainKey("xs");
        options.Indexes.Indexes.Should().HaveCount(2);
        options.Indexes.StructuralIndexEnabled.Should().BeTrue();
    }
}

/// <summary>
/// Tests for ValidationMode enum.
/// </summary>
public class ValidationModeTests
{
    [Fact]
    public void AllValuesAreDefined()
    {
        var values = Enum.GetValues<ValidationMode>();

        values.Should().HaveCount(3);
        values.Should().Contain(ValidationMode.None);
        values.Should().Contain(ValidationMode.Schema);
        values.Should().Contain(ValidationMode.WellFormed);
    }

    [Fact]
    public void DefaultValue_IsNone()
    {
        var defaultValue = default(ValidationMode);
        defaultValue.Should().Be(ValidationMode.None);
    }

    [Theory]
    [InlineData(ValidationMode.None, "None")]
    [InlineData(ValidationMode.Schema, "Schema")]
    [InlineData(ValidationMode.WellFormed, "WellFormed")]
    public void ToString_ReturnsExpectedValue(ValidationMode mode, string expected)
    {
        mode.ToString().Should().Be(expected);
    }
}

/// <summary>
/// Tests for DocumentOptions.
/// </summary>
public class DocumentOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var options = new DocumentOptions();

        options.ContentType.Should().BeNull();
        options.Metadata.Should().BeNull();
        options.Overwrite.Should().BeTrue();
    }

    [Fact]
    public void ContentType_CanBeSet()
    {
        var options = new DocumentOptions { ContentType = ContentType.Xml };
        options.ContentType.Should().Be(ContentType.Xml);

        options = new DocumentOptions { ContentType = ContentType.Json };
        options.ContentType.Should().Be(ContentType.Json);
    }

    [Fact]
    public void Metadata_CanBeSet()
    {
        var metadata = new Dictionary<string, object>
        {
            { "author", "John" },
            { "version", 1 }
        };

        var options = new DocumentOptions { Metadata = metadata };

        options.Metadata.Should().NotBeNull();
        options.Metadata!["author"].Should().Be("John");
        options.Metadata["version"].Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Overwrite_CanBeSet(bool overwrite)
    {
        var options = new DocumentOptions { Overwrite = overwrite };
        options.Overwrite.Should().Be(overwrite);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var options1 = new DocumentOptions
        {
            ContentType = ContentType.Xml,
            Overwrite = true
        };
        var options2 = new DocumentOptions
        {
            ContentType = ContentType.Xml,
            Overwrite = true
        };

        options1.Should().Be(options2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var options1 = new DocumentOptions { Overwrite = true };
        var options2 = new DocumentOptions { Overwrite = false };

        options1.Should().NotBe(options2);
    }
}

/// <summary>
/// Tests for ContentType enum.
/// </summary>
public class ContentTypeTests
{
    [Fact]
    public void AllValuesAreDefined()
    {
        var values = Enum.GetValues<ContentType>();

        values.Should().HaveCount(2);
        values.Should().Contain(ContentType.Xml);
        values.Should().Contain(ContentType.Json);
    }

    [Fact]
    public void DefaultValue_IsXml()
    {
        var defaultValue = default(ContentType);
        defaultValue.Should().Be(ContentType.Xml);
    }

    [Theory]
    [InlineData(ContentType.Xml, "Xml")]
    [InlineData(ContentType.Json, "Json")]
    public void ToString_ReturnsExpectedValue(ContentType contentType, string expected)
    {
        contentType.ToString().Should().Be(expected);
    }
}

/// <summary>
/// Tests for DocumentInfo record.
/// </summary>
public class DocumentInfoTests
{
    [Fact]
    public void RequiredProperties_MustBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var info = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test-document.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };

        info.Id.Should().Be(new DocumentId(42));
        info.Name.Should().Be("test-document.xml");
        info.Created.Should().Be(now);
        info.Modified.Should().Be(now);
        info.SizeBytes.Should().Be(1024);
        info.ContentType.Should().Be(ContentType.Xml);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var info1 = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };
        var info2 = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };

        info1.Should().Be(info2);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var info1 = new DocumentInfo
        {
            Id = new DocumentId(1),
            Name = "test.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };
        var info2 = new DocumentInfo
        {
            Id = new DocumentId(2),
            Name = "test.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };

        info1.Should().NotBe(info2);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var info1 = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test1.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };
        var info2 = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test2.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };

        info1.Should().NotBe(info2);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var now = DateTimeOffset.UtcNow;
        var info1 = new DocumentInfo
        {
            Id = new DocumentId(42),
            Name = "test.xml",
            Created = now,
            Modified = now,
            SizeBytes = 1024,
            ContentType = ContentType.Xml
        };

        var newModified = now.AddMinutes(5);
        var info2 = info1 with { Modified = newModified, SizeBytes = 2048 };

        info2.Id.Should().Be(info1.Id);
        info2.Name.Should().Be(info1.Name);
        info2.Created.Should().Be(info1.Created);
        info2.Modified.Should().Be(newModified);
        info2.SizeBytes.Should().Be(2048);
        info2.ContentType.Should().Be(info1.ContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("document.xml")]
    [InlineData("path/to/document.xml")]
    [InlineData("document with spaces.xml")]
    public void AcceptsVariousDocumentNames(string name)
    {
        var info = new DocumentInfo
        {
            Id = new DocumentId(1),
            Name = name,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
            SizeBytes = 100,
            ContentType = ContentType.Xml
        };

        info.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(1024L)]
    [InlineData(1024L * 1024L)]
    [InlineData(long.MaxValue)]
    public void AcceptsVariousSizes(long sizeBytes)
    {
        var info = new DocumentInfo
        {
            Id = new DocumentId(1),
            Name = "test.xml",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
            SizeBytes = sizeBytes,
            ContentType = ContentType.Xml
        };

        info.SizeBytes.Should().Be(sizeBytes);
    }

    [Fact]
    public void JsonContentType_Works()
    {
        var info = new DocumentInfo
        {
            Id = new DocumentId(1),
            Name = "data.json",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
            SizeBytes = 512,
            ContentType = ContentType.Json
        };

        info.ContentType.Should().Be(ContentType.Json);
    }

    [Fact]
    public void CreatedAndModified_CanBeDifferent()
    {
        var created = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var modified = new DateTimeOffset(2024, 6, 15, 12, 30, 0, TimeSpan.Zero);

        var info = new DocumentInfo
        {
            Id = new DocumentId(1),
            Name = "test.xml",
            Created = created,
            Modified = modified,
            SizeBytes = 100,
            ContentType = ContentType.Xml
        };

        info.Created.Should().Be(created);
        info.Modified.Should().Be(modified);
        info.Modified.Should().BeAfter(info.Created);
    }
}

/// <summary>
/// Tests for XdmNodeKind enum.
/// </summary>
public class XdmNodeKindTests
{
    [Fact]
    public void AllValuesAreDefined()
    {
        var values = Enum.GetValues<XdmNodeKind>();

        values.Should().HaveCount(8);
        values.Should().Contain(XdmNodeKind.None);
        values.Should().Contain(XdmNodeKind.Document);
        values.Should().Contain(XdmNodeKind.Element);
        values.Should().Contain(XdmNodeKind.Attribute);
        values.Should().Contain(XdmNodeKind.Text);
        values.Should().Contain(XdmNodeKind.Comment);
        values.Should().Contain(XdmNodeKind.ProcessingInstruction);
        values.Should().Contain(XdmNodeKind.Namespace);
    }

    [Fact]
    public void DefaultValue_IsNone()
    {
        var defaultValue = default(XdmNodeKind);
        defaultValue.Should().Be(XdmNodeKind.None);
    }

    [Theory]
    [InlineData(XdmNodeKind.None, 0)]
    [InlineData(XdmNodeKind.Document, 1)]
    [InlineData(XdmNodeKind.Element, 2)]
    [InlineData(XdmNodeKind.Attribute, 3)]
    [InlineData(XdmNodeKind.Text, 4)]
    [InlineData(XdmNodeKind.Comment, 5)]
    [InlineData(XdmNodeKind.ProcessingInstruction, 6)]
    [InlineData(XdmNodeKind.Namespace, 7)]
    public void UnderlyingValues_AreCorrect(XdmNodeKind kind, byte expectedValue)
    {
        ((byte)kind).Should().Be(expectedValue);
    }

    [Fact]
    public void IsByteEnum()
    {
        var underlyingType = Enum.GetUnderlyingType(typeof(XdmNodeKind));
        underlyingType.Should().Be<byte>();
    }
}
