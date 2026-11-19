using AwesomeAssertions;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// <see cref="XsDateTime.IsDateTimeStamp"/> tells an <c>xs:dateTimeStamp</c> from an
/// <c>xs:dateTime</c> that has a timezone; without it the engines answered
/// <c>xs:dateTime('2026-01-01T00:00:00Z') instance of xs:dateTimeStamp</c> with true. It is a mark of
/// the type only: the two values are equal, hash alike and order alike.
/// </summary>
public class XsDateTimeStampMarkTests
{
    private static readonly XsDateTime DateTime = XsDateTime.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void A_parsed_value_is_an_xs_dateTime()
        => DateTime.IsDateTimeStamp.Should().BeFalse();

    [Fact]
    public void The_mark_takes_no_part_in_equality_hashing_or_ordering()
    {
        var stamp = DateTime with { IsDateTimeStamp = true };

        stamp.IsDateTimeStamp.Should().BeTrue();
        stamp.Equals(DateTime).Should().BeTrue();
        (stamp == DateTime).Should().BeTrue();
        stamp.GetHashCode().Should().Be(DateTime.GetHashCode());
        stamp.CompareTo(DateTime).Should().Be(0);
    }

    [Fact]
    public void Values_that_differ_are_still_unequal()
    {
        var other = XsDateTime.Parse("2026-01-01T00:00:01Z") with { IsDateTimeStamp = true };

        other.Equals(DateTime with { IsDateTimeStamp = true }).Should().BeFalse();
        (DateTime with { ExtendedYear = 12026 }).Equals(DateTime).Should().BeFalse();
        new XsDateTime(DateTime.Value, HasTimezone: false).Equals(DateTime).Should().BeFalse();
    }
}
