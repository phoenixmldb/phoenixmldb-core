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
}
