using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// Rewrites a pattern facet so that everything in it that stands for one character matches one
/// character as XML Schema counts it: a code point. System.Xml hands the pattern to a .NET
/// expression, which counts UTF-16 units, so a character outside the Basic Multilingual Plane
/// (a surrogate pair) was two characters to ".", to a negated class and to a negated category,
/// and no character at all to a category it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Each atom that stands for one character becomes an alternation: the atom as written, kept
/// from matching half a pair, or one of the pairs that are the characters above U+FFFF the atom
/// stands for. Which those are is worked out from the atom: a category by the Unicode data of
/// the runtime, a class from its members, a negation as the complement, a subtraction as the
/// difference. An atom that stands for no such character and cannot match half a pair is left
/// exactly as written, so a pattern with nothing of the kind is not changed at all.
/// </para>
/// <para>
/// A block escape (<c>\p{IsGreek}</c>) is taken to hold no character above U+FFFF: .NET knows
/// no block there. <c>\i</c> and <c>\c</c> hold none either, as in System.Xml.
/// </para>
/// </remarks>
internal static class SchemaPatternCharacters
{
    private const int FirstAbove = 0x10000;
    private const int LastAbove = 0x10FFFF;
    private const string NotHalfAPair = @"(?![\uD800-\uDFFF])";
    private const string AnyPair = @"[\uD800-\uDBFF][\uDC00-\uDFFF]";

