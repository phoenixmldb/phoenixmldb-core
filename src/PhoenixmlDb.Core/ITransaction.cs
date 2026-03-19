using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// A read-only transaction providing snapshot isolation.
/// </summary>
public interface IReadTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The transaction ID (snapshot version).
    /// </summary>
    long TransactionId { get; }

    /// <summary>
    /// Whether the transaction is still active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets a document by name within this transaction's snapshot.
    /// </summary>
    ValueTask<IDocument?> GetDocumentAsync(
        ContainerId container,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries documents within this transaction's snapshot.
    /// </summary>
    IAsyncEnumerable<object> QueryAsync(
        ContainerId container,
        string xquery,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents in a container.
    /// </summary>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        ContainerId container,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A read-write transaction with full ACID guarantees.
/// </summary>
public interface IWriteTransaction : IReadTransaction
{
    /// <summary>
    /// Inserts or updates a document.
    /// </summary>
    ValueTask PutDocumentAsync(
        ContainerId container,
        string name,
        string content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document.
    /// </summary>
    ValueTask<bool> DeleteDocumentAsync(
        ContainerId container,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets document metadata.
    /// </summary>
    ValueTask SetMetadataAsync(
        ContainerId container,
        string documentName,
        string key,
        object value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all changes.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all changes.
    /// </summary>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
