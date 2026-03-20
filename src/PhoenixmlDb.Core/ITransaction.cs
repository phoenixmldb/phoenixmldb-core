using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// A read-only transaction that provides MVCC snapshot isolation over the database.
/// </summary>
/// <remarks>
/// <para>
/// A read transaction captures a frozen, point-in-time view of the database using
/// MVCC (Multi-Version Concurrency Control). Once begun, the transaction sees a consistent
/// snapshot — any concurrent writes by other threads or processes are invisible to it.
/// This guarantees repeatable reads without locking.
/// </para>
/// <para>
/// <b>Concurrency:</b> Multiple read transactions can be active simultaneously, and they
/// do not block writers. Read transactions are lightweight and non-blocking by design.
/// </para>
/// <para>
/// <b>When to use explicit read transactions:</b> Use a read transaction when you need to
/// read multiple documents or run multiple queries with a consistent view. For example,
/// reading a customer document and their related orders atomically ensures you don't see
/// a partially-updated state.
/// </para>
/// <para>
/// <b>When NOT to use:</b> For single document reads or single queries, the convenience
/// methods on <see cref="IContainer"/> (e.g., <see cref="IContainer.GetDocumentAsync"/>,
/// <see cref="IContainer.QueryAsync"/>) handle transactions automatically and are simpler.
/// </para>
/// <para>
/// <b>Important:</b> Always dispose read transactions promptly. Long-lived read transactions
/// prevent LMDB from reclaiming disk space used by older versions of data, which can cause
/// the database file to grow indefinitely.
/// </para>
/// </remarks>
/// <example>
/// <para>Read multiple documents with a consistent snapshot:</para>
/// <code>
/// using var txn = db.BeginRead();
/// var customer = await txn.GetDocumentAsync(customersContainer.Id, "acme.xml");
/// var orders = txn.ListDocumentsAsync(ordersContainer.Id);
///
/// // Both reads see the same snapshot — no concurrent writes are visible
/// await foreach (var order in orders)
/// {
///     Console.WriteLine($"Order: {order.Name}");
/// }
/// </code>
/// </example>
/// <seealso cref="IDocumentDatabase.BeginRead"/>
/// <seealso cref="IWriteTransaction"/>
public interface IReadTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the transaction ID, which corresponds to the LMDB snapshot version.
    /// </summary>
    /// <remarks>
    /// Transaction IDs are monotonically increasing. A higher ID means a more recent
    /// snapshot. This is primarily useful for debugging and diagnostics.
    /// </remarks>
    long TransactionId { get; }

    /// <summary>
    /// Gets whether this transaction is still active (has not been disposed or aborted).
    /// </summary>
    /// <remarks>
    /// Once a transaction is disposed, all operations on it throw <see cref="ObjectDisposedException"/>.
    /// Check this property if you need to verify the transaction is still usable before
    /// performing an operation.
    /// </remarks>
    bool IsActive { get; }

    /// <summary>
    /// Gets a document by name within this transaction's snapshot.
    /// </summary>
    /// <param name="container">The container to look up the document in.</param>
    /// <param name="name">The document name (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="IDocument"/> if found, or <c>null</c> if not found.</returns>
    /// <remarks>
    /// The returned document reflects the state at the time this transaction was begun,
    /// regardless of any concurrent modifications.
    /// </remarks>
    ValueTask<IDocument?> GetDocumentAsync(
        ContainerId container,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an XQuery expression within this transaction's snapshot.
    /// </summary>
    /// <param name="container">The container to query against.</param>
    /// <param name="xquery">The XQuery expression to execute.</param>
    /// <param name="variables">Optional external variable bindings (keys without <c>$</c> prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable yielding each item in the XQuery result sequence.</returns>
    /// <exception cref="XQueryException">The query has a syntax error or runtime error.</exception>
    /// <remarks>
    /// The query sees only documents that existed at the time this transaction was begun.
    /// This ensures query results are consistent even if other threads are modifying the database.
    /// </remarks>
    IAsyncEnumerable<object> QueryAsync(
        ContainerId container,
        string xquery,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents in a container within this transaction's snapshot.
    /// </summary>
    /// <param name="container">The container to list documents from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of <see cref="DocumentInfo"/> records.</returns>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        ContainerId container,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A read-write transaction providing full ACID guarantees for atomic multi-document operations.
/// </summary>
/// <remarks>
/// <para>
/// A write transaction inherits all read capabilities from <see cref="IReadTransaction"/>
/// and adds the ability to insert, update, and delete documents. All mutations within a
/// write transaction are buffered and become visible to other readers only after
/// <see cref="CommitAsync"/> is called.
/// </para>
/// <para>
/// <b>Single-writer model:</b> PhoenixmlDb follows LMDB's single-writer design — only one
/// write transaction can be active at a time across all threads and processes accessing the
/// same database. If another write transaction is in progress, acquiring a new one blocks
/// (or times out, if using the timeout overload of
/// <see cref="IDocumentDatabase.BeginWriteAsync(TimeSpan, CancellationToken)"/>).
/// </para>
/// <para>
/// <b>Commit or rollback:</b> You must explicitly call <see cref="CommitAsync"/> to persist
/// changes or <see cref="RollbackAsync"/> to discard them. If the transaction is disposed
/// without committing, all changes are automatically rolled back — but relying on this for
/// normal flow is discouraged; prefer explicit rollback for clarity.
/// </para>
/// <para>
/// <b>Common use patterns:</b>
/// </para>
/// <list type="bullet">
/// <item><description><b>Batch import:</b> Insert many documents in a single transaction for atomicity and performance.</description></item>
/// <item><description><b>Read-modify-write:</b> Read a document, transform it, and write it back within the same transaction.</description></item>
/// <item><description><b>Atomic multi-document updates:</b> Update related documents together so readers see all-or-nothing.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Typical write transaction lifecycle — batch import with error handling:</para>
/// <code>
/// await using var txn = await db.BeginWriteAsync(TimeSpan.FromSeconds(10));
/// try
/// {
///     foreach (var file in Directory.GetFiles("/data/imports/", "*.xml"))
///     {
///         var content = await File.ReadAllTextAsync(file);
///         var name = $"imports/{Path.GetFileName(file)}";
///         await txn.PutDocumentAsync(container.Id, name, content);
///     }
///
///     await txn.CommitAsync();
///     Console.WriteLine("Import committed successfully.");
/// }
/// catch (Exception ex)
/// {
///     await txn.RollbackAsync();
///     Console.WriteLine($"Import rolled back: {ex.Message}");
/// }
/// </code>
/// </example>
/// <seealso cref="IDocumentDatabase.BeginWriteAsync(CancellationToken)"/>
/// <seealso cref="IReadTransaction"/>
public interface IWriteTransaction : IReadTransaction
{
    /// <summary>
    /// Inserts or updates a document within this transaction.
    /// </summary>
    /// <param name="container">The target container.</param>
    /// <param name="name">Document name (URI-like identifier, case-sensitive).</param>
    /// <param name="content">The XML or JSON content to store.</param>
    /// <param name="options">Optional settings for content type, overwrite behavior, and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The document is buffered in the transaction and only becomes visible to other readers
    /// after <see cref="CommitAsync"/> is called. If the transaction is rolled back, the
    /// document is discarded.
    /// </remarks>
    /// <exception cref="DocumentExistsException">
    /// A document with the same name exists and <see cref="DocumentOptions.Overwrite"/> is <c>false</c>.
    /// </exception>
    /// <exception cref="DocumentParseException">
    /// The content is not well-formed XML or valid JSON.
    /// </exception>
    ValueTask PutDocumentAsync(
        ContainerId container,
        string name,
        string content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document within this transaction.
    /// </summary>
    /// <param name="container">The container holding the document.</param>
    /// <param name="name">The name of the document to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the document existed and was marked for deletion; <c>false</c> if not found.</returns>
    /// <remarks>
    /// The deletion is not visible to other readers until <see cref="CommitAsync"/> is called.
    /// </remarks>
    ValueTask<bool> DeleteDocumentAsync(
        ContainerId container,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a metadata key-value pair on a document within this transaction.
    /// </summary>
    /// <param name="container">The container holding the document.</param>
    /// <param name="documentName">The name of the document to attach metadata to.</param>
    /// <param name="key">The metadata key (case-sensitive). Replaces any existing value for this key.</param>
    /// <param name="value">The metadata value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocumentNotFoundException">
    /// No document with the specified name exists in the container.
    /// </exception>
    ValueTask SetMetadataAsync(
        ContainerId container,
        string documentName,
        string key,
        object value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all changes made within this transaction, making them permanently visible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// After a successful commit, all inserted, updated, and deleted documents become visible
    /// to new read transactions. The write lock is released, allowing other write transactions
    /// to proceed.
    /// </para>
    /// <para>
    /// A commit is durable — once this method returns successfully, the changes survive
    /// process crashes and power failures (subject to LMDB's sync mode).
    /// </para>
    /// </remarks>
    /// <exception cref="TransactionException">
    /// The transaction has already been committed, rolled back, or disposed.
    /// </exception>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all changes made within this transaction, discarding them entirely.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// After rollback, the database is exactly as it was before the transaction began.
    /// The write lock is released, allowing other write transactions to proceed.
    /// </para>
    /// <para>
    /// Disposing a write transaction without committing also rolls back, but calling
    /// <see cref="RollbackAsync"/> explicitly is preferred for code clarity.
    /// </para>
    /// </remarks>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