    /// <summary>The pattern with every atom that stands for one character made to match one code point.</summary>
    public static string Rewrite(string pattern)
    {
        var result = new StringBuilder(pattern.Length + 32);
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '.')
            {
                result.Append(@"(?:[^\n\r\uD800-\uDFFF]|").Append(AnyPair).Append(')');
                i++;
            }
            else if (c == '\\' && i + 1 < pattern.Length)
            {
                var end = EscapeEnd(pattern, i);
                var text = pattern[i..end];
                var above = EscapeAbove(text, out var matchesHalfAPair);
                AppendAtom(result, text, matchesHalfAPair, above);
                i = end;
            }
            else if (c == '[')
            {
                var cls = ParseClass(pattern, ref i);
                AppendAtom(result, cls.Bmp, cls.MatchesHalfAPair, cls.Above);
            }
            else if (char.IsHighSurrogate(c) && i + 1 < pattern.Length && char.IsLowSurrogate(pattern[i + 1]))
            {
                // One character written as itself: grouped, so that a quantifier after it
                // applies to the character and not to its second half.
                result.Append("(?:").Append(c).Append(pattern[i + 1]).Append(')');
                i += 2;
            }
            else
            {
                result.Append(c);
                i++;
            }
        }
        return result.ToString();
    }

    private static void AppendAtom(StringBuilder result, string? bmp, bool matchesHalfAPair, RangeSet above)
    {
        if (above.IsEmpty && !matchesHalfAPair)
        {
            // Nothing above U+FFFF, and no way to match half a pair: as written.
            result.Append(bmp ?? @"[^\u0000-￿]");
            return;
        }
        result.Append("(?:");
        if (bmp is not null)
            result.Append(NotHalfAPair).Append(bmp);
        if (!above.IsEmpty)
        {
            if (bmp is not null)
                result.Append('|');
            result.Append(above.AsPairs());
        }
        else if (bmp is null)
        {
            result.Append(@"[^\u0000-￿]");
        }
        result.Append(')');
    }

    // ---- escapes ------------------------------------------------------------------------

    private static int EscapeEnd(string pattern, int start)
    {
        var kind = pattern[start + 1];
        if (kind is 'p' or 'P' && start + 2 < pattern.Length && pattern[start + 2] == '{')
        {
            var close = pattern.IndexOf('}', start + 3);
            if (close > 0)
                return close + 1;
        }
        return start + 2;
    }

    /// <summary>
    /// The characters above U+FFFF that an escape stands for, and whether the escape as written
    /// matches half a pair (a negated set does: half a pair is not a letter, not a space).
    /// </summary>
    private static RangeSet EscapeAbove(string escape, out bool matchesHalfAPair)
    {
        matchesHalfAPair = false;
        var kind = escape[1];
        switch (kind)
        {
            case 'S' or 'I' or 'C':
                matchesHalfAPair = true;
                return RangeSet.All;
            case 'd':
                return Category("Nd");
            case 'D':
                matchesHalfAPair = true;
                return Category("Nd").Complement();
            case 'w':
                return NotAWordCharacter.Complement();
            case 'W':
                matchesHalfAPair = true;
                return NotAWordCharacter;
            case 'p' or 'P' when escape.Length > 4:
            {
                var name = escape[3..^1];
                // A block: none above U+FFFF that .NET knows. A category: by the Unicode data.
                var set = name.StartsWith("Is", StringComparison.Ordinal) ? RangeSet.Empty : Category(name);
                if (kind == 'p')
                    return set;
                matchesHalfAPair = true;
                return set.Complement();
            }
            default:
                // A single-character escape (\n, \., \-), \s, \i, \c: nothing above U+FFFF.
                return RangeSet.Empty;
        }
    }

    private static RangeSet NotAWordCharacter => Category("P").Union(Category("Z")).Union(Category("C"));

    // ---- character classes --------------------------------------------------------------

    private readonly record struct ClassResult(string? Bmp, RangeSet Above, bool MatchesHalfAPair);

    /// <summary>
    /// Reads a class expression from <paramref name="i"/>, which is on its "[", and leaves
    /// <paramref name="i"/> after its "]".
    /// </summary>
    private static ClassResult ParseClass(string pattern, ref int i)
    {
        var start = i;
        i++; // [
        var negated = i < pattern.Length && pattern[i] == '^';
        if (negated)
            i++;

        var bmp = new StringBuilder();
        var above = RangeSet.Empty;
        var membersMatchHalfAPair = false;
        var rebuilt = false;
        ClassResult? subtracted = null;
        var first = true;

        while (i < pattern.Length && pattern[i] != ']')
        {
            // A subtraction: -[ ... ] closes the class.
            if (pattern[i] == '-' && i + 1 < pattern.Length && pattern[i + 1] == '[')
            {
                i++;
                subtracted = ParseClass(pattern, ref i);
                if (subtracted.Value.Bmp is null || subtracted.Value.Above.IsEmpty == false || subtracted.Value.MatchesHalfAPair)
                    rebuilt = true;
                break;
            }

            // A category or multi-character escape is a member on its own.
            if (pattern[i] == '\\' && i + 1 < pattern.Length && IsSetEscape(pattern[i + 1]))
            {
                var end = EscapeEnd(pattern, i);
                var text = pattern[i..end];
                above = above.Union(EscapeAbove(text, out var half));
                membersMatchHalfAPair |= half;
                bmp.Append(text);
                i = end;
                first = false;
                continue;
            }

            var low = ReadCharacter(pattern, ref i, out var lowText);
            var high = low;
            var highText = lowText;
            var isRange = false;
            // a-b, unless the "-" is the last character of the class or starts a subtraction.
            if (i + 1 < pattern.Length && pattern[i] == '-' && pattern[i + 1] != ']' && pattern[i + 1] != '['
                && !(first && low == '-'))
            {
                i++;
                high = ReadCharacter(pattern, ref i, out highText);
                isRange = true;
            }
            first = false;

            if (high >= FirstAbove)
            {
                rebuilt = true;
                above = above.Union(RangeSet.Of(Math.Max(low, FirstAbove), high));
                if (low < FirstAbove)
                    bmp.Append(lowText).Append(@"-￿");
            }
            else
            {
                bmp.Append(lowText);
                if (isRange)
                    bmp.Append('-').Append(highText);
            }
        }
        if (i < pattern.Length)
            i++; // ]

        if (negated)
            above = above.Complement();
        if (subtracted is { } minus)
            above = above.Except(minus.Above);

        // As written, when nothing in it is above U+FFFF: the text is the class System.Xml
        // has always been given.
        if (!rebuilt)
            return new ClassResult(pattern[start..i], above, negated || membersMatchHalfAPair);

        string? bmpClass;
        if (bmp.Length == 0 && !negated)
        {
            bmpClass = null; // every member is above U+FFFF
        }
        else
        {
            var text = new StringBuilder("[");
            if (bmp.Length == 0)
                text.Append(@"\u0000-￿");
            else
                text.Append(negated ? "^" : "").Append(bmp);
            if (subtracted is { Bmp: { } minusBmp })
                text.Append('-').Append(minusBmp);
            bmpClass = text.Append(']').ToString();
        }
        return new ClassResult(bmpClass, above, negated || membersMatchHalfAPair || subtracted is not null);
    }

    private static bool IsSetEscape(char kind) => kind is 'p' or 'P' or 's' or 'S' or 'i' or 'I' or 'c' or 'C' or 'd' or 'D' or 'w' or 'W';

    /// <summary>One character of a class: itself, a single-character escape, or a pair.</summary>
    private static int ReadCharacter(string pattern, ref int i, out string text)
    {
        var c = pattern[i];
        if (c == '\\' && i + 1 < pattern.Length)
        {
            text = pattern.Substring(i, 2);
            var escaped = pattern[i + 1];
            i += 2;
            return escaped switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => escaped };
        }
        if (char.IsHighSurrogate(c) && i + 1 < pattern.Length && char.IsLowSurrogate(pattern[i + 1]))
        {
            text = pattern.Substring(i, 2);
            var codePoint = char.ConvertToUtf32(c, pattern[i + 1]);
            i += 2;
            return codePoint;
        }
        text = c.ToString();
        i++;
        return c;
    }

    // ---- the characters above U+FFFF, by category ---------------------------------------

    private static readonly Lazy<Dictionary<string, RangeSet>> Categories = new(BuildCategories);

    private static RangeSet Category(string name)
        => Categories.Value.TryGetValue(name, out var set) ? set : RangeSet.Empty;

    private static Dictionary<string, RangeSet> BuildCategories()
    {
        var builders = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
        string? current = null;
        var runStart = FirstAbove;
        for (var codePoint = FirstAbove; codePoint <= LastAbove + 1; codePoint++)
        {
            var name = codePoint <= LastAbove ? Name(CharUnicodeInfo.GetUnicodeCategory(codePoint)) : null;
            if (name == current)
                continue;
            if (current is not null)
            {
                if (!builders.TryGetValue(current, out var ranges))
                    builders[current] = ranges = [];
                ranges.Add((runStart, codePoint - 1));
            }
            current = name;
            runStart = codePoint;
        }

        var result = new Dictionary<string, RangeSet>(StringComparer.Ordinal);
        foreach (var (name, ranges) in builders)
            result[name] = new RangeSet(ranges);
        // L, M, N, P, Z, S, C: the categories whose name starts with the letter.
        foreach (var group in builders.Keys.GroupBy(k => k[..1], StringComparer.Ordinal).ToList())
            result[group.Key] = group.Aggregate(RangeSet.Empty, (set, name) => set.Union(result[name]));
        return result;
    }

    private static string Name(UnicodeCategory category) => category switch
    {
        UnicodeCategory.UppercaseLetter => "Lu",
        UnicodeCategory.LowercaseLetter => "Ll",
        UnicodeCategory.TitlecaseLetter => "Lt",
        UnicodeCategory.ModifierLetter => "Lm",
        UnicodeCategory.OtherLetter => "Lo",
        UnicodeCategory.NonSpacingMark => "Mn",
        UnicodeCategory.SpacingCombiningMark => "Mc",
        UnicodeCategory.EnclosingMark => "Me",
        UnicodeCategory.DecimalDigitNumber => "Nd",
        UnicodeCategory.LetterNumber => "Nl",
        UnicodeCategory.OtherNumber => "No",
        UnicodeCategory.SpaceSeparator => "Zs",
        UnicodeCategory.LineSeparator => "Zl",
        UnicodeCategory.ParagraphSeparator => "Zp",
        UnicodeCategory.Control => "Cc",
        UnicodeCategory.Format => "Cf",
        UnicodeCategory.Surrogate => "Cs",
        UnicodeCategory.PrivateUse => "Co",
        UnicodeCategory.ConnectorPunctuation => "Pc",
        UnicodeCategory.DashPunctuation => "Pd",
        UnicodeCategory.OpenPunctuation => "Ps",
        UnicodeCategory.ClosePunctuation => "Pe",
        UnicodeCategory.InitialQuotePunctuation => "Pi",
        UnicodeCategory.FinalQuotePunctuation => "Pf",
        UnicodeCategory.OtherPunctuation => "Po",
        UnicodeCategory.MathSymbol => "Sm",
        UnicodeCategory.CurrencySymbol => "Sc",
        UnicodeCategory.ModifierSymbol => "Sk",
        UnicodeCategory.OtherSymbol => "So",
        _ => "Cn",
    };

    /// <summary>A set of characters above U+FFFF, as sorted ranges that do not touch.</summary>
    private sealed class RangeSet
    {
        public static readonly RangeSet Empty = new([]);
        public static readonly RangeSet All = new([(FirstAbove, LastAbove)]);

        private readonly List<(int Low, int High)> _ranges;
        private string? _pairs;

        public RangeSet(List<(int Low, int High)> ranges) => _ranges = ranges;

        public static RangeSet Of(int low, int high) => new([(low, high)]);

        public bool IsEmpty => _ranges.Count == 0;

        public RangeSet Union(RangeSet other)
        {
            if (other.IsEmpty) return this;
            if (IsEmpty) return other;
            var all = _ranges.Concat(other._ranges).OrderBy(r => r.Low).ToList();
            var merged = new List<(int Low, int High)> { all[0] };
            foreach (var range in all.Skip(1))
            {
                var last = merged[^1];
                if (range.Low <= last.High + 1)
                    merged[^1] = (last.Low, Math.Max(last.High, range.High));
                else
                    merged.Add(range);
            }
            return new RangeSet(merged);
        }

        public RangeSet Complement()
        {
            var result = new List<(int Low, int High)>();
            var next = FirstAbove;
            foreach (var (low, high) in _ranges)
            {
                if (low > next)
                    result.Add((next, low - 1));
                next = high + 1;
            }
            if (next <= LastAbove)
                result.Add((next, LastAbove));
            return new RangeSet(result);
        }

        public RangeSet Except(RangeSet other)
            => other.IsEmpty || IsEmpty ? this : Complement().Union(other).Complement();

        /// <summary>The set as an alternation of surrogate pairs.</summary>
        public string AsPairs() => _pairs ??= BuildPairs();

        private string BuildPairs()
        {
            if (_ranges.Count == 1 && _ranges[0] == (FirstAbove, LastAbove))
                return AnyPair;

            // By first half: the second halves that go with it, as the text of a class. Then
            // the first halves that follow one another with the same second halves are one
            // range, so a whole plane is one alternative and not a thousand.
            var byHigh = new SortedDictionary<char, StringBuilder>();
            void Add(char high, char lowFrom, char lowTo)
            {
                if (!byHigh.TryGetValue(high, out var lows))
                    byHigh[high] = lows = new StringBuilder();
                AppendRange(lows, lowFrom, lowTo);
            }
            foreach (var (low, high) in _ranges)
            {
                var from = char.ConvertFromUtf32(low);
                var to = char.ConvertFromUtf32(high);
                for (var unit = from[0]; unit <= to[0]; unit++)
                    Add(unit, unit == from[0] ? from[1] : '\uDC00', unit == to[0] ? to[1] : '\uDFFF');
            }

            var text = new StringBuilder();
            char runStart = default, runEnd = default;
            string? runLows = null;
            void Flush()
            {
                if (runLows is null)
                    return;
                if (text.Length > 0)
                    text.Append('|');
                if (runStart == runEnd)
                    AppendUnit(text, runStart);
                else
                    AppendRange(text.Append('['), runStart, runEnd).Append(']');
                text.Append('[').Append(runLows).Append(']');
            }
            foreach (var (high, lows) in byHigh)
            {
                var lowText = lows.ToString();
                if (runLows == lowText && high == runEnd + 1)
                {
                    runEnd = high;
                    continue;
                }
                Flush();
                runStart = runEnd = high;
                runLows = lowText;
            }
            Flush();
            return text.ToString();
        }

        private static StringBuilder AppendRange(StringBuilder text, char from, char to)
        {
            AppendUnit(text, from);
            if (to != from)
                AppendUnit(text.Append('-'), to);
            return text;
        }

        private static StringBuilder AppendUnit(StringBuilder text, char unit)
            => text.Append(@"\u").Append(((int)unit).ToString("X4", CultureInfo.InvariantCulture));
    }
}
