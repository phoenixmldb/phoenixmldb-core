using System;
using System.Globalization;

namespace PhoenixmlDb.Xdm;

/// <summary>
/// Represents an <c>xs:yearMonthDuration</c> value as a count of months, implementing
/// correct XML Schema semantics for month-based date arithmetic.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TimeSpan"/>, which only supports fixed-length durations, this type
/// correctly handles variable-length months. For example, adding 1 month to January 31
/// yields February 28/29, which cannot be expressed as a fixed number of days.
/// </para>
/// <para>
/// The canonical lexical form is <c>PnYnM</c> (e.g., <c>P1Y6M</c> for 18 months).
/// Negative durations are prefixed with <c>-</c> (e.g., <c>-P3M</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var dur = YearMonthDuration.Parse("P1Y6M");
/// Console.WriteLine(dur.TotalMonths); // 18
/// Console.WriteLine(dur.Years);       // 1
/// Console.WriteLine(dur.Months);      // 6
/// </code>
/// </example>
/// <param name="TotalMonths">The total number of months (negative for negative durations).</param>
public readonly record struct YearMonthDuration(int TotalMonths) : IComparable<YearMonthDuration>
{
    public int Years => TotalMonths / 12;
    public int Months => TotalMonths % 12;

    public static YearMonthDuration Parse(string s)
    {
        s = s.Trim();
        var negative = s.StartsWith('-');
        if (negative) s = s[1..];
        if (!s.StartsWith('P')) throw new FormatException($"Invalid yearMonthDuration: {s}");
        s = s[1..]; // skip 'P'

        // xs:yearMonthDuration must not have day/time components
        if (s.Contains('D', StringComparison.Ordinal) || s.Contains('T', StringComparison.Ordinal))
            throw new FormatException($"Invalid yearMonthDuration: contains day/time components");

        var years = 0;
        var months = 0;
        var yIdx = s.IndexOf('Y', StringComparison.Ordinal);
        if (yIdx >= 0)
        {
            years = int.Parse(s[..yIdx], CultureInfo.InvariantCulture);
            s = s[(yIdx + 1)..];
        }
        var mIdx = s.IndexOf('M', StringComparison.Ordinal);
        if (mIdx >= 0)
        {
            months = int.Parse(s[..mIdx], CultureInfo.InvariantCulture);
        }
        var total = years * 12 + months;
        return new YearMonthDuration(negative ? -total : total);
    }

    public static YearMonthDuration Multiply(YearMonthDuration d, double factor) =>
        new((int)Math.Round(d.TotalMonths * factor));

    public static YearMonthDuration Add(YearMonthDuration a, YearMonthDuration b) =>
        new(a.TotalMonths + b.TotalMonths);

    public static YearMonthDuration Subtract(YearMonthDuration a, YearMonthDuration b) =>
        new(a.TotalMonths - b.TotalMonths);

    public static YearMonthDuration Negate(YearMonthDuration d) =>
        new(-d.TotalMonths);

    public static YearMonthDuration operator *(YearMonthDuration d, double factor) => Multiply(d, factor);
    public static YearMonthDuration operator *(double factor, YearMonthDuration d) => Multiply(d, factor);
    public static YearMonthDuration operator +(YearMonthDuration a, YearMonthDuration b) => Add(a, b);
    public static YearMonthDuration operator -(YearMonthDuration a, YearMonthDuration b) => Subtract(a, b);
    public static YearMonthDuration operator -(YearMonthDuration d) => Negate(d);

    public static bool operator <(YearMonthDuration left, YearMonthDuration right) => left.TotalMonths < right.TotalMonths;
    public static bool operator >(YearMonthDuration left, YearMonthDuration right) => left.TotalMonths > right.TotalMonths;
    public static bool operator <=(YearMonthDuration left, YearMonthDuration right) => left.TotalMonths <= right.TotalMonths;
    public static bool operator >=(YearMonthDuration left, YearMonthDuration right) => left.TotalMonths >= right.TotalMonths;

    public int CompareTo(YearMonthDuration other) => TotalMonths.CompareTo(other.TotalMonths);

    public override string ToString()
    {
        var abs = Math.Abs(TotalMonths);
        var y = abs / 12;
        var m = abs % 12;
        var prefix = TotalMonths < 0 ? "-" : "";
        if (y > 0 && m > 0) return $"{prefix}P{y}Y{m}M";
        if (y > 0) return $"{prefix}P{y}Y";
        return $"{prefix}P{m}M";
    }
}

