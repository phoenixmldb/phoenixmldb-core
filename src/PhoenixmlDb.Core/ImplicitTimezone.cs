namespace PhoenixmlDb.Xdm;

/// <summary>
/// The implicit timezone of the dynamic context (XPath 3.1 §2.1.2): the timezone a date, time
/// or dateTime with none of its own is taken to be in when it is compared with one that has
/// one. It is the timezone of the machine unless the host says otherwise.
/// </summary>
/// <remarks>
/// <para>
/// The date and time types read it here, so that every comparison in one evaluation uses the
/// same one. Left alone, two servers in different zones give different answers to
/// <c>xs:dateTime('2020-01-01T12:00:00') lt xs:dateTime('2020-01-01T12:00:00+01:00')</c>.
/// </para>
/// <para>
/// A host sets it for the code that runs inside <see cref="Use"/>. The value follows the
/// asynchronous flow of that code and no other, so evaluations that run side by side can each
/// have their own.
/// </para>
/// </remarks>
public static class ImplicitTimezone
{
    private static readonly AsyncLocal<TimeSpan?> Set = new();

    /// <summary>The implicit timezone in effect: what a host set, or the machine's offset now.</summary>
    public static TimeSpan Current => Set.Value ?? DateTimeOffset.Now.Offset;

    /// <summary>What a host set for the running code, or null when it set nothing.</summary>
    public static TimeSpan? Configured => Set.Value;

    /// <summary>
    /// Sets the implicit timezone for the code that runs until the result is disposed, which
    /// puts back what was there. Null means the machine's timezone.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The offset is not a whole number of minutes between -14:00 and +14:00, the range of an
    /// XML Schema timezone.
    /// </exception>
    public static ImplicitTimezoneScope Use(TimeSpan? offset)
    {
        if (offset is { } value
            && (value < TimeSpan.FromHours(-14) || value > TimeSpan.FromHours(14) || value.Ticks % TimeSpan.TicksPerMinute != 0))
            throw new ArgumentOutOfRangeException(nameof(offset), offset,
                "A timezone is a whole number of minutes between -14:00 and +14:00.");
        var previous = Set.Value;
        Set.Value = offset;
        return new ImplicitTimezoneScope(previous);
    }

    internal static void Restore(TimeSpan? previous) => Set.Value = previous;
}

/// <summary>Puts back the implicit timezone that was in effect before <see cref="ImplicitTimezone.Use"/>.</summary>
public readonly struct ImplicitTimezoneScope : IDisposable, IEquatable<ImplicitTimezoneScope>
{
    private readonly TimeSpan? _previous;

    internal ImplicitTimezoneScope(TimeSpan? previous) => _previous = previous;

    /// <inheritdoc />
    public void Dispose() => ImplicitTimezone.Restore(_previous);

    /// <inheritdoc />
    public bool Equals(ImplicitTimezoneScope other) => _previous == other._previous;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ImplicitTimezoneScope other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _previous.GetHashCode();

    /// <summary>Whether two scopes put back the same timezone.</summary>
    public static bool operator ==(ImplicitTimezoneScope left, ImplicitTimezoneScope right) => left.Equals(right);

    /// <summary>Whether two scopes put back different timezones.</summary>
    public static bool operator !=(ImplicitTimezoneScope left, ImplicitTimezoneScope right) => !left.Equals(right);
}
