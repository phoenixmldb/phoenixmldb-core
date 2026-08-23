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
        => Status.ToString().Should().Be("dbxml:status");

    // The single most load-bearing property of MetadataProperty<T>: FromXdm is a bare
    // pass-through to XdmValue.To<T> (Task 2), so a stored value of the wrong XDM type
    // must fail loudly through the wrapper, not just through XdmValue directly.
    [Fact]
    public void FromXdm_TypeMismatch_Throws()
    {
        var stored = XdmValue.From(1024L); // xs:integer, not xs:string
        var act = () => Status.FromXdm(stored);
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void FromXdm_OverflowingNarrowing_Throws()
    {
        var byteProperty = new MetadataProperty<byte>(NamespaceId.PhoenixmlMeta, "count");
        var stored = XdmValue.From(300L); // xs:integer 300 doesn't fit in a byte
        var act = () => byteProperty.FromXdm(stored);
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void FromXdm_Empty_ReturnsNull_ForReferenceType()
        => Status.FromXdm(XdmValue.Empty).Should().BeNull();

    [Fact]
    public void FromXdm_Empty_Throws_ForNonNullableValueType()
    {
        var longProperty = new MetadataProperty<long>(NamespaceId.PhoenixmlMeta, "count");
        var act = () => longProperty.FromXdm(XdmValue.Empty);
        act.Should().Throw<InvalidCastException>();
    }

    // XdmQName is a value type (readonly record struct), so it follows the non-nullable
    // value-type arm too, not the reference-type arm — worth asserting explicitly since
    // it's easy to mistake for a reference type at a glance.
    [Fact]
    public void FromXdm_Empty_Throws_ForXdmQName()
    {
        var qnameProperty = new MetadataProperty<XdmQName>(NamespaceId.PhoenixmlMeta, "ref");
        var act = () => qnameProperty.FromXdm(XdmValue.Empty);
        act.Should().Throw<InvalidCastException>();
    }
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

    // Get<T> is a separate code path from MetadataProperty<T>.FromXdm (it goes through
    // the indexer first), so the strictness has to be verified through it independently.
    [Fact]
    public void Get_TypeMismatch_Throws()
    {
        var meta = Build();
        var wrongType = new MetadataProperty<DateTimeOffset>(Status.Namespace, Status.Name);
        var act = () => meta.Get(wrongType);
        act.Should().Throw<InvalidCastException>();
    }
}
