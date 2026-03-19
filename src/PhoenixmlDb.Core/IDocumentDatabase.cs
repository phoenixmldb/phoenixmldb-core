using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// Root entry point for XmlDb database operations.
/// </summary>
public interface IDocumentDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the database path.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Gets database statistics.
    /// </summary>
    DatabaseStatistics Statistics { get; }

    /// <summary>
    /// Creates a new container with the specified configuration.
    /// </summary>
    /// <param name="name">Container name (must be unique).</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created container.</returns>
    ValueTask<IContainer> CreateContainerAsync(
        string name,
        Action<ContainerOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an existing container.
    /// </summary>
    /// <param name="name">Container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The container, or null if not found.</returns>
    ValueTask<IContainer?> OpenContainerAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a container, creating it if it doesn't exist.
    /// </summary>
    ValueTask<IContainer> OpenOrCreateContainerAsync(
        string name,
        Action<ContainerOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a container and all its contents.
    /// </summary>
    ValueTask<bool> DeleteContainerAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all containers.
    /// </summary>
    IAsyncEnumerable<ContainerInfo> ListContainersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a read-only transaction.
    /// </summary>
    IReadTransaction BeginRead();

    /// <summary>
    /// Begins a read-write transaction.
    /// </summary>
    /// <remarks>
    /// Only one write transaction can be active at a time.
    /// Additional calls will block until the current write transaction completes.
    /// </remarks>
    ValueTask<IWriteTransaction> BeginWriteAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a read-write transaction with a timeout.
    /// </summary>
    ValueTask<IWriteTransaction> BeginWriteAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered data to disk.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Database-level statistics.
/// </summary>
public record DatabaseStatistics
{
    public required long DatabaseSizeBytes { get; init; }
    public required long UsedSizeBytes { get; init; }
    public required int ContainerCount { get; init; }
    public required long TotalDocumentCount { get; init; }
    public required long TotalNodeCount { get; init; }
}

/// <summary>
/// Information about a container.
/// </summary>
public record ContainerInfo
{
    public required ContainerId Id { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Modified { get; init; }
    public required long DocumentCount { get; init; }
}
