namespace PhoenixmlDb.Core.Schema;

/// <summary>Limits on what one schema compilation reads, and how it reads it.</summary>
public sealed record SchemaCompileOptions
{
    /// <summary>The options used when none are given.</summary>
    public static SchemaCompileOptions Default { get; } = new();

    /// <summary>
    /// The largest single schema document, in bytes. Reading stops there: no more than this is
    /// taken from any one stream. 16 MiB by default.
    /// </summary>
    public long MaxDocumentBytes { get; init; } = 16L * 1024 * 1024;

    /// <summary>The most all the documents of one compilation may come to, in bytes. 64 MiB by default.</summary>
    public long MaxTotalBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>The most documents one compilation may read, roots included. 1,024 by default.</summary>
    public int MaxDocuments { get; init; } = 1024;

    /// <summary>
    /// Whether a schema document written for XSD 1.1 is made loadable by the XSD 1.0 processor
    /// the layer uses. True by default. The parts a document marks as 1.1-only with the
    /// <c>vc:</c> versioning attributes are left out, as XSD 1.1 §4.2.2 says a 1.0 processor is to,
    /// and a reference to a built-in type that 1.1 added (<c>xs:dayTimeDuration</c>,
    /// <c>xs:yearMonthDuration</c>, <c>xs:dateTimeStamp</c>) is read as the 1.0 type it restricts.
    /// With false such a schema fails to compile.
    /// </summary>
    public bool Xsd11Compatibility { get; init; } = true;
}
