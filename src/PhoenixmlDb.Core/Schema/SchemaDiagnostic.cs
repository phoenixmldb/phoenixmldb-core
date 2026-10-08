using System.Globalization;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>How serious a <see cref="SchemaDiagnostic"/> is.</summary>
public enum SchemaSeverity
{
    /// <summary>The schema or the instance is still usable.</summary>
    Warning,

    /// <summary>The schema does not compile, or the instance is not valid.</summary>
    Error,
}

/// <summary>
/// One thing a schema compilation or a validation reports, in the same shape whoever asks.
/// </summary>
/// <remarks>
/// <see cref="Code"/> and <see cref="MessageId"/> come from which message the schema processor
/// (System.Xml's) reports, never from the wording of the message. The processor keeps that in
/// private state, so both are null where it cannot be read.
/// </remarks>
/// <param name="Severity">Warning or error.</param>
/// <param name="Message">The message, as the schema processor gives it.</param>
/// <param name="SourceUri">The document the diagnostic is about, when it is known.</param>
/// <param name="Line">The line in that document, from 1; 0 when it is not known.</param>
/// <param name="Column">The column in that line, from 1; 0 when it is not known.</param>
public sealed record SchemaDiagnostic(
    SchemaSeverity Severity,
    string Message,
    string? SourceUri = null,
    int Line = 0,
    int Column = 0)
{
    /// <summary>
    /// The validation rule of the XSD specification that the diagnostic reports, by the name
    /// the specification gives it (<c>cvc-complex-type.2.4.a</c>, <c>cvc-pattern-valid</c>), for
    /// a validation error whose message means exactly one rule. Null for every other
    /// diagnostic: a message that covers several rules (a duplicate key and a duplicate unique
    /// value share one), a compilation diagnostic, and one the layer reports itself.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// The schema processor's own name for the message (<c>Sch_UndeclaredElement</c>): the same
    /// for every diagnostic of one kind, whatever names and values its text carries, so a host
    /// can tell diagnostics apart without reading the text. Null when it is not known, and for
    /// a diagnostic the layer reports itself. The names are System.Xml's and are not a contract
    /// of this library: they may differ between .NET versions.
    /// </summary>
    public string? MessageId { get; init; }

    internal static SchemaDiagnostic From(XmlSchemaException exception, XmlSeverityType severity)
    {
        var (messageId, _) = SchemaMessageIds.Of(exception);
        return new(
            severity == XmlSeverityType.Warning ? SchemaSeverity.Warning : SchemaSeverity.Error,
            exception.Message,
            string.IsNullOrEmpty(exception.SourceUri) ? null : exception.SourceUri,
            exception.LineNumber,
            exception.LinePosition) { MessageId = messageId };
    }

    /// <summary>The message with its position, for a log or an exception.</summary>
    public override string ToString()
    {
        var where = SourceUri is null ? "" : Line > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{SourceUri}({Line},{Column}): ")
            : SourceUri + ": ";
        return where + (Severity == SchemaSeverity.Warning ? "warning: " : "error: ") + Message;
    }
}

/// <summary>A schema could not be loaded or does not compile.</summary>
public sealed class SchemaCompilationException : XmlDbException
{
    /// <summary>Every error and warning the compilation reported, in order.</summary>
    public IReadOnlyList<SchemaDiagnostic> Diagnostics { get; } = [];

    /// <inheritdoc/>
    public SchemaCompilationException()
    {
    }

    /// <inheritdoc/>
    public SchemaCompilationException(string message) : base(message)
    {
    }

    /// <inheritdoc/>
    public SchemaCompilationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>A failed compilation with what it reported.</summary>
    public SchemaCompilationException(string message, IReadOnlyList<SchemaDiagnostic> diagnostics,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        Diagnostics = diagnostics;
    }
}
