using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// Root entry point for all PhoenixmlDb operations.
/// </summary>
/// <remarks>
/// <para>
/// <c>IDocumentDatabase</c> is the first object you create when working with PhoenixmlDb.
/// It manages the underlying LMDB storage engine and provides access to containers,
/// which hold collections of XML and JSON documents.
/// </para>
/// <para>
/// A database maps to a single directory on disk. All containers and their documents
/// are stored within this directory using LMDB's memory-mapped file architecture,
/// which provides excellent read performance and ACID transaction guarantees.
/// </para>
/// <para>
/// <b>Basic usage:</b>
/// <code>
/// await using var db = await DocumentDatabase.OpenAsync("/path/to/db");
/// var container = await db.OpenOrCreateContainerAsync("products", opts =>
///     opts.Indexes.AddPathIndex("//product/name")
///                 .AddValueIndex("//product/price", XdmValueType.XdmDecimal));
/// await container.PutDocumentAsync("item1.xml", xmlContent);
/// </code>
/// </para>
/// <para>
/// <b>Thread safety:</b> An <c>IDocumentDatabase</c> instance is thread-safe. Multiple
/// threads can read concurrently. Write transactions are serialized — only one write
/// transaction can be active at a time, consistent with LMDB's single-writer model.
/// </para>
/// <para>
/// <b>Disposal:</b> Always dispose the database when done. This flushes pending writes
/// and releases the LMDB environment. Use <c>await using</c> for automatic disposal.
/// </para>
/// </remarks>
public interface IDocumentDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the filesystem path where this database is stored.
    /// </summary>
    /// <remarks>
    /// This is the directory passed to <c>DocumentDatabase.OpenAsync()</c>.
    /// The directory contains LMDB data files (<c>data.mdb</c> and <c>lock.mdb</c>).
    /// </remarks>
    string Path { get; }

    /// <summary>
    /// Gets current database statistics including size, container count, and document count.
    /// </summary>
    /// <remarks>
    /// Statistics are computed from the LMDB environment and are relatively inexpensive to read.
    /// Use this for monitoring and diagnostics, not for business logic — counts may lag
    /// slightly behind concurrent writes.
    /// </remarks>
    DatabaseStatistics Statistics { get; }

    /// <summary>
    /// Creates a new container with the specified name and optional configuration.
    /// </summary>
    /// <param name="name">
    /// Container name. Must be unique within the database. Use meaningful names
    /// that describe the document collection, e.g. "orders", "customers", "config".
    /// </param>
    /// <param name="configure">
    /// Optional configuration action to set up indexes, default namespaces, and
    /// validation mode. If null, the container is created with default settings (no indexes).
    /// Indexes can be added later, but existing documents won't be retroactively indexed.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created container, ready for document operations.</returns>
    /// <exception cref="InvalidOperationException">
    /// A container with this name already exists. Use <see cref="OpenOrCreateContainerAsync"/>
    /// if you want create-if-not-exists semantics.
    /// </exception>
    /// <example>
    /// <code>
    /// var products = await db.CreateContainerAsync("products", opts =>
    /// {
    ///     opts.Indexes
    ///         .AddPathIndex("//product/@id")
    ///         .AddValueIndex("//product/price", XdmValueType.XdmDecimal)
    ///         .AddFullTextIndex("//product/description");
    ///     opts.DefaultNamespaces.Add("p", "http://example.com/products");
    /// });
    /// </code>
    /// </example>
    ValueTask<IContainer> CreateContainerAsync(
        string name,
        Action<ContainerOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an existing container by name.
    /// </summary>
    /// <param name="name">Container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The container, or <c>null</c> if no container with this name exists.</returns>
    /// <remarks>
    /// This is the preferred method when you know the container should exist and want
    /// to handle the not-found case explicitly. For fire-and-forget scenarios, use
    /// <see cref="OpenOrCreateContainerAsync"/>.
    /// </remarks>
    ValueTask<IContainer?> OpenContainerAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a container, creating it if it doesn't already exist.
    /// </summary>
    /// <param name="name">Container name.</param>
    /// <param name="configure">
    /// Configuration action applied only when creating a new container.
    /// Ignored if the container already exists.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created container.</returns>
    /// <remarks>
    /// This is the most convenient method for application startup — call it without
    /// worrying about whether the container exists yet. Note that the <paramref name="configure"/>
    /// action is only applied on creation; it won't modify an existing container's settings.
    /// </remarks>
    ValueTask<IContainer> OpenOrCreateContainerAsync(
        string name,
        Action<ContainerOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a container and all of its documents, indexes, and metadata.
    /// </summary>
    /// <param name="name">Container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the container existed and was deleted; <c>false</c> if it didn't exist.</returns>
    /// <remarks>
    /// <b>This operation is irreversible.</b> All documents, metadata, and index data
    /// within the container are permanently removed. The disk space is reclaimed
    /// by LMDB for future use.
    /// </remarks>
    ValueTask<bool> DeleteContainerAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all containers in the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An async enumerable of <see cref="ContainerInfo"/> records with container
    /// metadata including name, creation date, and document count.
    /// </returns>
    IAsyncEnumerable<ContainerInfo> ListContainersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a read-only transaction providing snapshot isolation.
    /// </summary>
    /// <returns>A read transaction that sees a consistent snapshot of the database.</returns>
    /// <remarks>
    /// <para>
    /// Read transactions provide MVCC (Multi-Version Concurrency Control) snapshot isolation.
    /// Once begun, the transaction sees a frozen view of the database — concurrent writes
    /// by other threads or processes are invisible. This guarantees consistent reads
    /// without locking.
    /// </para>
    /// <para>
    /// Multiple read transactions can be active simultaneously. Read transactions
    /// are lightweight and do not block writers.
    /// </para>
    /// <para>
    /// <b>Important:</b> Always dispose read transactions promptly. Long-lived read
    /// transactions prevent LMDB from reclaiming disk space used by older versions of data.
    /// </para>
    /// <para>
    /// For simple single-operation reads, the <see cref="IContainer"/> methods
    /// (e.g., <c>GetDocumentAsync</c>) handle transactions automatically.
    /// Use explicit transactions when you need to read multiple documents
    /// with a consistent view.
    /// </para>
    /// </remarks>
    IReadTransaction BeginRead();

    /// <summary>
    /// Begins a read-write transaction, waiting indefinitely for the write lock.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write transaction. Call <c>CommitAsync()</c> to persist changes or
    /// <c>RollbackAsync()</c> to discard them.</returns>
    /// <remarks>
    /// <para>
    /// Only one write transaction can be active at a time across all threads and processes
    /// accessing the same database. If another write transaction is active, this method
    /// blocks until it completes (commits or rolls back).
    /// </para>
    /// <para>
    /// Use the overload with <see cref="TimeSpan"/> timeout to avoid indefinite blocking.
    /// </para>
    /// <para>
    /// Write transactions also provide read access — you can query documents within the
    /// same transaction to implement read-modify-write patterns.
    /// </para>
    /// </remarks>
    ValueTask<IWriteTransaction> BeginWriteAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a read-write transaction with a timeout for acquiring the write lock.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the write lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write transaction.</returns>
    /// <exception cref="TransactionTimeoutException">
    /// The write lock could not be acquired within the specified timeout.
    /// </exception>
    /// <remarks>
    /// Prefer this overload in production code to prevent threads from blocking
    /// indefinitely. A timeout of 5-30 seconds is typical for most applications.
    /// </remarks>
    ValueTask<IWriteTransaction> BeginWriteAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces any buffered writes to be flushed to persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// LMDB normally syncs data to disk on transaction commit. Call this method
    /// if you need to guarantee durability at a specific point, for example before
    /// reporting success to a client.
    /// </remarks>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregate statistics for a PhoenixmlDb database.
/// </summary>
/// <remarks>
/// Use these statistics for monitoring dashboard displays, health checks,
/// and capacity planning. All sizes are in bytes.
/// </remarks>
public record DatabaseStatistics
{
    /// <summary>Total size of the database file on disk, in bytes.</summary>
    public required long DatabaseSizeBytes { get; init; }

    /// <summary>Bytes actually used by data (the rest is free space for future writes).</summary>
    public required long UsedSizeBytes { get; init; }

    /// <summary>Number of containers in the database.</summary>
    public required int ContainerCount { get; init; }

    /// <summary>Total number of documents across all containers.</summary>
    public required long TotalDocumentCount { get; init; }

    /// <summary>Total number of XDM nodes across all documents.</summary>
    public required long TotalNodeCount { get; init; }
}

/// <summary>
/// Summary information about a container, returned by <see cref="IDocumentDatabase.ListContainersAsync"/>.
/// </summary>
public record ContainerInfo
{
    /// <summary>The container's unique identifier.</summary>
    public required ContainerId Id { get; init; }

    /// <summary>The container's name.</summary>
    public required string Name { get; init; }

    /// <summary>When the container was created.</summary>
    public required DateTimeOffset Created { get; init; }

    /// <summary>When the container was last modified (document added, updated, or deleted).</summary>
    public required DateTimeOffset Modified { get; init; }

    /// <summary>Number of documents currently stored in the container.</summary>
    public required long DocumentCount { get; init; }
}
