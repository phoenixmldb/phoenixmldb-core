using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

internal static partial class SchemaPatternGuard
{
    /// <summary>
    /// The length a string type asks for, in characters: what the length facets of one
    /// restriction said before they were turned into a pattern.
    /// </summary>
    private sealed record LengthBounds(int? Length, int? Min, int? Max);

    private static readonly ConditionalWeakTable<XmlSchemaObject, LengthBounds> LengthsOf = new();

    // One character, by code point, a line end included.
    private const string OneCharacter = @"(?:[^\uD800-\uDFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

    private const string PatternFailed = "The Pattern constraint failed.";

    /// <summary>
    /// Makes the length facets of the string types of a compiled schema set count characters as
    /// XML Schema does, by code point. System.Xml counts UTF-16 units, so
    /// <c>maxLength="1"</c> refused one character outside the Basic Multilingual Plane and
    /// <c>minLength="2"</c> accepted it. Call it after the set has compiled, so that the rules
    /// that relate the length facets of a type to those of its base have been checked on the
    /// facets as written. When it returns true it has changed the set, which must be compiled
    /// again (<see cref="XmlSchemaSet.Reprocess"/> each schema, then compile).
    /// </summary>
    /// <remarks>
    /// The count is inside System.Xml and cannot be changed, so each <c>length</c>,
    /// <c>minLength</c> and <c>maxLength</c> of a type derived from xs:string or xs:anyURI is
    /// taken out and a pattern put in its place that asks for the same number of characters.
    /// The facets of a list type (a number of items) and of a binary type (a number of octets)
    /// are left alone. A value then fails as a pattern does; <see cref="RestoreLengthMessage(string, XmlSchemaType?)"/>
    /// gives the message of the facet back.
    /// </remarks>
    public static bool CountCharactersInLengths(XmlSchemaSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var changed = false;
        foreach (var obj in Walk(set))
        {
            var facets = obj switch
            {
                XmlSchemaSimpleTypeRestriction restriction => restriction.Facets,
                XmlSchemaSimpleContentRestriction restriction => restriction.Facets,
                _ => null,
            };
            if (facets is null || facets.Count == 0 || !CountsCharacters((XmlSchemaObject)obj))
                continue;

            int? length = null, min = null, max = null;
            for (var i = facets.Count - 1; i >= 0; i--)
            {
                if (facets[i] is not XmlSchemaFacet { Value: { } text } facet
                    || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    continue;
                switch (facet)
                {
                    case XmlSchemaLengthFacet: length = value; break;
                    case XmlSchemaMinLengthFacet: min = value; break;
                    case XmlSchemaMaxLengthFacet: max = value; break;
                    default: continue;
                }
                facets.RemoveAt(i);
            }
            if (length is null && min is null && max is null)
                continue;

            var pattern = LengthPattern(length ?? min ?? 0, length ?? max);
            var combined = false;
            foreach (var existing in facets)
            {
                // The patterns of one restriction are alternatives, so the length is asked of
                // each of them and not added as one more.
                if (existing is not XmlSchemaPatternFacet { Value: { } written } own)
                    continue;
                own.Value = @"(?=" + pattern + @"\z)(?:" + written + ")";
                Rewritten.AddOrUpdate(own, own.Value);
                combined = true;
            }
            if (!combined)
            {
                var added = new XmlSchemaPatternFacet { Value = pattern };
                facets.Add(added);
                Rewritten.AddOrUpdate(added, pattern);
            }
            LengthsOf.AddOrUpdate((XmlSchemaObject)obj, new LengthBounds(length, min, max));
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// <see cref="CountCharactersInLengths"/> on a set that has just compiled, and the second
    /// compilation it asks for. Nothing happens for a set that did not compile, or that has no
    /// length facet left to change.
    /// </summary>
    public static void CompileWithCharacterLengths(XmlSchemaSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (!set.IsCompiled || !CountCharactersInLengths(set))
            return;
        foreach (XmlSchema schema in set.Schemas())
            set.Reprocess(schema);
        set.Compile();
    }

    /// <summary>
    /// Whether the restriction is of a type whose length is a number of characters: an atomic
    /// type derived from xs:string or xs:anyURI.
    /// </summary>
    private static bool CountsCharacters(XmlSchemaObject restriction)
    {
        var datatype = restriction.Parent switch
        {
            XmlSchemaSimpleType simple => simple.Datatype,
            XmlSchemaSimpleContent { Parent: XmlSchemaComplexType complex } => complex.Datatype,
            _ => null,
        };
        return datatype is { Variety: XmlSchemaDatatypeVariety.Atomic }
            && datatype.TypeCode is XmlTypeCode.String or XmlTypeCode.NormalizedString or XmlTypeCode.Token
                or XmlTypeCode.Language or XmlTypeCode.NmToken or XmlTypeCode.Name or XmlTypeCode.NCName
                or XmlTypeCode.Id or XmlTypeCode.Idref or XmlTypeCode.Entity or XmlTypeCode.AnyUri;
    }

    /// <summary>
    /// A pattern for a value of <paramref name="min"/> to <paramref name="max"/> characters.
    /// A value with no surrogate pair is counted by unit, which is the same number and far
    /// quicker; one with a pair is counted by code point.
    /// </summary>
    private static string LengthPattern(int min, int? max)
    {
        var count = max is { } most
            ? string.Create(CultureInfo.InvariantCulture, $"{{{min},{most}}}")
            : string.Create(CultureInfo.InvariantCulture, $"{{{min},}}");
        return @"(?(?=[^\uD800-\uDFFF]*\z)[\s\S]" + count + "|" + OneCharacter + count + ")";
    }

    private static readonly Regex FacetMessage = new(
        @"The value '(?<value>.*)' is invalid according to its datatype '[^']*' - The Pattern constraint failed\.\z",
        RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex AttributeMessage = new(
        @"\AThe '(?<name>[^']+)' attribute is invalid - ",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// <see cref="RestoreLengthMessage(string, XmlSchemaType?)"/> for a message raised while
    /// <paramref name="reader"/> validates: the type is that of what the event names, or of the
    /// element the reader is on, or of the attribute of it that the message names.
    /// </summary>
    public static string RestoreLengthMessage(string message, XmlReader? reader, XmlSchemaException? reported = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.EndsWith(PatternFailed, StringComparison.Ordinal))
            return message;
        // What the event itself names, when it names anything.
        switch (reported?.SourceSchemaObject)
        {
            case XmlSchemaAttribute { AttributeSchemaType: { } named }:
                return RestoreLengthMessage(message, named);
            case XmlSchemaElement { ElementSchemaType: { } named }:
                return RestoreLengthMessage(message, named);
            case XmlSchemaType named:
                return RestoreLengthMessage(message, named);
        }
        if (reader is null)
            return message;
        var attribute = AttributeMessage.Match(message);
        if (!attribute.Success)
            return RestoreLengthMessage(message, reader.SchemaInfo?.SchemaType);
        if (reader.SchemaInfo?.SchemaAttribute?.AttributeSchemaType is { } attributeOfReader)
            return RestoreLengthMessage(message, attributeOfReader);

        // System.Xml reports an attribute before it says what the attribute or its element is.
        // The declarations of that name in the schema set stand in: when each one that the
        // value breaks a length of gives the same words, those are the words.
        if (reader.Settings?.Schemas is not { } set)
            return message;
        var name = attribute.Groups["name"].Value;
        var local = name[(name.LastIndexOf(':') + 1)..];
        string? restored = null;
        foreach (var type in AttributeTypes.GetValue(set, FindAttributeTypes).GetValueOrDefault(local) ?? [])
        {
            var candidate = RestoreLengthMessage(message, type);
            if (ReferenceEquals(candidate, message))
                continue;
            if (restored is not null && restored != candidate)
                return message;
            restored = candidate;
        }
        return restored ?? message;
    }

    private static readonly ConditionalWeakTable<XmlSchemaSet, Dictionary<string, List<XmlSchemaType>>> AttributeTypes = new();

    private static Dictionary<string, List<XmlSchemaType>> FindAttributeTypes(XmlSchemaSet set)
    {
        var byName = new Dictionary<string, List<XmlSchemaType>>(StringComparer.Ordinal);
        foreach (var obj in Walk(set))
        {
            if (obj is not XmlSchemaAttribute { AttributeSchemaType: { } type } attribute)
                continue;
            var local = attribute.QualifiedName.Name;
            if (string.IsNullOrEmpty(local))
                continue;
            if (!byName.TryGetValue(local, out var types))
                byName[local] = types = [];
            if (!types.Contains(type))
                types.Add(type);
        }
        return byName;
    }

    /// <summary>
    /// The message of a value that failed validation, with the words of the length facet it
    /// broke where System.Xml reports the pattern that stands for the facet
    /// (<see cref="CountCharactersInLengths"/>). Any other message is returned as it is.
    /// </summary>
    /// <param name="message">The message of the validation event.</param>
    /// <param name="type">The type of the element or attribute the value belongs to; null when not known.</param>
    public static string RestoreLengthMessage(string message, XmlSchemaType? type)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (type is null || !message.EndsWith(PatternFailed, StringComparison.Ordinal))
            return message;
        var match = FacetMessage.Match(message);
        if (!match.Success)
            return message;

        var characters = 0;
        foreach (var _ in match.Groups["value"].Value.EnumerateRunes())
            characters++;

        for (XmlSchemaType? t = type; t is not null; t = t.BaseXmlSchemaType)
        {
            if (RestrictionOf(t) is not { } restriction || !LengthsOf.TryGetValue(restriction, out var bounds))
                continue;
            var words = BrokenFacet(bounds, characters);
            if (words is not null)
                return string.Concat(message.AsSpan(0, message.Length - PatternFailed.Length), words);
        }
        return message;
    }

    /// <summary>
    /// The validation rule of the length facet a restored message names; null for any other
    /// message.
    /// </summary>
    public static string? LengthRule(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EndsWith("The actual length is greater than the MaxLength value.", StringComparison.Ordinal))
            return "cvc-maxLength-valid";
        if (message.EndsWith("The actual length is less than the MinLength value.", StringComparison.Ordinal))
            return "cvc-minLength-valid";
        if (message.EndsWith("The actual length is not equal to the specified length.", StringComparison.Ordinal))
            return "cvc-length-valid";
        return null;
    }

    private static XmlSchemaObject? RestrictionOf(XmlSchemaType type)
    {
        if (type is XmlSchemaSimpleType simple)
            return simple.Content as XmlSchemaSimpleTypeRestriction;
        if (type is XmlSchemaComplexType complex)
            return complex.ContentModel?.Content as XmlSchemaSimpleContentRestriction;
        return null;
    }

    private static string? BrokenFacet(LengthBounds bounds, int characters)
    {
        if (bounds.Length is { } exact && characters != exact)
            return "The actual length is not equal to the specified length.";
        if (bounds.Max is { } most && characters > most)
            return "The actual length is greater than the MaxLength value.";
        if (bounds.Min is { } least && characters < least)
            return "The actual length is less than the MinLength value.";
        return null;
    }
}
