using System.Xml;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>What one validation checks, and when it stops.</summary>
public sealed record SchemaValidationOptions
{
    /// <summary>The options used when none are given.</summary>
    public static SchemaValidationOptions Default { get; } = new();

    /// <summary>
    /// Whether the document element must be declared by the schema. True by default. An element
    /// the schema says nothing about is otherwise not an error to the schema processor, so a
    /// document validated against the wrong schema, or against none of its namespace, would be
    /// reported valid.
    /// </summary>
    public bool RequireDeclaredRoot { get; init; } = true;

    /// <summary>Whether <c>xs:key</c>, <c>xs:keyref</c> and <c>xs:unique</c> are checked. True by default.</summary>
    public bool CheckIdentityConstraints { get; init; } = true;

    /// <summary>Whether the schema processor's warnings are reported with the errors. False by default.</summary>
    public bool ReportWarnings { get; init; }

    /// <summary>
    /// The most diagnostics collected. Validation stops there and the result says it was cut
    /// short. 1,000 by default.
    /// </summary>
    public int MaxDiagnostics { get; init; } = 1000;

    /// <summary>
    /// How deeply the instance's elements may nest; deeper is an error and validation stops.
    /// 0, the default, sets no limit: the layer does not know what a host's documents look like.
    /// </summary>
    public int MaxDepth { get; init; }
}

/// <summary>What a validation found.</summary>
public sealed class SchemaValidationResult
{
    internal SchemaValidationResult(IReadOnlyList<SchemaDiagnostic> diagnostics, bool truncated, bool patternTimedOut = false)
    {
        PatternTimedOut = patternTimedOut;
        Diagnostics = diagnostics;
        Truncated = truncated;
        IsValid = !truncated && !diagnostics.Any(d => d.Severity == SchemaSeverity.Error);
    }

    /// <summary>True when no error was reported. False whenever validation stopped early.</summary>
    public bool IsValid { get; }

    /// <summary>The errors, and the warnings when they were asked for, in document order.</summary>
    public IReadOnlyList<SchemaDiagnostic> Diagnostics { get; }

    /// <summary>
    /// True when validation stopped before the end of the document: at
    /// <see cref="SchemaValidationOptions.MaxDiagnostics"/>, at
    /// <see cref="SchemaValidationOptions.MaxDepth"/>, or where the document is not well-formed.
    /// What comes after that point was not looked at.
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// True when validation stopped because one match of a pattern facet ran past the schema's
    /// <see cref="CompiledSchema.PatternMatchTimeout"/>. <see cref="Truncated"/> is then true too.
    /// </summary>
    public bool PatternTimedOut { get; }
}

