using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// The implicit timezone is the machine's unless a host sets it, and a comparison of a value
/// with no timezone against one with a timezone uses the one in effect.
/// </summary>
public class ImplicitTimezoneTests
{
    [Fact]
    public void It_is_the_timezone_of_the_machine_until_a_host_sets_it()
    {
        ImplicitTimezone.Configured.Should().BeNull();
        ImplicitTimezone.Current.Should().Be(DateTimeOffset.Now.Offset);
    }

    [Fact]
    public void A_scope_sets_it_and_puts_back_what_was_there()
    {
        using (ImplicitTimezone.Use(TimeSpan.FromHours(5)))
        {
            ImplicitTimezone.Current.Should().Be(TimeSpan.FromHours(5));
            using (ImplicitTimezone.Use(TimeSpan.FromHours(-8)))
                ImplicitTimezone.Current.Should().Be(TimeSpan.FromHours(-8));
            ImplicitTimezone.Current.Should().Be(TimeSpan.FromHours(5));
            using (ImplicitTimezone.Use(null))
                ImplicitTimezone.Configured.Should().BeNull();
            ImplicitTimezone.Current.Should().Be(TimeSpan.FromHours(5));
        }
        ImplicitTimezone.Configured.Should().BeNull();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-15)]
    public void An_offset_outside_the_range_of_a_timezone_is_refused(int hours)
    {
        var act = () => ImplicitTimezone.Use(TimeSpan.FromHours(hours));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void An_offset_that_is_not_whole_minutes_is_refused()
    {
        var act = () => ImplicitTimezone.Use(TimeSpan.FromSeconds(90));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// 12:00 with no timezone against 12:00+01:00: earlier, the same and later as the implicit
    /// timezone is ahead of, equal to and behind +01:00.
    /// </summary>
    [Theory]
    [InlineData(2, -1)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void A_dateTime_with_no_timezone_is_compared_in_the_implicit_one(int implicitHours, int expected)
    {
        var plain = XsDateTime.Parse("2020-01-01T12:00:00");
        var zoned = XsDateTime.Parse("2020-01-01T12:00:00+01:00");
        using (ImplicitTimezone.Use(TimeSpan.FromHours(implicitHours)))
            Math.Sign(plain.CompareTo(zoned)).Should().Be(expected);
    }

    [Theory]
    [InlineData(2, -1)]
    [InlineData(1, 0)]
    [InlineData(-5, 1)]
    public void And_so_are_a_time_and_a_date(int implicitHours, int expected)
    {
        using (ImplicitTimezone.Use(TimeSpan.FromHours(implicitHours)))
        {
            Math.Sign(XsTime.Parse("12:00:00").CompareTo(XsTime.Parse("12:00:00+01:00"))).Should().Be(expected);
            Math.Sign(XsDate.Parse("2020-01-01").CompareTo(XsDate.Parse("2020-01-01+01:00"))).Should().Be(expected);
        }
    }

    [Fact]
    public async Task Each_asynchronous_flow_has_its_own()
    {
        async Task<TimeSpan> In(int hours)
        {
            using (ImplicitTimezone.Use(TimeSpan.FromHours(hours)))
            {
                await Task.Delay(20).ConfigureAwait(true);
                return ImplicitTimezone.Current;
            }
        }

        var results = await Task.WhenAll(In(3), In(-3), In(7)).ConfigureAwait(true);

        results.Should().Equal(TimeSpan.FromHours(3), TimeSpan.FromHours(-3), TimeSpan.FromHours(7));
        ImplicitTimezone.Configured.Should().BeNull();
    }
}
