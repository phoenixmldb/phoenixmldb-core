namespace PhoenixmlDb.Core.Storage;

/// <summary>
/// Abstraction over key-value storage engines.
/// Defines the contract that storage implementations (LMDB, etc.) must fulfill.
/// This interface lives in Core so that open-source components (XQuery, XSLT)
/// can reference storage abstractions without depending on the concrete
/// storage implementation.
/// </summary>
public interface IStorageEngine : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Opens or creates a named database within the environment.
    /// </summary>
    IDatabase OpenDatabase(string name, DatabaseOptions? options = null);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    IStorageTransaction BeginTransaction(TransactionMode mode = TransactionMode.Read);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    StorageStatistics GetStatistics();

    /// <summary>
    /// Flushes any buffered data to disk.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A named database within the storage environment.
/// </summary>
public interface IDatabase
{
    string Name { get; }
    DatabaseOptions Options { get; }
}

/// <summary>
/// A storage transaction.
/// </summary>
public interface IStorageTransaction : IDisposable
{
    /// <summary>Transaction mode (read or write).</summary>
    TransactionMode Mode { get; }

    /// <summary>Gets a value by key.</summary>
    bool TryGet(IDatabase db, ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value);

    /// <summary>Puts a key-value pair.</summary>
    void Put(IDatabase db, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, PutOptions options = PutOptions.None);

    /// <summary>Deletes a key.</summary>
    bool Delete(IDatabase db, ReadOnlySpan<byte> key);

    /// <summary>Creates a cursor for iteration.</summary>
    IStorageCursor CreateCursor(IDatabase db);

    /// <summary>Commits the transaction.</summary>
    void Commit();

    /// <summary>Aborts the transaction.</summary>
    void Abort();
}

/// <summary>
/// A cursor for iterating over key-value pairs.
/// </summary>
public interface IStorageCursor : IDisposable
{
    /// <summary>Current key-value pair.</summary>
    KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> Current { get; }

    /// <summary>Positions cursor at exact key.</summary>
    bool SetKey(ReadOnlySpan<byte> key);

    /// <summary>Positions cursor at first key >= given key.</summary>
    bool SetRange(ReadOnlySpan<byte> key);

    /// <summary>Moves to first entry.</summary>
    bool MoveFirst();

    /// <summary>Moves to last entry.</summary>
    bool MoveLast();

    /// <summary>Moves to next entry.</summary>
    bool MoveNext();

    /// <summary>Moves to previous entry.</summary>
    bool MovePrevious();

    /// <summary>For duplicate key databases, moves to next duplicate.</summary>
    bool MoveNextDuplicate();

    /// <summary>Deletes the current entry.</summary>
    bool Delete();
}

/// <summary>
/// Database configuration options.
/// </summary>
public record DatabaseOptions
{
    public bool AllowDuplicates { get; init; }
    public bool IntegerKey { get; init; }
    public bool ReverseKey { get; init; }
    public bool IntegerDuplicates { get; init; }
    public bool ReverseDuplicates { get; init; }
}

/// <summary>
/// Put operation options.
/// </summary>
[Flags]
public enum PutOptions
{
    None = 0,
    NoOverwrite = 1,
    NoDuplicateData = 2,
    Append = 4,
    AppendDuplicate = 8
}

/// <summary>
/// Transaction mode.
/// </summary>
public enum TransactionMode
{
    Read,
    Write
}

/// <summary>
/// Storage statistics.
/// </summary>
public record StorageStatistics
{
    public required long DatabaseSizeBytes { get; init; }
    public required long UsedSizeBytes { get; init; }
    public required long EntryCount { get; init; }
    public required int TreeDepth { get; init; }
    public required long BranchPages { get; init; }
    public required long LeafPages { get; init; }
    public required long OverflowPages { get; init; }
}
