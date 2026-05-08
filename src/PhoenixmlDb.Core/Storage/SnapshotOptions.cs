namespace PhoenixmlDb.Core.Storage;

/// <summary>
/// Options for <see cref="IStorageEngine.SnapshotAsync"/>.
/// </summary>
/// <remarks>
/// Currently a placeholder for future expansion (compaction during snapshot, encryption,
/// progress reporting, etc.). Pass <c>null</c> or a default-constructed instance to use
/// the engine's default snapshot behavior.
/// </remarks>
public sealed record SnapshotOptions
{
    /// <summary>
    /// When true, the snapshot is compacted (free pages elided) during write. May be
    /// significantly slower than a raw copy but produces a smaller stream. Default: false.
    /// </summary>
    /// <remarks>
    /// Engines that do not support compaction during snapshot ignore this flag and
    /// produce a raw copy regardless.
    /// </remarks>
    public bool Compact { get; init; }
}
