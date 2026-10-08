using System.Reflection;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// Which of the schema processor's messages a diagnostic is, and the validation rule of the XSD
/// specification it reports, where one message means one rule.
/// </summary>
/// <remarks>
/// System.Xml does not publish which message an <see cref="XmlSchemaException"/> carries, only
/// its formatted text. It does keep the message's template, and its resource table names every
/// template; the two are matched here, by identity of the template and never by the wording of
/// a formatted message. Both are private to System.Xml, so on a runtime where they are not
/// found a diagnostic has no id and no code, and nothing else changes.
/// </remarks>
internal static class SchemaMessageIds
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly FieldInfo? TemplateField =
        typeof(XmlSchemaException).GetField("_res", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly Lazy<Dictionary<string, string>> IdsByTemplate = new(LoadIds);

    /// <summary>The validation rule each message reports, for the messages that report exactly one.</summary>
    private static readonly Dictionary<string, string> RuleById = new(StringComparer.Ordinal)
    {
        ["Sch_UndeclaredElement"] = "cvc-elt.1",
        ["Sch_InvalidXsiNill"] = "cvc-elt.3.1",
        ["Sch_ContentInNill"] = "cvc-elt.3.2.1",
        ["Sch_XsiTypeNotFound"] = "cvc-elt.4.2",
        ["Sch_XsiTypeBlockedEx"] = "cvc-elt.4.3",
        ["Sch_FixedElementValue"] = "cvc-elt.5.2.2",
        ["Sch_InvalidElementInEmptyEx"] = "cvc-complex-type.2.1",
        ["Sch_InvalidTextInEmpty"] = "cvc-complex-type.2.1",
        ["Sch_InvalidElementInTextOnlyEx"] = "cvc-complex-type.2.2",
        ["Sch_InvalidTextInElement"] = "cvc-complex-type.2.3",
        ["Sch_InvalidTextInElementExpecting"] = "cvc-complex-type.2.3",
        ["Sch_InvalidElementContentExpecting"] = "cvc-complex-type.2.4.a",
        ["Sch_InvalidElementContentExpectingComplex"] = "cvc-complex-type.2.4.a",
        ["Sch_IncompleteContentExpecting"] = "cvc-complex-type.2.4.b",
        ["Sch_IncompleteContentExpectingComplex"] = "cvc-complex-type.2.4.b",
        ["Sch_IncompleteContent"] = "cvc-complex-type.2.4.b",
        ["Sch_IncompleteContentComplex"] = "cvc-complex-type.2.4.b",
        ["Sch_InvalidElementContent"] = "cvc-complex-type.2.4.d",
        ["Sch_InvalidElementContentComplex"] = "cvc-complex-type.2.4.d",
        ["Sch_UndeclaredAttribute"] = "cvc-complex-type.3.2.2",
        ["Sch_MissRequiredAttribute"] = "cvc-complex-type.4",
        ["Sch_FixedAttributeValue"] = "cvc-attribute.4",
        ["Sch_DupId"] = "cvc-id.2",
        ["Sch_UndeclaredId"] = "cvc-id.1",
        ["Sch_MissingKey"] = "cvc-identity-constraint.4.2.1",
        ["Sch_UnresolvedKeyref"] = "cvc-identity-constraint.4.3",
        ["Sch_MaxInclusiveConstraintFailed"] = "cvc-maxInclusive-valid",
        ["Sch_MinInclusiveConstraintFailed"] = "cvc-minInclusive-valid",
        ["Sch_MaxExclusiveConstraintFailed"] = "cvc-maxExclusive-valid",
        ["Sch_MinExclusiveConstraintFailed"] = "cvc-minExclusive-valid",
        ["Sch_PatternConstraintFailed"] = "cvc-pattern-valid",
        ["Sch_EnumerationConstraintFailed"] = "cvc-enumeration-valid",
        ["Sch_LengthConstraintFailed"] = "cvc-length-valid",
        ["Sch_MinLengthConstraintFailed"] = "cvc-minLength-valid",
        ["Sch_MaxLengthConstraintFailed"] = "cvc-maxLength-valid",
        ["Sch_TotalDigitsConstraintFailed"] = "cvc-totalDigits-valid",
        ["Sch_FractionDigitsConstraintFailed"] = "cvc-fractionDigits-valid",
    };

    /// <summary>The messages that say a value is not valid for its type, and wrap the reason.</summary>
    private static readonly Dictionary<string, string> ValueRuleById = new(StringComparer.Ordinal)
    {
        ["Sch_ElementValueDataType"] = "cvc-type.3.1.3",
        ["Sch_ElementValueDataTypeDetailed"] = "cvc-type.3.1.3",
        ["Sch_AttributeValueDataType"] = "cvc-attribute.3",
        ["Sch_AttributeValueDataTypeDetailed"] = "cvc-attribute.3",
    };

    /// <summary>
    /// The id of the message <paramref name="exception"/> carries, and the rule it reports.
    /// A value that is not valid for its type is reported by the facet it breaks when there is
    /// one (<c>cvc-pattern-valid</c>), by <c>cvc-datatype-valid.1.2.1</c> when it is not in the
    /// type's lexical space, and otherwise by the rule for the element or attribute.
    /// </summary>
    public static (string? MessageId, string? Code) Of(XmlSchemaException exception)
    {
        var id = IdOf(exception);
        if (id is null)
            return (null, null);
        if (RuleById.TryGetValue(id, out var rule))
            return (id, rule);
        if (!ValueRuleById.TryGetValue(id, out var valueRule))
            return (id, null);
        return exception.InnerException switch
        {
            XmlSchemaException inner when IdOf(inner) is { } innerId && RuleById.TryGetValue(innerId, out var facetRule) => (id, facetRule),
            XmlSchemaException => (id, valueRule),
            null => (id, valueRule),
            // The value could not be read as the type at all: a FormatException or the like.
            _ => (id, "cvc-datatype-valid.1.2.1"),
        };
    }

    private static string? IdOf(XmlSchemaException exception)
    {
        try
        {
            return TemplateField?.GetValue(exception) is string template
                && IdsByTemplate.Value.TryGetValue(template, out var id) ? id : null;
        }
        catch (Exception e) when (e is FieldAccessException or TargetException or NotSupportedException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> LoadIds()
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var table = typeof(XmlSchemaException).Assembly.GetType("System.SR");
            if (table is null)
                return ids;
            foreach (var property in table.GetProperties(AnyStatic))
            {
                if (property.PropertyType != typeof(string) || !property.Name.StartsWith("Sch_", StringComparison.Ordinal))
                    continue;
                // Two messages with one template cannot be told apart: neither gets an id.
                if (property.GetValue(null) is string template && !ids.TryAdd(template, property.Name))
                    ids[template] = "";
            }
            foreach (var shared in ids.Where(pair => pair.Value.Length == 0).Select(pair => pair.Key).ToList())
                ids.Remove(shared);
        }
        catch (Exception e) when (e is TargetInvocationException or MemberAccessException or NotSupportedException or TypeLoadException)
        {
            ids.Clear();
        }
        return ids;
    }
}