/// <summary>
/// Represents an <c>xs:dayTimeDuration</c> value as a total seconds count using
/// <see cref="decimal"/> precision, supporting arbitrarily large durations.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TimeSpan"/>, which is limited to approximately ±10,000 days and
/// 100-nanosecond tick precision, this type uses <see cref="decimal"/> for the total seconds
/// count. This allows representation of durations like <c>P1234567890D</c> without overflow.
/// </para>
/// <para>
/// The canonical lexical form is <c>PnDTnHnMnS</c> (e.g., <c>P2DT3H30M</c> for 2 days,
/// 3 hours, and 30 minutes). Fractional seconds are supported.
/// </para>
/// </remarks>
/// <param name="TotalSeconds">The total seconds as a <see cref="decimal"/> (negative for negative durations).</param>
public readonly record struct DayTimeDuration(decimal TotalSeconds) : IComparable<DayTimeDuration>
{
    public long Days => (long)(TotalSeconds / 86400m);
    public int Hours => (int)(Math.Abs(TotalSeconds) % 86400m / 3600m);
    public int Minutes => (int)(Math.Abs(TotalSeconds) % 3600m / 60m);
    public decimal Seconds => Math.Abs(TotalSeconds) % 60m;

    public bool IsNegative => TotalSeconds < 0;

    /// <summary>Converts to TimeSpan if within range, otherwise throws.</summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks((long)(TotalSeconds * TimeSpan.TicksPerSecond));

    public static DayTimeDuration FromTimeSpan(TimeSpan ts) => new((decimal)ts.Ticks / TimeSpan.TicksPerSecond);

    public static DayTimeDuration Parse(string s)
    {
        s = s.Trim();
        var negative = s.StartsWith('-');
        if (negative) s = s[1..];
        if (!s.StartsWith('P')) throw new FormatException($"Invalid dayTimeDuration: {s}");
        s = s[1..]; // skip P

        // Must not have year/month components
        var tIdx = s.IndexOf('T', StringComparison.Ordinal);
        var datePart = tIdx >= 0 ? s[..tIdx] : s;
        var timePart = tIdx >= 0 ? s[(tIdx + 1)..] : "";

        if (datePart.Contains('Y', StringComparison.Ordinal) || datePart.Contains('M', StringComparison.Ordinal))
            throw new FormatException($"Invalid dayTimeDuration: contains year/month components");

        decimal totalSeconds = 0;

        // Parse days from date part
        var dIdx = datePart.IndexOf('D', StringComparison.Ordinal);
        if (dIdx >= 0)
        {
            totalSeconds += decimal.Parse(datePart[..dIdx], CultureInfo.InvariantCulture) * 86400m;
        }

        // Parse time part: nHnMnS
        if (timePart.Length > 0)
        {
            var pos = 0;
            while (pos < timePart.Length)
            {
                var start = pos;
                while (pos < timePart.Length && (char.IsDigit(timePart[pos]) || timePart[pos] == '.')) pos++;
                if (pos >= timePart.Length) break;
                var num = decimal.Parse(timePart[start..pos], CultureInfo.InvariantCulture);
                var suffix = timePart[pos]; pos++;
                switch (suffix)
                {
                    case 'H': totalSeconds += num * 3600m; break;
                    case 'M': totalSeconds += num * 60m; break;
                    case 'S': totalSeconds += num; break;
                }
            }
        }

        return new DayTimeDuration(negative ? -totalSeconds : totalSeconds);
    }

    public static DayTimeDuration Multiply(DayTimeDuration d, double factor) =>
        new(d.TotalSeconds * (decimal)factor);

    public static DayTimeDuration Add(DayTimeDuration a, DayTimeDuration b) =>
        new(a.TotalSeconds + b.TotalSeconds);

    public static DayTimeDuration Subtract(DayTimeDuration a, DayTimeDuration b) =>
        new(a.TotalSeconds - b.TotalSeconds);

    public static DayTimeDuration Negate(DayTimeDuration d) =>
        new(-d.TotalSeconds);

    public static DayTimeDuration operator *(DayTimeDuration d, double factor) => Multiply(d, factor);
    public static DayTimeDuration operator *(double factor, DayTimeDuration d) => Multiply(d, factor);
    public static DayTimeDuration operator +(DayTimeDuration a, DayTimeDuration b) => Add(a, b);
    public static DayTimeDuration operator -(DayTimeDuration a, DayTimeDuration b) => Subtract(a, b);
    public static DayTimeDuration operator -(DayTimeDuration d) => Negate(d);

    public static bool operator <(DayTimeDuration left, DayTimeDuration right) => left.TotalSeconds < right.TotalSeconds;
    public static bool operator >(DayTimeDuration left, DayTimeDuration right) => left.TotalSeconds > right.TotalSeconds;
    public static bool operator <=(DayTimeDuration left, DayTimeDuration right) => left.TotalSeconds <= right.TotalSeconds;
    public static bool operator >=(DayTimeDuration left, DayTimeDuration right) => left.TotalSeconds >= right.TotalSeconds;

    public int CompareTo(DayTimeDuration other) => TotalSeconds.CompareTo(other.TotalSeconds);

    public override string ToString()
    {
        var abs = Math.Abs(TotalSeconds);
        var prefix = TotalSeconds < 0 ? "-" : "";
        var d = (long)(abs / 86400m);
        var remaining = abs % 86400m;
        var h = (int)(remaining / 3600m);
        remaining %= 3600m;
        var min = (int)(remaining / 60m);
        var sec = remaining % 60m;

        var sb = new System.Text.StringBuilder(20);
        sb.Append(prefix).Append('P');
        if (d > 0) sb.Append(d).Append('D');
        if (h > 0 || min > 0 || sec > 0)
        {
            sb.Append('T');
            if (h > 0) sb.Append(h).Append('H');
            if (min > 0) sb.Append(min).Append('M');
            if (sec > 0)
            {
                // Format seconds: integer or with fractional digits, no trailing zeros
                var intPart = (long)sec;
                var fracPart = sec - intPart;
                if (fracPart > 0)
                {
                    var fracStr = fracPart.ToString("G", CultureInfo.InvariantCulture);
                    // fracStr is like "0.125" — take everything after "0."
                    var dotIdx = fracStr.IndexOf('.', StringComparison.Ordinal);
                    sb.Append(intPart).Append('.').Append(fracStr.AsSpan(dotIdx + 1)).Append('S');
                }
                else
                {
                    sb.Append(intPart).Append('S');
                }
            }
        }
        if (sb.Length <= 1 + prefix.Length) sb.Append("T0S"); // P0D → PT0S
        return sb.ToString();
    }
}

