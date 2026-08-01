using FluentAssertions;
using PhoenixmlDb.Core.Metadata;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

public class MetadataPropertyTests
{
    private static readonly MetadataProperty<string> Status =
        new(NamespaceId.PhoenixmlMeta, "status");

    [Fact]
    public void QName_CombinesNamespaceAndName()
    {
        Status.QName.Namespace.Should().Be(NamespaceId.PhoenixmlMeta);
        Status.QName.LocalName.Should().Be("status");
    }

    [Fact]
    public void ToXdm_And_FromXdm_RoundTrip()
        => Status.FromXdm(Status.ToXdm("pending")).Should().Be("pending");

    [Fact]
    public void TypedProperty_RoundTripsDateTimeOffset()
    {
        var created = new MetadataProperty<DateTimeOffset>(NamespaceId.DcTerms, "created");
        var when = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);
        created.FromXdm(created.ToXdm(when)).Should().Be(when);
    }

    // The defect this whole design removes: under the old "namespace:key" concatenation,
    // ("a", "b:c") and ("a:b", "c") produced the identical stored key.
    [Fact]
    public void DistinctNamespaceAndNameCombinations_ProduceDistinctQNames()
    {
        var nsA = new NamespaceId(100);
        var nsAb = new NamespaceId(101);

        var one = new MetadataProperty<string>(nsA, "b:c").QName;
        var two = new MetadataProperty<string>(nsAb, "c").QName;

        one.Should().NotBe(two);
    }

    [Fact]
    public void WellKnownProperties_AreOnTheReservedNamespaces()
    {
        PhxMeta.ContentType.Namespace.Should().Be(NamespaceId.PhoenixmlMeta);
        DcTerms.Created.Namespace.Should().Be(NamespaceId.DcTerms);
        DcTerms.Created.Name.Should().Be("created");
    }

    [Fact]
    public void Constructor_RejectsEmptyName()
    {
        var act = () => new MetadataProperty<string>(NamespaceId.PhoenixmlMeta, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RejectsUnsupportedClrType()
    {
        var act = () => new MetadataProperty<Guid>(NamespaceId.PhoenixmlMeta, "id");
        act.Should().Throw<NotSupportedException>();
    }

    // XdmValue.IsSupportedClrType(object) is true (From<object> dispatches on the runtime
    // value), but a MetadataProperty<object> would defeat the type safety the descriptor
    // exists to provide — FromXdm's return type would be object, forcing every call site
    // back into an unchecked cast. Reject it explicitly rather than let it compile silently.
    [Fact]
    public void Constructor_RejectsObject()
    {
        var act = () => new MetadataProperty<object>(NamespaceId.PhoenixmlMeta, "anything");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ToString_UsesConventionalPrefix()
        => Status.ToString().Should().Be("phxm:status");
}

public class MetadataCollectionTests
{
    private static readonly MetadataProperty<string> Status =
        new(NamespaceId.PhoenixmlMeta, "status");
    private static readonly MetadataProperty<long> Size =
        new(NamespaceId.PhoenixmlMeta, "size");

    private static MetadataCollection Build() => new(new Dictionary<XdmQName, XdmValue>
    {
        [Status.QName]           = XdmValue.From("pending"),
        [Size.QName]             = XdmValue.From(1024L),
        [DcTerms.Creator.QName]  = XdmValue.From("lucas"),
    });

    [Fact]
    public void TypedAccess_ReturnsTypedValue()
    {
        var meta = Build();
        meta.Get(Status).Should().Be("pending");
        meta.Get(Size).Should().Be(1024L);
    }

    [Fact]
    public void QNameIndexer_ReturnsXdmValue()
        => Build()[Status.QName].Should().Be(XdmValue.From("pending"));

    [Fact]
    public void MissingKey_ReturnsDefault()
        => Build().Get(new MetadataProperty<string>(NamespaceId.PhoenixmlMeta, "absent"))
            .Should().BeNull();

    // Replaces GetAllMetadataAsync returning unsplittable concatenated strings.
    [Fact]
    public void ByNamespace_GroupsWithoutReparsingStrings()
    {
        var groups = Build().ByNamespace.ToDictionary(g => g.Key, g => g.Count());
        groups[NamespaceId.PhoenixmlMeta].Should().Be(2);
        groups[NamespaceId.DcTerms].Should().Be(1);
    }

    [Fact]
    public void QNameIndexer_ReturnsNull_WhenAbsent()
        => Build()[new XdmQName(NamespaceId.PhoenixmlMeta, "absent")].Should().BeNull();

    [Fact]
    public void Contains_ReflectsPresenceOfProperty()
    {
        var meta = Build();
        meta.Contains(Status).Should().BeTrue();
        meta.Contains(new MetadataProperty<string>(NamespaceId.PhoenixmlMeta, "absent")).Should().BeFalse();
    }

    [Fact]
    public void Count_ReflectsNumberOfEntries()
        => Build().Count.Should().Be(3);

    [Fact]
    public void Empty_HasNoEntries()
        => MetadataCollection.Empty.Count.Should().Be(0);
}
