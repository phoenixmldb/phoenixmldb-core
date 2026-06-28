using FluentAssertions;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for proleptic-Gregorian date/dateTime ± duration arithmetic, including the
/// year-1 / year-0 / negative-year boundary that the .NET DateOnly/DateTimeOffset types clamp.
/// </summary>
public class ProlepticDateArithmeticTests
{
    // ----- XsDate.AddDays across the year-1 boundary -----

    [Fact]
    public void XsDate_AddDays_CrossesBelowYearOne()
    {
        // 0001-01-01 - 11 days -> 0000-12-21 (proleptic year 0)
        var date = XsDate.Parse("0001-01-01Z");
        var result = date.AddDays(-11);
        result.ToString().Should().Be("0000-12-21Z");
        result.EffectiveYear.Should().Be(0);
    }

    [Fact]
    public void XsDate_AddDays_DurationStyleSubtraction()
    {
        // QT3 op-subtract-dayTimeDuration-from-date-8:
        // 0001-01-01Z - P11DT02H02M -> 0000-12-20Z (whole-day floor after time carry)
        var date = XsDate.Parse("0001-01-01Z");
        // 11 days + 2h2m, expressed as whole days for AddDays is 11; the engine path handles the time.
        var result = date.AddDays(-12); // floor of -11d2h2m in days
        result.ToString().Should().Be("0000-12-20Z");
    }

    [Fact]
    public void XsDate_AddDays_RoundTripBackAboveYearOne()
    {
        var date = XsDate.Parse("0000-12-21Z");
        var result = date.AddDays(11);
        result.ToString().Should().Be("0001-01-01Z");
        result.ExtendedYear.Should().BeNull();
    }

    [Fact]
    public void XsDate_AddDays_NegativeYear()
    {
        // 0001-01-01 - 366 days lands in year 0 (a leap year: divisible by 4, but not 100)
        var date = XsDate.Parse("0001-01-01Z");
        var result = date.AddDays(-366);
        result.ToString().Should().Be("0000-01-01Z");
    }

    // ----- XsDate.AddMonths across the year-1 boundary -----

    [Fact]
    public void XsDate_AddMonths_CrossesIntoNegativeYear()
    {
        // QT3 op-add-yearMonthDuration-to-date-8: 0001-01 + (-P20Y07M) -> -0020-06
        var date = XsDate.Parse("0001-01-01Z");
        var result = date.AddMonths(-(20 * 12 + 7));
        result.ToString().Should().Be("-0020-06-01Z");
        result.EffectiveYear.Should().Be(-20);
    }

    [Fact]
    public void XsDate_AddMonths_ClampsDayToShorterMonth()
    {
        // 2024-01-31 + 1 month -> 2024-02-29 (2024 is a leap year)
        var date = XsDate.Parse("2024-01-31");
        date.AddMonths(1).ToString().Should().Be("2024-02-29");
    }

    [Fact]
    public void XsDate_AddMonths_BackAboveYearOne()
    {
        // -0020-06 + P20Y07M -> 0001-01
        var date = XsDate.Parse("-0020-06-01Z");
        var result = date.AddMonths(20 * 12 + 7);
        result.ToString().Should().Be("0001-01-01Z");
        result.ExtendedYear.Should().BeNull();
    }

    // ----- XsDateTime.Add across the year-1 boundary -----

    [Fact]
    public void XsDateTime_Add_CrossesBelowYearOneWithTimeCarry()
    {
        // QT3 op-add-dayTimeDuration-to-dateTime-8:
        // 0001-01-01T11:11:11Z + (-P11DT02H02M) -> 0000-12-21T09:09:11Z
        var dt = XsDateTime.Parse("0001-01-01T11:11:11Z");
        var delta = -(System.TimeSpan.FromDays(11) + System.TimeSpan.FromHours(2) + System.TimeSpan.FromMinutes(2));
        dt.Add(delta).ToString().Should().Be("0000-12-21T09:09:11Z");
    }

    [Fact]
    public void XsDateTime_Add_RoundTrip()
    {
        var dt = XsDateTime.Parse("0000-12-21T09:09:11Z");
        var delta = System.TimeSpan.FromDays(11) + System.TimeSpan.FromHours(2) + System.TimeSpan.FromMinutes(2);
        var result = dt.Add(delta);
        result.ToString().Should().Be("0001-01-01T11:11:11Z");
        result.ExtendedYear.Should().BeNull();
    }

    [Fact]
    public void XsDateTime_AddMonths_CrossesIntoNegativeYear()
    {
        // QT3 op-add-yearMonthDuration-to-dateTime-8:
        // 0001-01-01T01:01:01Z + (-P20Y07M) -> -0020-06-01T01:01:01Z
        var dt = XsDateTime.Parse("0001-01-01T01:01:01Z");
        dt.AddMonths(-(20 * 12 + 7)).ToString().Should().Be("-0020-06-01T01:01:01Z");
    }

    [Fact]
    public void XsDateTime_AddMonths_PreservesTimeAndTimezone()
    {
        var dt = XsDateTime.Parse("2024-01-31T13:45:30+05:00");
        var result = dt.AddMonths(1);
        result.ToString().Should().Be("2024-02-29T13:45:30+05:00");
    }

    // ----- ToString round-trip for the extended-year forms -----

    [Theory]
    [InlineData("0000-12-20Z")]
    [InlineData("-0001-12-20Z")]
    [InlineData("-0020-06-01Z")]
    [InlineData("0021-08-01Z")]
    public void XsDate_ToString_RoundTrip(string s)
    {
        XsDate.Parse(s).ToString().Should().Be(s);
    }

    [Theory]
    [InlineData("0000-12-21T09:09:11Z")]
    [InlineData("-0020-06-01T01:01:01Z")]
    [InlineData("0021-08-01T01:01:01Z")]
    public void XsDateTime_ToString_RoundTrip(string s)
    {
        XsDateTime.Parse(s).ToString().Should().Be(s);
    }
}
