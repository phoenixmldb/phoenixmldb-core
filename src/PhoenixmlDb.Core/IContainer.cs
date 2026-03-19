using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// A container holding a collection of documents with shared configuration.
/// </summary>
public interface IContainer
{
    /// <summary>
    /// Container identifier.
    /// </summary>
    ContainerId Id { get; }

    /// <summary>
    /// Container name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Container configuration.
    /// </summary>
    ContainerOptions Options { get; }

    /// <summary>
    /// Stores a document in the container.
    /// </summary>
    /// <param name="name">Document name (URI-like identifier).</param>
    /// <param name="content">XML or JSON content.</param>
    /// <param name="options">Document options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PutDocumentAsync(
        string name,
        string content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a document from a stream.
    /// </summary>
    ValueTask PutDocumentAsync(
        string name,
        Stream content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by name.
    /// </summary>
    /// <param name="name">Document name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, or null if not found.</returns>
    ValueTask<IDocument?> GetDocumentAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document.
    /// </summary>
    /// <returns>True if the document existed and was deleted.</returns>
    ValueTask<bool> DeleteDocumentAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document exists.
    /// </summary>
    ValueTask<bool> DocumentExistsAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents in the container.
    /// </summary>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists documents matching a name prefix.
    /// </summary>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an XQuery query against this container.
    /// </summary>
    /// <param name="query">XQuery expression.</param>
    /// <param name="variables">External variable bindings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<object> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets document metadata.
    /// </summary>
    ValueTask SetMetadataAsync(
        string documentName,
        string key,
        object value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document metadata.
    /// </summary>
    ValueTask<object?> GetMetadataAsync(
        string documentName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata for a document.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, object>> GetAllMetadataAsync(
        string documentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries documents by metadata.
    /// </summary>
    IAsyncEnumerable<DocumentInfo> QueryMetadataAsync(
        string key,
        object value,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Container configuration options.
/// </summary>
public sealed class ContainerOptions
{
    /// <summary>
    /// Index configuration for this container.
    /// </summary>
    public IndexConfiguration Indexes { get; } = new();

    /// <summary>
    /// Default namespace bindings (prefix → URI).
    /// </summary>
    public Dictionary<string, string> DefaultNamespaces { get; } = new();

    /// <summary>
    /// Document validation mode.
    /// </summary>
    public ValidationMode ValidationMode { get; set; } = ValidationMode.None;

    /// <summary>
    /// Whether to preserve whitespace in documents.
    /// </summary>
    public bool PreserveWhitespace { get; set; }
}

/// <summary>
/// Document validation mode.
/// </summary>
public enum ValidationMode
{
    /// <summary>No validation.</summary>
    None,

    /// <summary>Validate against XML Schema if specified.</summary>
    Schema,

    /// <summary>Validate well-formedness only.</summary>
    WellFormed
}

/// <summary>
/// Options for storing a document.
/// </summary>
public record DocumentOptions
{
    /// <summary>
    /// Content type (xml or json). Auto-detected if not specified.
    /// </summary>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Initial metadata to attach to the document.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Whether to overwrite an existing document with the same name.
    /// </summary>
    public bool Overwrite { get; init; } = true;
}

/// <summary>
/// Document content type.
/// </summary>
public enum ContentType
{
    Xml,
    Json
}

/// <summary>
/// Information about a stored document.
/// </summary>
public record DocumentInfo
{
    public required DocumentId Id { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Modified { get; init; }
    public required long SizeBytes { get; init; }
    public required ContentType ContentType { get; init; }
}
