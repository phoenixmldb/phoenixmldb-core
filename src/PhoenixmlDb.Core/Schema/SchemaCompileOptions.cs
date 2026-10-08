namespace PhoenixmlDb.Core.Schema;

/// <summary>Limits on what one schema compilation reads.</summary>
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
}
