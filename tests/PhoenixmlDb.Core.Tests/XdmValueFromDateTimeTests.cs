using FluentAssertions;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// XdmValue.From accepts a DateTime whose Kind names an instant, as xs:dateTime, and its error
/// for an unsupported type names that type rather than the call site's type argument.
/// </summary>
/// <remarks>
/// From(DateTime) threw NotSupportedException ("No XDM representation for CLR type 'Object'")
/// while DateTimeOffset worked. Reported from the phoenixml.dev documentation audit (docs#38).
/// </remarks>
public class XdmValueFromDateTimeTests
{
    [Fact]
    public void UtcDateTime_IsAnXsDateTimeAtOffsetZero()
    {
        var v = XdmValue.From(new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc));
        v.Type.Should().Be(XdmType.DateTime);
        ((DateTimeOffset)v.RawValue!).Should().Be(new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.Zero));
        ((DateTimeOffset)v.RawValue!).Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void LocalDateTime_KeepsItsInstantAndTheLocalOffset()
    {
        var local = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Local);
        var v = XdmValue.From(local);
        var stored = (DateTimeOffset)v.RawValue!;
        stored.UtcDateTime.Should().Be(local.ToUniversalTime());
        stored.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(local));
    }

    [Fact]
    public void UnspecifiedDateTime_IsRefusedRatherThanGivenAGuessedOffset()
    {
        var act = () => XdmValue.From(new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Unspecified));
        act.Should().Throw<ArgumentException>().WithMessage("*Unspecified*SpecifyKind*");
    }

    [Fact]
    public void DateTime_ThroughObject_DispatchesOnTheRuntimeType()
        => XdmValue.From<object>(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Type.Should().Be(XdmType.DateTime);

    [Fact]
    public void StoredDateTime_ReadsBackAsTheSameUtcInstant()
    {
        var utc = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc);
        var back = XdmValue.To<DateTime>(XdmValue.From(utc));
        back.Should().Be(utc);
        back.Kind.Should().Be(DateTimeKind.Utc);
        XdmValue.To<DateTime>(XdmValue.From(new DateTimeOffset(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(2))))
            .Should().Be(utc);
    }

    [Fact]
    public void UnsupportedType_ErrorNamesTheRuntimeType()
    {
        var act = () => XdmValue.From<object>(new System.Text.StringBuilder());
        act.Should().Throw<NotSupportedException>().WithMessage("*'StringBuilder'*");
    }
}