/// <summary>
/// Represents an <c>xs:duration</c> value with both month and day-time components,
/// preserving the full XSD duration semantics that <see cref="TimeSpan"/> cannot.
/// </summary>
/// <remarks>
/// <para>
/// XSD durations have two independent components: a month count and a day-time span.
/// These cannot be combined into a single time value because the number of days in a month
/// varies. The <see cref="TotalMonths"/> and <see cref="DayTime"/> components are stored
/// separately to preserve this distinction.
/// </para>
/// <para>
/// <b>Comparison:</b> Durations are partially ordered — two durations with different
/// month and day-time ratios may be incomparable. This implementation compares months
/// first, then day-time, which is correct for durations that have the same ratio but may
/// give unexpected results for mixed durations.
/// </para>
/// </remarks>
/// <param name="TotalMonths">The month component (may be negative).</param>
/// <param name="DayTime">The day-time component as a <see cref="TimeSpan"/>.</param>
public readonly record struct XsDuration(int TotalMonths, TimeSpan DayTime) : IComparable<XsDuration>
{
    public int Years => Math.Abs(TotalMonths) / 12;
    public int Months => Math.Abs(TotalMonths) % 12;
    public bool IsNegative => TotalMonths < 0 || (TotalMonths == 0 && DayTime < TimeSpan.Zero);

    public static XsDuration Parse(string s)
    {
        s = s.Trim();
        var negative = s.StartsWith('-');
        if (negative) s = s[1..];
        if (!s.StartsWith('P')) throw new FormatException($"Invalid duration: {s}");
        s = s[1..]; // skip P

        int years = 0, months = 0;
        var tIdx = s.IndexOf('T', StringComparison.Ordinal);
        var datePart = tIdx >= 0 ? s[..tIdx] : s;
        var timePart = tIdx >= 0 ? s[(tIdx + 1)..] : "";

        // Parse date part: nYnMnD
        var pos = 0;
        while (pos < datePart.Length)
        {
            var start = pos;
            while (pos < datePart.Length && (char.IsDigit(datePart[pos]) || datePart[pos] == '.')) pos++;
            if (pos >= datePart.Length) break;
            var num = datePart[start..pos];
            var suffix = datePart[pos]; pos++;
            switch (suffix)
            {
                case 'Y': years = int.Parse(num, CultureInfo.InvariantCulture); break;
                case 'M': months = int.Parse(num, CultureInfo.InvariantCulture); break;
                case 'D': break; // days handled via TimeSpan below
            }
        }

        // Parse the full duration via XmlConvert for the day-time part
        var totalMonths = years * 12 + months;
        // Reconstruct just the day-time portion for TimeSpan
        var dayTimeStr = "P";
        // Extract days from datePart
        var dIdx = datePart.IndexOf('D', StringComparison.Ordinal);
        if (dIdx >= 0)
        {
            var dStart = dIdx - 1;
            while (dStart >= 0 && (char.IsDigit(datePart[dStart]) || datePart[dStart] == '.')) dStart--;
            dayTimeStr += datePart[(dStart + 1)..(dIdx + 1)];
        }
        if (timePart.Length > 0) dayTimeStr += "T" + timePart;
        var dayTime = dayTimeStr.Length > 1 ? System.Xml.XmlConvert.ToTimeSpan(dayTimeStr) : TimeSpan.Zero;

        if (negative) { totalMonths = -totalMonths; dayTime = -dayTime; }
        return new XsDuration(totalMonths, dayTime);
    }

    public int CompareTo(XsDuration other)
    {
        var mc = TotalMonths.CompareTo(other.TotalMonths);
        return mc != 0 ? mc : DayTime.CompareTo(other.DayTime);
    }

    public override string ToString()
    {
        var neg = IsNegative;
        var absMonths = Math.Abs(TotalMonths);
        var absDayTime = DayTime < TimeSpan.Zero ? -DayTime : DayTime;
        var y = absMonths / 12;
        var m = absMonths % 12;
        var d = absDayTime.Days;
        var h = absDayTime.Hours;
        var min = absDayTime.Minutes;
        var sec = absDayTime.Seconds;
        var ticks = absDayTime.Ticks % TimeSpan.TicksPerSecond;

        var sb = new System.Text.StringBuilder(20);
        if (neg) sb.Append('-');
        sb.Append('P');
        if (y > 0) sb.Append(y).Append('Y');
        if (m > 0) sb.Append(m).Append('M');
        if (d > 0) sb.Append(d).Append('D');

        if (h > 0 || min > 0 || sec > 0 || ticks > 0)
        {
            sb.Append('T');
            if (h > 0) sb.Append(h).Append('H');
            if (min > 0) sb.Append(min).Append('M');
            if (sec > 0 || ticks > 0)
            {
                sb.Append(sec);
                if (ticks > 0)
                {
                    var frac = ticks.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
                    sb.Append('.').Append(frac);
                }
                sb.Append('S');
            }
        }

        // Ensure at least P0M for zero duration
        if (sb.Length == 1 || (sb.Length == 2 && neg)) sb.Append("T0S");
        return sb.ToString();
    }

    public static bool operator <(XsDuration left, XsDuration right) => left.CompareTo(right) < 0;
    public static bool operator >(XsDuration left, XsDuration right) => left.CompareTo(right) > 0;
    public static bool operator <=(XsDuration left, XsDuration right) => left.CompareTo(right) <= 0;
    public static bool operator >=(XsDuration left, XsDuration right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Wrapper type for <c>xs:untypedAtomic</c> values, distinguishing them from <c>xs:string</c>
/// at the CLR type level.
/// </summary>
/// <remarks>
/// <para>
/// In XPath/XQuery, <c>xs:untypedAtomic</c> values are implicitly cast to the required type
/// during comparisons and arithmetic (e.g., cast to <c>xs:double</c> for numeric operations,
/// or to <c>xs:string</c> for string operations). This is different from <c>xs:string</c>,
/// which requires explicit casting to numeric types.
/// </para>
/// <para>
/// Implements <see cref="IConvertible"/> so that .NET's built-in conversion infrastructure
/// can perform these implicit casts, delegating to the underlying string value.
/// </para>
/// </remarks>
/// <param name="Value">The lexical string representation of the untyped value.</param>
public readonly record struct XsUntypedAtomic(string Value) : IConvertible
{
    public override string ToString() => Value;

    // IConvertible — delegate to string value
    TypeCode IConvertible.GetTypeCode() => TypeCode.String;
    bool IConvertible.ToBoolean(IFormatProvider? p) => ((IConvertible)Value).ToBoolean(p);
    byte IConvertible.ToByte(IFormatProvider? p) => ((IConvertible)Value).ToByte(p);
    char IConvertible.ToChar(IFormatProvider? p) => ((IConvertible)Value).ToChar(p);
    DateTime IConvertible.ToDateTime(IFormatProvider? p) => ((IConvertible)Value).ToDateTime(p);
    decimal IConvertible.ToDecimal(IFormatProvider? p) => ((IConvertible)Value).ToDecimal(p);
    double IConvertible.ToDouble(IFormatProvider? p) => ((IConvertible)Value).ToDouble(p);
    short IConvertible.ToInt16(IFormatProvider? p) => ((IConvertible)Value).ToInt16(p);
    int IConvertible.ToInt32(IFormatProvider? p) => ((IConvertible)Value).ToInt32(p);
    long IConvertible.ToInt64(IFormatProvider? p) => ((IConvertible)Value).ToInt64(p);
    sbyte IConvertible.ToSByte(IFormatProvider? p) => ((IConvertible)Value).ToSByte(p);
    float IConvertible.ToSingle(IFormatProvider? p) => ((IConvertible)Value).ToSingle(p);
    string IConvertible.ToString(IFormatProvider? p) => Value;
    object IConvertible.ToType(Type t, IFormatProvider? p) => ((IConvertible)Value).ToType(t, p);
    ushort IConvertible.ToUInt16(IFormatProvider? p) => ((IConvertible)Value).ToUInt16(p);
    uint IConvertible.ToUInt32(IFormatProvider? p) => ((IConvertible)Value).ToUInt32(p);
    ulong IConvertible.ToUInt64(IFormatProvider? p) => ((IConvertible)Value).ToUInt64(p);
}

/// <summary>
/// Wrapper type for <c>xs:anyURI</c> values, distinguishing them from plain strings
/// at the CLR type level.
/// </summary>
/// <remarks>
/// <para>
/// In XPath/XQuery, <c>xs:anyURI</c> values are promotable to <c>xs:string</c> — they
/// can be used anywhere a string is expected. This wrapper preserves the type distinction
/// so that type-aware operations (e.g., <c>instance of xs:anyURI</c>) work correctly.
/// </para>
/// <para>
/// Implements <see cref="IConvertible"/> to enable seamless participation in string operations.
/// </para>
/// </remarks>
/// <param name="Value">The URI string value.</param>
public readonly record struct XsAnyUri(string Value) : IConvertible
{
    public override string ToString() => Value;

    // IConvertible — delegate to string value
    TypeCode IConvertible.GetTypeCode() => TypeCode.String;
    bool IConvertible.ToBoolean(IFormatProvider? p) => ((IConvertible)Value).ToBoolean(p);
    byte IConvertible.ToByte(IFormatProvider? p) => ((IConvertible)Value).ToByte(p);
    char IConvertible.ToChar(IFormatProvider? p) => ((IConvertible)Value).ToChar(p);
    DateTime IConvertible.ToDateTime(IFormatProvider? p) => ((IConvertible)Value).ToDateTime(p);
    decimal IConvertible.ToDecimal(IFormatProvider? p) => ((IConvertible)Value).ToDecimal(p);
    double IConvertible.ToDouble(IFormatProvider? p) => ((IConvertible)Value).ToDouble(p);
    short IConvertible.ToInt16(IFormatProvider? p) => ((IConvertible)Value).ToInt16(p);
    int IConvertible.ToInt32(IFormatProvider? p) => ((IConvertible)Value).ToInt32(p);
    long IConvertible.ToInt64(IFormatProvider? p) => ((IConvertible)Value).ToInt64(p);
    sbyte IConvertible.ToSByte(IFormatProvider? p) => ((IConvertible)Value).ToSByte(p);
    float IConvertible.ToSingle(IFormatProvider? p) => ((IConvertible)Value).ToSingle(p);
    string IConvertible.ToString(IFormatProvider? p) => Value;
    object IConvertible.ToType(Type t, IFormatProvider? p) => ((IConvertible)Value).ToType(t, p);
    ushort IConvertible.ToUInt16(IFormatProvider? p) => ((IConvertible)Value).ToUInt16(p);
    uint IConvertible.ToUInt32(IFormatProvider? p) => ((IConvertible)Value).ToUInt32(p);
    ulong IConvertible.ToUInt64(IFormatProvider? p) => ((IConvertible)Value).ToUInt64(p);
}

/// <summary>
/// Represents an <c>xs:dateTime</c> value with explicit timezone tracking and support for
/// extended years outside the .NET <see cref="DateTimeOffset"/> range.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DateTimeOffset"/>, this type distinguishes between "no timezone"
/// and "UTC timezone" (<c>Z</c>). In XML Schema, <c>2024-01-15T10:00:00</c> (no timezone)
/// and <c>2024-01-15T10:00:00Z</c> (UTC) are semantically different — the former uses the
/// implicit timezone for comparisons, while the latter is explicitly UTC.
/// </para>
/// <para>
/// <b>Extended years:</b> XML Schema allows years outside the 1-9999 range (including year 0
/// and negative years in the proleptic Gregorian calendar). When <see cref="ExtendedYear"/>
/// is non-null, it holds the true year while <see cref="Value"/>'s year is clamped to a valid
/// .NET range for time-of-day storage.
/// </para>
/// <para>
/// <b>Comparison:</b> Follows the XPath Functions and Operators specification. Values with
/// timezones are normalized to UTC; values without timezones use the implicit (system) timezone.
/// </para>
/// </remarks>
public readonly record struct XsDateTime(DateTimeOffset Value, bool HasTimezone) : IComparable<XsDateTime>
{
    /// <summary>
    /// Extended year for dateTimes outside the .NET DateTimeOffset range (years &lt; 1 or &gt; 9999).
    /// When non-null, this is the true year; Value.Year is clamped for time storage.
    /// </summary>
    public long? ExtendedYear { get; init; }

    public static bool operator <(XsDateTime left, XsDateTime right) => left.CompareTo(right) < 0;
    public static bool operator >(XsDateTime left, XsDateTime right) => left.CompareTo(right) > 0;
    public static bool operator <=(XsDateTime left, XsDateTime right) => left.CompareTo(right) <= 0;
    public static bool operator >=(XsDateTime left, XsDateTime right) => left.CompareTo(right) >= 0;
    /// <summary>Fractional seconds preserved from parsing (sub-tick precision not needed, but format is preserved).</summary>
    public int FractionalTicks => (int)(Value.Ticks % TimeSpan.TicksPerSecond);

    /// <summary>The effective year, using ExtendedYear if available, otherwise Value.Year.</summary>
    public long EffectiveYear => ExtendedYear ?? Value.Year;

    public static XsDateTime Parse(string s)
    {
        s = s.Trim();

        // XSD: T24:00:00 is valid — normalize to next day T00:00:00
        // But T24:MM:SS with non-zero MM or SS is invalid (FORG0001)
        var tIdx = s.IndexOf('T', StringComparison.Ordinal);
        if (tIdx >= 0 && tIdx + 3 < s.Length && s.AsSpan(tIdx + 1, 2).SequenceEqual("24"))
        {
            // Validate that minutes, seconds, and fractional seconds are all zero
            var afterTime = s[(tIdx + 3)..]; // everything after "T24"
            var timeRest = afterTime;
            var plusIdx = timeRest.IndexOf('+', 1);
            var minusIdx = timeRest.IndexOf('-', 1);
            var zIdx = timeRest.IndexOf('Z', StringComparison.Ordinal);
            var tzStart = timeRest.Length;
            if (plusIdx >= 0) tzStart = Math.Min(tzStart, plusIdx);
            if (minusIdx >= 0) tzStart = Math.Min(tzStart, minusIdx);
            if (zIdx >= 0) tzStart = Math.Min(tzStart, zIdx);
            var timePortion = timeRest[..tzStart];
            if (!timePortion.StartsWith(":00:00", StringComparison.Ordinal))
                throw new FormatException("Hour 24 is only valid with minutes and seconds of 00:00");
            if (timePortion.Length > 6)
            {
                var fractional = timePortion[6..];
                if (!fractional.StartsWith('.'))
                    throw new FormatException("Hour 24 is only valid with minutes and seconds of 00:00");
                if (fractional[1..].Any(c => c != '0'))
                    throw new FormatException("Hour 24 is only valid with fractional seconds of zero");
            }
            var datePart = s[..tIdx];
            s = datePart + "T00" + afterTime;
            var hasTzMid = HasTimezoneIndicator(s);
            // Try standard parsing first
            if (TryParseStandard(s, hasTzMid, out var result))
                return new XsDateTime(result.Value.AddDays(1), hasTzMid);
            // Extended year — parse manually, add 1 day conceptually
            var extended = ParseExtendedDateTime(s, hasTzMid);
            // For T24:00:00 rollover, increment day (simplified: just note it)
            return extended;
        }

        var hasTz = HasTimezoneIndicator(s);

        // Check if this is an extended year (negative, year 0, or > 9999)
        bool isExtendedDt = s.StartsWith('-') || s.StartsWith("0000", StringComparison.Ordinal);
        if (!isExtendedDt)
        {
            var tPos = s.IndexOf('T', StringComparison.Ordinal);
            var yearEnd = tPos >= 0 ? tPos : s.Length;
            // Find first '-' after potential year digits
            var firstDash = s.IndexOf('-', StringComparison.Ordinal);
            if (firstDash > 4) isExtendedDt = true;
        }

        if (!isExtendedDt)
        {
            // Standard year range — use .NET parsing (throws on invalid dates)
            if (TryParseStandard(s, hasTz, out var standardResult))
                return standardResult;
            // .NET couldn't parse it — let it throw with a clear error
            if (!hasTz)
            {
                var dt = System.DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.None);
                return new XsDateTime(new DateTimeOffset(dt, TimeSpan.Zero), false);
            }
            var dtoFallback = DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
            return new XsDateTime(dtoFallback, true);
        }

        // Extended year parsing for years outside 1-9999 or year 0
        return ParseExtendedDateTime(s, hasTz);
    }

    private static bool TryParseStandard(string s, bool hasTz, out XsDateTime result)
    {
        if (!hasTz)
        {
            if (System.DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                result = new XsDateTime(new DateTimeOffset(dt, TimeSpan.Zero), false);
                return true;
            }
        }
        else
        {
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                result = new XsDateTime(dto, true);
                return true;
            }
        }
        result = default;
        return false;
    }

    private static XsDateTime ParseExtendedDateTime(string s, bool hasTz)
    {
        // XSD dateTime format: [-]YYYY-MM-DDThh:mm:ss[.fff][timezone]
        // Extract timezone first
        TimeSpan tzOffset = TimeSpan.Zero;
        var corePart = s;
        if (s.EndsWith('Z'))
        {
            corePart = s[..^1];
        }
        else if (s.Length >= 6)
        {
            var signIdx = s.Length - 6;
            if (signIdx > 0 && (s[signIdx] == '+' || s[signIdx] == '-') && s[signIdx + 3] == ':')
            {
                var tzStr = s[signIdx..];
                var hours = int.Parse(tzStr[1..3], CultureInfo.InvariantCulture);
                var mins = int.Parse(tzStr[4..6], CultureInfo.InvariantCulture);
                tzOffset = new TimeSpan(hours, mins, 0);
                if (tzStr[0] == '-') tzOffset = -tzOffset;
                corePart = s[..signIdx];
            }
        }

        // Split on 'T'
        var tIdx = corePart.IndexOf('T', StringComparison.Ordinal);
        if (tIdx < 0)
            throw new FormatException($"Invalid XSD dateTime format: '{s}'");
        var datePart = corePart[..tIdx];
        var timePart = corePart[(tIdx + 1)..];

        // Parse date part
        bool negative = datePart.StartsWith('-');
        var dateParts = (negative ? datePart[1..] : datePart).Split('-');
        if (dateParts.Length != 3)
            throw new FormatException($"Invalid XSD dateTime date component: '{datePart}'");

        var year = long.Parse(dateParts[0], CultureInfo.InvariantCulture);
        if (negative) year = -year;
        var month = int.Parse(dateParts[1], CultureInfo.InvariantCulture);
        var day = int.Parse(dateParts[2], CultureInfo.InvariantCulture);

        // FODT0001: reject years that exceed implementation-defined limits
        if (Math.Abs(year) > 999999)
            throw new FormatException($"FODT0001: Year {year} exceeds the implementation-defined limit");

        // Parse time part (HH:mm:ss[.fff])
        var timeParts = timePart.Split(':');
        if (timeParts.Length < 3)
            throw new FormatException($"Invalid XSD dateTime time component: '{timePart}'");
        var hour = int.Parse(timeParts[0], CultureInfo.InvariantCulture);
        var minute = int.Parse(timeParts[1], CultureInfo.InvariantCulture);
        // Seconds may have fractional part
        var secStr = timeParts[2];
        int second = 0;
        int fractionalTicks = 0;
        var dotIdx = secStr.IndexOf('.', StringComparison.Ordinal);
        if (dotIdx >= 0)
        {
            second = int.Parse(secStr[..dotIdx], CultureInfo.InvariantCulture);
            var fracStr = secStr[(dotIdx + 1)..];
            // Pad or truncate to 7 digits (ticks precision)
            if (fracStr.Length > 7) fracStr = fracStr[..7];
            else fracStr = fracStr.PadRight(7, '0');
            fractionalTicks = int.Parse(fracStr, CultureInfo.InvariantCulture);
        }
        else
        {
            second = int.Parse(secStr, CultureInfo.InvariantCulture);
        }

        // Validate day against proleptic Gregorian calendar for the actual year
        var maxDay = XsDateTime.DaysInMonthProleptic(year, month);
        if (day > maxDay)
            throw new FormatException($"Day {day} is not valid for year {year} month {month}");

        // Clamp year for .NET storage — use matching leap year status to preserve day
        var isLeap = IsLeapYearProleptic(year);
        var clampedYear = isLeap ? 4 : 1;
        var dt = new DateTime(clampedYear, month, day, hour, minute, second).AddTicks(fractionalTicks);
        var dto = new DateTimeOffset(dt, hasTz ? tzOffset : TimeSpan.Zero);

        return new XsDateTime(dto, hasTz) { ExtendedYear = year };
    }

    public int CompareTo(XsDateTime other)
    {
        // If either has extended year, compare by year first
        if (ExtendedYear.HasValue || other.ExtendedYear.HasValue)
        {
            var leftYear = EffectiveYear;
            var rightYear = other.EffectiveYear;
            if (leftYear != rightYear) return leftYear.CompareTo(rightYear);
            // Same year — compare the rest using DateTimeOffset
        }
        return Value.CompareTo(other.Value);
    }

    public override string ToString()
    {
        var dto = Value;
        var sb = new System.Text.StringBuilder(30);
        if (ExtendedYear.HasValue)
        {
            var year = ExtendedYear.Value;
            if (year < 0)
            {
                sb.Append('-');
                sb.Append((-year).ToString("D4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(year.ToString("D4", CultureInfo.InvariantCulture));
            }
            sb.Append('-');
            sb.Append(dto.Month.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append('-');
            sb.Append(dto.Day.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append('T');
            sb.Append(dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(dto.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        }
        AppendFractionalSeconds(sb, FractionalTicks);
        if (HasTimezone)
        {
            if (dto.Offset == TimeSpan.Zero)
                sb.Append('Z');
            else
                sb.Append(dto.ToString("zzz", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    internal static bool HasTimezoneIndicator(string s)
    {
        if (s.EndsWith('Z')) return true;
        // Check for +HH:MM or -HH:MM at end (at least 6 chars: ±HH:MM)
        if (s.Length >= 6)
        {
            var last6 = s[^6..];
            if ((last6[0] == '+' || last6[0] == '-') && last6[3] == ':')
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines if a year is a leap year in the proleptic Gregorian calendar.
    /// XSD uses proleptic Gregorian where year 0 exists (= 1 BC in astronomical year numbering).
    /// </summary>
    internal static bool IsLeapYearProleptic(long year)
    {
        // In XSD, year 0 = 1 BC, -1 = 2 BC, etc.
        // Proleptic Gregorian leap year: divisible by 4, except centuries not divisible by 400
        if (year % 4 != 0) return false;
        if (year % 100 != 0) return true;
        return year % 400 == 0;
    }

    /// <summary>
    /// Returns the number of days in a given month for a proleptic Gregorian year.
    /// </summary>
    internal static int DaysInMonthProleptic(long year, int month) => month switch
    {
        1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
        4 or 6 or 9 or 11 => 30,
        2 => IsLeapYearProleptic(year) ? 29 : 28,
        _ => throw new FormatException($"Invalid month: {month}")
    };

    internal static void AppendFractionalSeconds(System.Text.StringBuilder sb, int fractionalTicks)
    {
        if (fractionalTicks <= 0) return;
        // Format as .NNN... with trailing zeros removed
        var frac = fractionalTicks.ToString("D7", CultureInfo.InvariantCulture); // 7 digits for ticks
        var trimmed = frac.TrimEnd('0');
        sb.Append('.').Append(trimmed);
    }
}

/// <summary>
/// Represents an <c>xs:date</c> value with optional timezone and support for extended years.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DateOnly"/>, this type preserves timezone offset information and
/// distinguishes between "no timezone" (<see cref="Timezone"/> is <c>null</c>) and a
/// specific timezone offset. This distinction matters for XPath date comparisons and
/// timezone-related functions like <c>fn:adjust-date-to-timezone()</c>.
/// </para>
/// <para>
/// <b>Extended years:</b> Supports years outside the .NET 1-9999 range, including year 0
/// and negative years in the proleptic Gregorian calendar. See <see cref="ExtendedYear"/>.
/// </para>
/// </remarks>
public readonly record struct XsDate(DateOnly Date, TimeSpan? Timezone) : IComparable<XsDate>
{
    /// <summary>
    /// Extended year for dates outside the .NET DateOnly range (years &lt; 1 or &gt; 9999).
    /// When non-null, this is the true year; Date.Year is clamped for month/day storage.
    /// </summary>
    public long? ExtendedYear { get; init; }

    public static bool operator <(XsDate left, XsDate right) => left.CompareTo(right) < 0;
    public static bool operator >(XsDate left, XsDate right) => left.CompareTo(right) > 0;
    public static bool operator <=(XsDate left, XsDate right) => left.CompareTo(right) <= 0;
    public static bool operator >=(XsDate left, XsDate right) => left.CompareTo(right) >= 0;

    /// <summary>The effective year, using ExtendedYear if available, otherwise Date.Year.</summary>
    public long EffectiveYear => ExtendedYear ?? Date.Year;

    public static XsDate Parse(string s)
    {
        s = s.Trim();
        TimeSpan? tz = null;
        var dateStr = s;

        if (s.EndsWith('Z'))
        {
            tz = TimeSpan.Zero;
            dateStr = s[..^1];
        }
        else if (s.Length >= 6)
        {
            var signIdx = s.Length - 6;
            if (signIdx > 0 && (s[signIdx] == '+' || s[signIdx] == '-') && s[signIdx + 3] == ':')
            {
                var tzStr = s[signIdx..];
                var hours = int.Parse(tzStr[1..3], CultureInfo.InvariantCulture);
                var mins = int.Parse(tzStr[4..6], CultureInfo.InvariantCulture);
                tz = new TimeSpan(hours, mins, 0);
                if (tzStr[0] == '-') tz = -tz;
                dateStr = s[..signIdx];
            }
        }

        // Check if this is an extended year (negative, year 0, or > 9999)
        bool isExtended = dateStr.StartsWith('-') || dateStr.StartsWith("0000", StringComparison.Ordinal);
        if (!isExtended)
        {
            // Check if year part has > 4 digits (year > 9999)
            var firstDash = dateStr.IndexOf('-', StringComparison.Ordinal);
            if (firstDash > 4) isExtended = true;
        }

        if (!isExtended)
        {
            // Standard year range — use .NET parsing (will throw on invalid dates like Feb 29 in non-leap years)
            return new XsDate(DateOnly.Parse(dateStr, CultureInfo.InvariantCulture), tz);
        }

        // Extended year parsing for years outside 1-9999 or year 0
        return ParseExtendedDate(dateStr, tz);
    }

    private static XsDate ParseExtendedDate(string dateStr, TimeSpan? tz)
    {
        // XSD date format: [-]YYYY-MM-DD
        bool negative = dateStr.StartsWith('-');
        var parts = (negative ? dateStr[1..] : dateStr).Split('-');
        if (parts.Length != 3)
            throw new FormatException($"Invalid XSD date format: '{dateStr}'");

        var year = long.Parse(parts[0], CultureInfo.InvariantCulture);
        if (negative) year = -year;
        var month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var day = int.Parse(parts[2], CultureInfo.InvariantCulture);

        // FODT0001: reject years that exceed implementation-defined limits
        if (Math.Abs(year) > 999999)
            throw new FormatException($"FODT0001: Year {year} exceeds the implementation-defined limit");

        if (month < 1 || month > 12)
            throw new FormatException($"Invalid month {month} in date '{dateStr}'");
        if (day < 1 || day > 31)
            throw new FormatException($"Invalid day {day} in date '{dateStr}'");

        // XSD year 0000 is valid (proleptic Gregorian year 0 = 1 BC)
        // Validate day against the proleptic Gregorian calendar for the actual year
        var maxDay = XsDateTime.DaysInMonthProleptic(year, month);
        if (day > maxDay)
            throw new FormatException($"Day {day} is not valid for year {year} month {month}");

        // For month/day storage, clamp year to .NET range.
        // Use a clamped year with matching leap year status to preserve the actual day.
        var isLeap = XsDateTime.IsLeapYearProleptic(year);
        var clampedYear = isLeap ? 4 : 1; // year 4 is a leap year, year 1 is not
        var clampedDate = new DateOnly(clampedYear, month, day);

        return new XsDate(clampedDate, tz) { ExtendedYear = year };
    }

    public int CompareTo(XsDate other)
    {
        // Extended years: compare by year first, then month/day
        var leftYear = EffectiveYear;
        var rightYear = other.EffectiveYear;
        if (leftYear != rightYear) return leftYear.CompareTo(rightYear);

        // Same effective year — compare month and day
        var monthCmp = Date.Month.CompareTo(other.Date.Month);
        if (monthCmp != 0) return monthCmp;
        var dayCmp = Date.Day.CompareTo(other.Date.Day);
        if (dayCmp != 0) return dayCmp;

        // Same date — compare timezones (normalize to UTC)
        var leftTz = Timezone ?? TimeSpan.Zero;
        var rightTz = other.Timezone ?? TimeSpan.Zero;
        // Earlier UTC = later local time offset, so subtract offset
        return (-leftTz).CompareTo(-rightTz);
    }

    /// <summary>
    /// Converts this date to UTC ticks for comparison, using midnight as the time component.
    /// Falls back to year comparison for extended dates.
    /// </summary>
    internal long ToUtcTicks()
    {
        if (ExtendedYear.HasValue)
        {
            // Approximate: use year * days_per_year for ordering
            // This is sufficient for comparison but not exact arithmetic
            var implicitTz = Timezone ?? DateTimeOffset.Now.Offset;
            return EffectiveYear * 365L * TimeSpan.TicksPerDay
                + Date.Month * 30L * TimeSpan.TicksPerDay
                + Date.Day * TimeSpan.TicksPerDay
                - implicitTz.Ticks;
        }
        var dt = Date.ToDateTime(TimeOnly.MinValue);
        var offset = Timezone ?? DateTimeOffset.Now.Offset;
        return new DateTimeOffset(dt, offset).UtcTicks;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder(20);
        if (ExtendedYear.HasValue)
        {
            var year = ExtendedYear.Value;
            if (year < 0)
            {
                sb.Append('-');
                sb.Append((-year).ToString("D4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(year.ToString("D4", CultureInfo.InvariantCulture));
            }
            sb.Append('-');
            sb.Append(Date.Month.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append('-');
            sb.Append(Date.Day.ToString("D2", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        AppendTimezone(sb, Timezone);
        return sb.ToString();
    }

    public static void AppendTimezone(System.Text.StringBuilder sb, TimeSpan? tz)
    {
        if (tz is null) return;
        if (tz == TimeSpan.Zero)
        {
            sb.Append('Z');
            return;
        }
        var offset = tz.Value;
        sb.Append(offset < TimeSpan.Zero ? '-' : '+');
        var abs = offset < TimeSpan.Zero ? -offset : offset;
        sb.Append(abs.Hours.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(abs.Minutes.ToString("D2", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Represents an <c>xs:time</c> value with optional timezone and sub-second precision.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TimeOnly"/>, this type preserves timezone offset information and
/// fractional seconds beyond tick precision. The <see cref="Timezone"/> property distinguishes
/// "no timezone" (<c>null</c>) from UTC (<see cref="TimeSpan.Zero"/>), which is significant
/// for XPath time comparisons and timezone adjustment functions.
/// </para>
/// <para>
/// <b>Comparison:</b> Times are compared by normalizing to UTC. When no timezone is present,
/// the system's implicit timezone is used, per the XPath Functions and Operators specification.
/// </para>
/// <para>
/// <b>24:00:00:</b> The XML Schema value <c>24:00:00</c> (end of day) is normalized to
/// <c>00:00:00</c> during parsing, since <see cref="TimeOnly"/> does not support hour 24.
/// </para>
/// </remarks>
public readonly record struct XsTime(TimeOnly Time, TimeSpan? Timezone, int FractionalTicks) : IComparable<XsTime>
{
    public static bool operator <(XsTime left, XsTime right) => left.CompareTo(right) < 0;
    public static bool operator >(XsTime left, XsTime right) => left.CompareTo(right) > 0;
    public static bool operator <=(XsTime left, XsTime right) => left.CompareTo(right) <= 0;
    public static bool operator >=(XsTime left, XsTime right) => left.CompareTo(right) >= 0;
    public static XsTime Parse(string s)
    {
        s = s.Trim();
        TimeSpan? tz = null;
        var timeStr = s;

        if (s.EndsWith('Z'))
        {
            tz = TimeSpan.Zero;
            timeStr = s[..^1];
        }
        else if (s.Length >= 6)
        {
            var signIdx = s.Length - 6;
            if (signIdx > 0 && (s[signIdx] == '+' || s[signIdx] == '-') && s[signIdx + 3] == ':')
            {
                var tzStr = s[signIdx..];
                var hours = int.Parse(tzStr[1..3], CultureInfo.InvariantCulture);
                var mins = int.Parse(tzStr[4..6], CultureInfo.InvariantCulture);
                tz = new TimeSpan(hours, mins, 0);
                if (tzStr[0] == '-') tz = -tz;
                timeStr = s[..signIdx];
            }
        }

        // XSD: 24:00:00 is a valid time representing midnight (end of day)
        // Normalize to 00:00:00 since TimeOnly doesn't support 24:00:00
        if (timeStr.StartsWith("24:00:00", StringComparison.Ordinal))
            timeStr = "00:00:00" + timeStr[8..]; // preserve any fractional seconds

        var time = TimeOnly.Parse(timeStr, CultureInfo.InvariantCulture);
        var fractionalTicks = (int)(time.Ticks % TimeSpan.TicksPerSecond);
        return new XsTime(time, tz, fractionalTicks);
    }

    public int CompareTo(XsTime other)
    {
        // XPath F&O: times with timezones compare by normalizing to UTC
        // Times without timezones use implicit timezone (UTC) for comparison
        var leftUtc = ToUtcTicks();
        var rightUtc = other.ToUtcTicks();
        return leftUtc.CompareTo(rightUtc);
    }

    /// <summary>
    /// Converts this time to UTC ticks for comparison.
    /// Per XPath F&amp;O §10.4, uses the system implicit timezone when timezone is absent.
    /// </summary>
    internal long ToUtcTicks()
    {
        var offset = Timezone ?? DateTimeOffset.Now.Offset; // implicit timezone = system timezone per spec
        var ticks = Time.Ticks - offset.Ticks;
        return ticks;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder(20);
        sb.Append(Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        XsDateTime.AppendFractionalSeconds(sb, FractionalTicks);
        XsDate.AppendTimezone(sb, Timezone);
        return sb.ToString();
    }
}

/// <summary>
/// Represents an <c>xs:gYearMonth</c> value (e.g., <c>2005-10</c>), stored as its lexical form.
/// </summary>
/// <remarks>
/// Part of the XML Schema Gregorian date family. These partial-date types are used in
/// XPath/XQuery for recurring date patterns (e.g., "every October").
/// </remarks>
/// <param name="Value">The lexical representation (e.g., <c>"2005-10"</c> or <c>"2005-10Z"</c>).</param>
public readonly record struct XsGYearMonth(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Represents an <c>xs:gYear</c> value (e.g., <c>2005</c>), stored as its lexical form.
/// </summary>
/// <param name="Value">The lexical representation (e.g., <c>"2005"</c> or <c>"-0044"</c>).</param>
public readonly record struct XsGYear(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Represents an <c>xs:gMonthDay</c> value (e.g., <c>--12-25</c>), stored as its lexical form.
/// </summary>
/// <param name="Value">The lexical representation (e.g., <c>"--12-25"</c>).</param>
public readonly record struct XsGMonthDay(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Represents an <c>xs:gDay</c> value (e.g., <c>---25</c>), stored as its lexical form.
/// </summary>
/// <param name="Value">The lexical representation (e.g., <c>"---25"</c>).</param>
public readonly record struct XsGDay(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Represents an <c>xs:gMonth</c> value (e.g., <c>--12</c>), stored as its lexical form.
/// </summary>
/// <param name="Value">The lexical representation (e.g., <c>"--12"</c>).</param>
public readonly record struct XsGMonth(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Represents an XDM atomic value — a typed, immutable value from the XPath/XQuery type system.
/// </summary>
/// <remarks>
/// <para>
/// <c>XdmValue</c> pairs a CLR value (boxed in <see cref="RawValue"/>) with an
/// <see cref="XdmType"/> tag that identifies its XSD type. This enables type-aware
/// operations such as comparisons, arithmetic, and casting as defined by the XPath/XQuery
/// Functions and Operators specification.
/// </para>
/// <para>
/// <b>Construction:</b> Use the static factory methods (<see cref="UntypedAtomic"/>,
/// <see cref="XsString"/>, <see cref="XsInteger"/>, <see cref="Boolean"/>, etc.) to create
/// values. These ensure the correct <see cref="XdmType"/> tag is applied.
/// </para>
/// <para>
/// <b>Access:</b> Use the typed accessor methods (<see cref="AsString"/>, <see cref="AsLong"/>,
/// <see cref="AsDouble"/>, <see cref="AsBoolean"/>, etc.) to extract the value. These perform
/// type coercion following XPath casting rules and throw <see cref="InvalidCastException"/>
/// for incompatible types.
/// </para>
/// <para>
/// <b>Equality:</b> Two values are equal if and only if they have the same <see cref="Type"/>
/// and the same underlying value.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var price = XdmValue.XsDecimal(19.99m);
/// var name = XdmValue.XsString("Widget");
/// var active = XdmValue.Boolean(true);
///
/// Console.WriteLine(price.AsDecimal()); // 19.99
/// Console.WriteLine(name.AsString());   // Widget
/// Console.WriteLine(active.AsBoolean()); // True
/// </code>
/// </example>
public readonly struct XdmValue : IEquatable<XdmValue>
{
    private readonly object? _value;
    private readonly XdmType _type;

    public XdmType Type => _type;
    public bool IsEmpty => _value is null;

    private XdmValue(XdmType type, object? value)
    {
        _type = type;
        _value = value;
    }

    // Factory methods for common types
    public static XdmValue Empty => new(XdmType.UntypedAtomic, null);

    public static XdmValue UntypedAtomic(string value) =>
        new(XdmType.UntypedAtomic, value);

    public static XdmValue XsString(string value) =>
        new(XdmType.XsString, value);

    public static XdmValue Boolean(bool value) =>
        new(XdmType.Boolean, value);

    public static XdmValue XsInteger(long value) =>
        new(XdmType.XsInteger, value);

    public static XdmValue XsDecimal(decimal value) =>
        new(XdmType.XsDecimal, value);

    public static XdmValue XsDouble(double value) =>
        new(XdmType.XsDouble, value);

    public static XdmValue XsFloat(float value) =>
        new(XdmType.XsFloat, value);

    public static XdmValue DateTime(DateTimeOffset value) =>
        new(XdmType.DateTime, value);

    public static XdmValue Date(DateOnly value) =>
        new(XdmType.Date, value);

    public static XdmValue Time(TimeOnly value) =>
        new(XdmType.Time, value);

    public static XdmValue Duration(TimeSpan value) =>
        new(XdmType.Duration, value);

    public static XdmValue QName(XdmQName value) =>
        new(XdmType.QName, value);

    public static XdmValue AnyUri(Uri value) =>
        new(XdmType.AnyUri, value);

    public static XdmValue AnyUri(string value) =>
        new(XdmType.AnyUri, new Uri(value, UriKind.RelativeOrAbsolute));

    public static XdmValue Base64Binary(byte[] value) =>
        new(XdmType.Base64Binary, value);

    public static XdmValue HexBinary(byte[] value) =>
        new(XdmType.HexBinary, value);

    // Accessors
    public string AsString() => _value switch
    {
        string s => s,
        null => string.Empty,
        bool b => b ? "true" : "false",
        byte[] bytes when _type == XdmType.Base64Binary => Convert.ToBase64String(bytes),
        byte[] bytes when _type == XdmType.HexBinary => Convert.ToHexString(bytes),
        DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => _value.ToString() ?? string.Empty
    };

    public bool AsBoolean() => _value switch
    {
        bool b => b,
        string s => !string.IsNullOrEmpty(s) && s != "false" && s != "0",
        long l => l != 0,
        int i => i != 0,
        double d => d != 0 && !double.IsNaN(d),
        decimal dec => dec != 0,
        float f => f != 0 && !float.IsNaN(f),
        null => false,
        _ => throw new InvalidCastException($"Cannot convert {_type} to boolean")
    };

    public long AsLong() => _value switch
    {
        long l => l,
        int i => i,
        short s => s,
        byte b => b,
        decimal d => (long)d,
        double d => (long)d,
        float f => (long)f,
        string s => long.Parse(s, CultureInfo.InvariantCulture),
        null => 0,
        _ => throw new InvalidCastException($"Cannot convert {_type} to long")
    };

    public int AsInt() => (int)AsLong();

    public double AsDouble() => _value switch
    {
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        decimal dec => (double)dec,
        string s => double.Parse(s, CultureInfo.InvariantCulture),
        null => 0.0,
        _ => throw new InvalidCastException($"Cannot convert {_type} to double")
    };

    public float AsFloat() => (float)AsDouble();

    public decimal AsDecimal() => _value switch
    {
        decimal d => d,
        long l => l,
        int i => i,
        double d => (decimal)d,
        float f => (decimal)f,
        string s => decimal.Parse(s, CultureInfo.InvariantCulture),
        null => 0m,
        _ => throw new InvalidCastException($"Cannot convert {_type} to decimal")
    };

    public DateTimeOffset AsDateTime() => _value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(dt),
        DateOnly d => new DateTimeOffset(d, TimeOnly.MinValue, TimeSpan.Zero),
        string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture),
        null => DateTimeOffset.MinValue,
        _ => throw new InvalidCastException($"Cannot convert {_type} to dateTime")
    };

    public DateOnly AsDate() => _value switch
    {
        DateOnly d => d,
        DateTimeOffset dto => DateOnly.FromDateTime(dto.Date),
        DateTime dt => DateOnly.FromDateTime(dt),
        string s => DateOnly.Parse(s, CultureInfo.InvariantCulture),
        null => DateOnly.MinValue,
        _ => throw new InvalidCastException($"Cannot convert {_type} to date")
    };

    public TimeOnly AsTime() => _value switch
    {
        TimeOnly t => t,
        DateTimeOffset dto => TimeOnly.FromDateTime(dto.DateTime),
        DateTime dt => TimeOnly.FromDateTime(dt),
        string s => TimeOnly.Parse(s, CultureInfo.InvariantCulture),
        null => TimeOnly.MinValue,
        _ => throw new InvalidCastException($"Cannot convert {_type} to time")
    };

    public TimeSpan AsDuration() => _value switch
    {
        TimeSpan ts => ts,
        string s => System.Xml.XmlConvert.ToTimeSpan(s),
        null => TimeSpan.Zero,
        _ => throw new InvalidCastException($"Cannot convert {_type} to duration")
    };

    public XdmQName AsQName() => _value switch
    {
        XdmQName qn => qn,
        null => throw new InvalidCastException("Cannot convert null to QName"),
        _ => throw new InvalidCastException($"Cannot convert {_type} to QName")
    };

    public Uri AsUri() => _value switch
    {
        Uri u => u,
        string s => new Uri(s, UriKind.RelativeOrAbsolute),
        null => throw new InvalidCastException("Cannot convert null to URI"),
        _ => throw new InvalidCastException($"Cannot convert {_type} to anyURI")
    };

    public byte[] AsBinary() => _value switch
    {
        byte[] b => b,
        string s when _type == XdmType.Base64Binary => Convert.FromBase64String(s),
        string s when _type == XdmType.HexBinary => Convert.FromHexString(s),
        null => [],
        _ => throw new InvalidCastException($"Cannot convert {_type} to binary")
    };

    /// <summary>
    /// Gets the raw value object.
    /// </summary>
    public object? RawValue => _value;

    // Equality
    public bool Equals(XdmValue other) =>
        _type == other._type && Equals(_value, other._value);

    public override bool Equals(object? obj) =>
        obj is XdmValue other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(_type, _value);

    public static bool operator ==(XdmValue left, XdmValue right) => left.Equals(right);
    public static bool operator !=(XdmValue left, XdmValue right) => !left.Equals(right);

    public override string ToString() => AsString();
}
