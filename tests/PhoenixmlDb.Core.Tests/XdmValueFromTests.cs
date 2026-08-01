using FluentAssertions;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

public class XdmValueFromTests
{
    [Fact]
    public void From_String_RoundTrips()
    {
        var v = XdmValue.From("pending");
        XdmValue.To<string>(v).Should().Be("pending");
    }

    [Fact]
    public void From_DateTimeOffset_RoundTrips()
    {
        var when = new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
        XdmValue.To<DateTimeOffset>(XdmValue.From(when)).Should().Be(when);
    }

    [Theory]
    [InlineData(42L)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void From_Long_RoundTrips(long value)
        => XdmValue.To<long>(XdmValue.From(value)).Should().Be(value);

    [Fact]
    public void From_Int_WidensToInteger_AndNarrowsBack()
        => XdmValue.To<int>(XdmValue.From(7)).Should().Be(7);

    [Fact]
    public void From_Bool_RoundTrips()
        => XdmValue.To<bool>(XdmValue.From(true)).Should().BeTrue();

    [Fact]
    public void IsSupportedClrType_RejectsArbitraryTypes()
    {
        XdmValue.IsSupportedClrType(typeof(string)).Should().BeTrue();
        XdmValue.IsSupportedClrType(typeof(XdmValueFromTests)).Should().BeFalse();
    }

    [Fact]
    public void From_UnsupportedType_Throws()
    {
        var act = () => XdmValue.From(new XdmValueFromTests());
        act.Should().Throw<NotSupportedException>()
           .WithMessage("*XdmValueFromTests*");
    }

    // The whole point of typed values: 1 and 1.0 must not be confused, and equality
    // must be value equality rather than encoding equality (spec section 7.1).
    [Fact]
    public void IntegerAndDouble_AreDistinctValues()
        => XdmValue.From(1L).Should().NotBe(XdmValue.From(1.0d));

    // --- Review finding #1: narrowing to short/byte (and int) must fail loudly on
    // overflow instead of silently wrapping. ---

    [Fact]
    public void To_Byte_OverflowingStoredInteger_ThrowsOverflowException()
    {
        var stored = XdmValue.From(300L); // out of byte range (0-255)
        var act = () => XdmValue.To<byte>(stored);
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void To_Short_OverflowingStoredInteger_ThrowsOverflowException()
    {
        var stored = XdmValue.From(int.MaxValue + 1L); // out of short range
        var act = () => XdmValue.To<short>(stored);
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void To_Int_OverflowingStoredInteger_ThrowsOverflowException()
    {
        var stored = XdmValue.From(long.MaxValue); // out of int range
        var act = () => XdmValue.To<int>(stored);
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void To_Byte_InRangeStoredInteger_RoundTrips()
        => XdmValue.To<byte>(XdmValue.From(200L)).Should().Be((byte)200);

    [Fact]
    public void To_Short_InRangeStoredInteger_RoundTrips()
        => XdmValue.To<short>(XdmValue.From(-30000L)).Should().Be((short)-30000);

    // --- Review finding #2: IsSupportedClrType and From<T> must agree for `object`. ---

    [Fact]
    public void IsSupportedClrType_AcceptsObject_MatchingFromObjectDispatch()
    {
        XdmValue.IsSupportedClrType(typeof(object)).Should().BeTrue();

        object boxedString = "pending";
        XdmValue.From(boxedString).Should().Be(XdmValue.XsString("pending"));
    }

    [Fact]
    public void To_Object_ReturnsNaturalClrRepresentation()
    {
        object roundTripped = XdmValue.To<object>(XdmValue.From(42L));
        roundTripped.Should().Be(42L);
    }

    [Fact]
    public void IsSupportedClrType_AcceptsXdmValue()
        => XdmValue.IsSupportedClrType(typeof(XdmValue)).Should().BeTrue();

    // --- Review finding #3 (ruling: To<T> is strict). A stored xs:string must not
    // silently coerce into a numeric CLR type, whether or not its content parses. ---

    [Fact]
    public void To_Int_FromStoredString_ThrowsInvalidCastException_EvenWhenParseable()
    {
        var stored = XdmValue.From("42"); // parses as a number, but is NOT stored as one
        var act = () => XdmValue.To<int>(stored);
        act.Should().Throw<InvalidCastException>()
           .WithMessage("*XsString*")
           .WithMessage("*Int32*");
    }

    [Fact]
    public void To_Int_FromStoredString_ThrowsInvalidCastException_WhenNotParseable()
    {
        var stored = XdmValue.From("abc");
        var act = () => XdmValue.To<int>(stored);
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void To_String_FromStoredInteger_ThrowsInvalidCastException()
    {
        var stored = XdmValue.From(42L);
        var act = () => XdmValue.To<string>(stored);
        act.Should().Throw<InvalidCastException>();
    }

    // --- Review finding #4: exercise every supported CLR type, including the named
    // float widening/narrowing case, plus the cross-family double/float accommodation. ---

    [Fact]
    public void From_Decimal_RoundTrips()
        => XdmValue.To<decimal>(XdmValue.From(12.5m)).Should().Be(12.5m);

    [Fact]
    public void From_Double_RoundTrips()
        => XdmValue.To<double>(XdmValue.From(12.5d)).Should().Be(12.5d);

    [Fact]
    public void From_Float_RoundTrips()
        => XdmValue.To<float>(XdmValue.From(12.5f)).Should().Be(12.5f);

    [Fact]
    public void To_Float_FromStoredDouble_Widens()
        // Stored as xs:double (From<double>), read back as float: the case the
        // reviewer named explicitly and the original tests never exercised.
        => XdmValue.To<float>(XdmValue.From(2.5d)).Should().Be(2.5f);

    [Fact]
    public void To_Double_FromStoredFloat_Widens()
        => XdmValue.To<double>(XdmValue.From(2.5f)).Should().Be(2.5d);

    [Fact]
    public void From_DateOnly_RoundTrips()
    {
        var date = new DateOnly(2026, 1, 15);
        XdmValue.To<DateOnly>(XdmValue.From(date)).Should().Be(date);
    }

    [Fact]
    public void From_TimeOnly_RoundTrips()
    {
        var time = new TimeOnly(9, 30, 0);
        XdmValue.To<TimeOnly>(XdmValue.From(time)).Should().Be(time);
    }

    [Fact]
    public void From_TimeSpan_RoundTrips()
    {
        var span = TimeSpan.FromMinutes(90);
        XdmValue.To<TimeSpan>(XdmValue.From(span)).Should().Be(span);
    }

    [Fact]
    public void From_Uri_RoundTrips()
    {
        var uri = new Uri("https://phoenixml.dev/");
        XdmValue.To<Uri>(XdmValue.From(uri)).Should().Be(uri);
    }

    [Fact]
    public void From_XdmQName_RoundTrips()
    {
        var qname = new XdmQName(NamespaceId.PhoenixmlMeta, "created");
        XdmValue.To<XdmQName>(XdmValue.From(qname)).Should().Be(qname);
    }

    [Fact]
    public void From_ByteArray_RoundTrips()
    {
        byte[] bytes = [1, 2, 3, 4];
        XdmValue.To<byte[]>(XdmValue.From(bytes)).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void From_Short_RoundTrips()
        => XdmValue.To<short>(XdmValue.From((short)123)).Should().Be((short)123);

    [Fact]
    public void From_Byte_RoundTrips()
        => XdmValue.To<byte>(XdmValue.From((byte)42)).Should().Be((byte)42);

    // --- Review finding #4 (data-driven): every entry in the supported-type surface
    // round-trips, via reflection over From<T>/To<T>. This is the drift guard finding
    // #5 asks for at the public-API level: if a type is added to the internal
    // conversion table but this list isn't updated to match, that's a visible, separate
    // maintenance signal rather than a silent gap. ---

    public static TheoryData<Type, object> SupportedTypeSamples() => new()
    {
        { typeof(string), "pending" },
        { typeof(bool), true },
        { typeof(long), 42L },
        { typeof(int), 7 },
        { typeof(short), (short)7 },
        { typeof(byte), (byte)7 },
        { typeof(decimal), 12.5m },
        { typeof(double), 12.5d },
        { typeof(float), 12.5f },
        { typeof(DateTimeOffset), new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero) },
        { typeof(DateOnly), new DateOnly(2026, 1, 15) },
        { typeof(TimeOnly), new TimeOnly(9, 30, 0) },
        { typeof(TimeSpan), TimeSpan.FromMinutes(90) },
        { typeof(Uri), new Uri("https://phoenixml.dev/") },
        { typeof(XdmQName), new XdmQName(NamespaceId.PhoenixmlMeta, "created") },
        { typeof(byte[]), new byte[] { 1, 2, 3 } },
    };

    [Theory]
    [MemberData(nameof(SupportedTypeSamples))]
    public void From_To_RoundTrips_ForEverySupportedClrType(Type clrType, object sample)
    {
        XdmValue.IsSupportedClrType(clrType).Should().BeTrue();

        var fromMethod = typeof(XdmValue).GetMethod(nameof(XdmValue.From))!.MakeGenericMethod(clrType);
        var xdmValue = (XdmValue)fromMethod.Invoke(null, [sample])!;

        var toMethod = typeof(XdmValue).GetMethod(nameof(XdmValue.To))!.MakeGenericMethod(clrType);
        var roundTripped = toMethod.Invoke(null, [xdmValue]);

        if (clrType == typeof(byte[]))
            ((byte[])roundTripped!).Should().BeEquivalentTo((byte[])sample);
        else
            roundTripped.Should().Be(sample);
    }
}
