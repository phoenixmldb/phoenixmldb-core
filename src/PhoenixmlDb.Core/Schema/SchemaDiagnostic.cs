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
    internal static SchemaDiagnostic From(XmlSchemaException exception, XmlSeverityType severity)
        => new(
            severity == XmlSeverityType.Warning ? SchemaSeverity.Warning : SchemaSeverity.Error,
            exception.Message,
            string.IsNullOrEmpty(exception.SourceUri) ? null : exception.SourceUri,
            exception.LineNumber,
            exception.LinePosition);

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