/// <summary>
/// Validates an instance document against a <see cref="CompiledSchema"/>, with one result shape
/// whoever asks.
/// </summary>
/// <remarks>
/// <para>
/// Any number of validations may run against one compiled schema at once.
/// </para>
/// <para>
/// The instance is read with no document type declaration allowed and nothing resolved. What the
/// instance says about its own schema is not followed: <c>xsi:schemaLocation</c>,
/// <c>xsi:noNamespaceSchemaLocation</c> and a schema written inline in the instance are ignored.
/// The schema validated against is the one passed in.
/// </para>
/// <para>
/// A match of a pattern facet takes no longer than the schema's
/// <see cref="CompiledSchema.PatternMatchTimeout"/>. A schema compiled without one has no limit:
/// a pattern that backtracks catastrophically on a value then takes as long as that value makes
/// it. A host that validates against schemas or instances it does not trust sets
/// <see cref="SchemaCompileOptions.PatternMatchTimeout"/>.
/// </para>
/// </remarks>
public static class SchemaValidator
{
    /// <summary>Validates the document in <paramref name="instance"/>.</summary>
    /// <param name="schema">The schema to validate against.</param>
    /// <param name="instance">The document text.</param>
    /// <param name="sourceUri">What to call the document in diagnostics; null when it has no name.</param>
    /// <param name="options"><see cref="SchemaValidationOptions.Default"/> when null.</param>
    /// <param name="cancellationToken">Looked at between nodes.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static SchemaValidationResult Validate(CompiledSchema schema, string instance, Uri? sourceUri = null,
        SchemaValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        using var text = new StringReader(instance);
        return Validate(schema, text, sourceUri, options, cancellationToken);
    }

    /// <inheritdoc cref="Validate(CompiledSchema, string, Uri?, SchemaValidationOptions?, CancellationToken)"/>
    public static SchemaValidationResult Validate(CompiledSchema schema, TextReader instance, Uri? sourceUri = null,
        SchemaValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(instance);
        return Run(schema, settings => XmlReader.Create(instance, settings, sourceUri?.AbsoluteUri), sourceUri,
            options ?? SchemaValidationOptions.Default, cancellationToken);
    }

    /// <inheritdoc cref="Validate(CompiledSchema, string, Uri?, SchemaValidationOptions?, CancellationToken)"/>
    public static SchemaValidationResult Validate(CompiledSchema schema, Stream instance, Uri? sourceUri = null,
        SchemaValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(instance);
        return Run(schema, settings => XmlReader.Create(instance, settings, sourceUri?.AbsoluteUri), sourceUri,
            options ?? SchemaValidationOptions.Default, cancellationToken);
    }

    private static SchemaValidationResult Run(CompiledSchema schema, Func<XmlReaderSettings, XmlReader> open,
        Uri? sourceUri, SchemaValidationOptions options, CancellationToken cancellationToken)
    {
        var diagnostics = new List<SchemaDiagnostic>();
        var truncated = false;
        var patternTimedOut = false;
        var name = sourceUri?.AbsoluteUri;

        void Report(SchemaSeverity severity, string message, int line, int column)
        {
            if (diagnostics.Count < options.MaxDiagnostics)
                diagnostics.Add(new SchemaDiagnostic(severity, message, name, line, column));
            else
                truncated = true;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schema.SchemaSet,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = false,
            // Stated in full: the default includes following the instance's own schema hints off,
            // but it is not left to a default.
            ValidationFlags = XmlSchemaValidationFlags.AllowXmlAttributes
                | (options.CheckIdentityConstraints ? XmlSchemaValidationFlags.ProcessIdentityConstraints : 0)
                | (options.ReportWarnings ? XmlSchemaValidationFlags.ReportValidationWarnings : 0),
        };
        settings.ValidationEventHandler += (_, e) => Report(
            e.Severity == XmlSeverityType.Warning ? SchemaSeverity.Warning : SchemaSeverity.Error,
            e.Message, e.Exception.LineNumber, e.Exception.LinePosition);

        try
        {
            using var reader = open(settings);
            var sawRoot = false;
            while (!truncated && reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element)
                    continue;
                var position = reader as IXmlLineInfo;
                if (options.MaxDepth > 0 && reader.Depth >= options.MaxDepth)
                {
                    Report(SchemaSeverity.Error,
                        $"The document nests elements more than {options.MaxDepth} levels deep; validation stopped.",
                        position?.LineNumber ?? 0, position?.LinePosition ?? 0);
                    truncated = true;
                    break;
                }
                if (!sawRoot)
                {
                    sawRoot = true;
                    if (options.RequireDeclaredRoot && reader.SchemaInfo?.SchemaElement is null)
                    {
                        var ns = reader.NamespaceURI.Length == 0 ? "no namespace" : $"namespace '{reader.NamespaceURI}'";
                        Report(SchemaSeverity.Error,
                            $"The schema has no declaration for the document element '{reader.LocalName}' in {ns}.",
                            position?.LineNumber ?? 0, position?.LinePosition ?? 0);
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            Report(SchemaSeverity.Error, ex.Message, ex.LineNumber, ex.LinePosition);
            truncated = true;
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException ex)
        {
            Report(SchemaSeverity.Error,
                $"A pattern facet of the schema ran past the match time limit of {ex.MatchTimeout.TotalSeconds:0.###} s on a value of the document; validation stopped.",
                0, 0);
            truncated = true;
            patternTimedOut = true;
        }
        return new SchemaValidationResult(diagnostics, truncated, patternTimedOut);
    }
}
