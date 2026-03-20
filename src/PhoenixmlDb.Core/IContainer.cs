using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// Primary interaction surface for storing, retrieving, querying, and managing documents
/// within a PhoenixmlDb container.
/// </summary>
/// <remarks>
/// <para>
/// <c>IContainer</c> is the interface most application code works with day-to-day.
/// While <see cref="IDocumentDatabase"/> manages containers and transactions at the database level,
/// <c>IContainer</c> is where you put documents, run XQuery queries, and manage metadata.
/// </para>
/// <para>
/// A container holds a collection of documents that share the same index configuration,
/// namespace bindings, and validation rules. Documents can be either XML or JSON — both
/// formats are stored natively, parsed into the XQuery Data Model (XDM), and are fully
/// queryable via XQuery.
/// </para>
/// <para>
/// <b>Document naming:</b> Each document is identified by a name string within its container.
/// Names are URI-like identifiers. Path-style names (e.g., <c>"customers/acme/profile.xml"</c>)
/// are recommended because they enable prefix-based listing via
/// <see cref="ListDocumentsAsync(string, CancellationToken)"/> and create a natural
/// organizational hierarchy. Names are case-sensitive and must be unique within the container.
/// </para>
/// <para>
/// <b>Convenience methods vs. explicit transactions:</b> The methods on <c>IContainer</c>
/// (e.g., <see cref="PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>,
/// <see cref="GetDocumentAsync"/>) each run within an implicit transaction — a write transaction
/// for mutations, a read transaction for queries. This is convenient for single operations, but
/// if you need to perform multiple reads or writes atomically, use
/// <see cref="IDocumentDatabase.BeginWriteAsync(CancellationToken)"/> or
/// <see cref="IDocumentDatabase.BeginRead"/> to create an explicit transaction instead.
/// </para>
/// <para>
/// <b>Basic usage:</b>
/// <code>
/// // Store a document
/// await container.PutDocumentAsync("orders/2024/order-001.xml", orderXml);
///
/// // Retrieve it
/// var doc = await container.GetDocumentAsync("orders/2024/order-001.xml");
/// var content = await doc!.GetContentAsync();
///
/// // Query with XQuery
/// await foreach (var result in container.QueryAsync("//order[total > 100]"))
/// {
///     Console.WriteLine(result);
/// }
///
/// // List documents by prefix
/// await foreach (var info in container.ListDocumentsAsync("orders/2024/"))
/// {
///     Console.WriteLine($"{info.Name} ({info.SizeBytes} bytes)");
/// }
/// </code>
/// </para>
/// </remarks>
public interface IContainer
{
    /// <summary>
    /// Gets the unique identifier for this container within the database.
    /// </summary>
    /// <remarks>
    /// The <see cref="ContainerId"/> is an opaque, database-assigned identifier that remains
    /// stable across the container's lifetime. Use <see cref="Name"/> for human-readable
    /// identification; use <see cref="Id"/> when you need a compact key for internal
    /// bookkeeping or cross-referencing.
    /// </remarks>
    ContainerId Id { get; }

    /// <summary>
    /// Gets the human-readable name of this container.
    /// </summary>
    /// <remarks>
    /// This is the name passed to
    /// <see cref="IDocumentDatabase.CreateContainerAsync"/> or
    /// <see cref="IDocumentDatabase.OpenOrCreateContainerAsync"/>
    /// when the container was created. Container names are unique within a database.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the configuration options for this container, including indexes, namespace
    /// bindings, and validation settings.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="ContainerOptions"/> reflects the configuration established
    /// at container creation time. See <see cref="ContainerOptions.Indexes"/> for the
    /// index definitions that accelerate queries against this container's documents.
    /// </remarks>
    ContainerOptions Options { get; }

    /// <summary>
    /// Stores a document in the container from a string.
    /// </summary>
    /// <param name="name">
    /// Document name (URI-like identifier). Path-style names are recommended
    /// (e.g., <c>"invoices/2024/inv-1042.xml"</c>) to enable prefix-based listing.
    /// Names are case-sensitive and must be unique within the container.
    /// </param>
    /// <param name="content">
    /// The document content as XML or JSON. The content type is auto-detected from the
    /// content itself unless explicitly specified via <paramref name="options"/>.
    /// The content must be well-formed XML or valid JSON.
    /// </param>
    /// <param name="options">
    /// Optional settings controlling content type detection, overwrite behavior, and
    /// initial metadata. See <see cref="DocumentOptions"/> for details.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the document is stored and indexed.</returns>
    /// <exception cref="DocumentExistsException">
    /// Thrown when a document with the same <paramref name="name"/> already exists and
    /// <see cref="DocumentOptions.Overwrite"/> is set to <c>false</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method runs within an implicit write transaction. The document is parsed,
    /// validated (if <see cref="ContainerOptions.ValidationMode"/> is set), stored, and
    /// indexed atomically.
    /// </para>
    /// <para>
    /// By default, <see cref="DocumentOptions.Overwrite"/> is <c>true</c>, so calling this
    /// method with an existing document name silently replaces the previous content. Set
    /// <c>Overwrite = false</c> when you want insert-only semantics (e.g., to prevent
    /// accidental data loss).
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Store an XML document with default options (auto-detect, overwrite enabled):</para>
    /// <code>
    /// await container.PutDocumentAsync("products/widget.xml",
    ///     "&lt;product&gt;&lt;name&gt;Widget&lt;/name&gt;&lt;price&gt;9.99&lt;/price&gt;&lt;/product&gt;");
    /// </code>
    /// <para>Store a JSON document with metadata and insert-only semantics:</para>
    /// <code>
    /// await container.PutDocumentAsync("events/evt-42.json",
    ///     """{"type": "click", "timestamp": "2024-03-15T10:30:00Z"}""",
    ///     new DocumentOptions
    ///     {
    ///         ContentType = ContentType.Json,
    ///         Overwrite = false,
    ///         Metadata = new Dictionary&lt;string, object&gt;
    ///         {
    ///             ["source"] = "web",
    ///             ["priority"] = 1
    ///         }
    ///     });
    /// </code>
    /// </example>
    ValueTask PutDocumentAsync(
        string name,
        string content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a document in the container from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="name">
    /// Document name (URI-like identifier). See
    /// <see cref="PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
    /// for naming conventions.
    /// </param>
    /// <param name="content">
    /// A readable stream containing XML or JSON content. The stream is read to completion
    /// but is not disposed by this method — the caller retains ownership.
    /// </param>
    /// <param name="options">
    /// Optional settings controlling content type detection, overwrite behavior, and
    /// initial metadata. See <see cref="DocumentOptions"/> for details.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the document is stored and indexed.</returns>
    /// <exception cref="DocumentExistsException">
    /// Thrown when a document with the same <paramref name="name"/> already exists and
    /// <see cref="DocumentOptions.Overwrite"/> is set to <c>false</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use this overload when loading documents from files, HTTP responses, or other
    /// stream-based sources to avoid buffering the entire content in memory as a string.
    /// This is especially beneficial for large documents.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var fileStream = File.OpenRead("/data/catalog.xml");
    /// await container.PutDocumentAsync("catalog.xml", fileStream);
    /// </code>
    /// </example>
    ValueTask PutDocumentAsync(
        string name,
        Stream content,
        DocumentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a document by its name.
    /// </summary>
    /// <param name="name">The exact document name to look up (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The <see cref="IDocument"/> if found, or <c>null</c> if no document with the given
    /// name exists in this container.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned <see cref="IDocument"/> provides access to the document's content
    /// (via <see cref="IDocument.GetContentAsync"/> or <see cref="IDocument.GetContentStreamAsync"/>),
    /// its parsed XDM tree (via <see cref="IDocument.GetRootNodeAsync"/>), and its metadata.
    /// </para>
    /// <para>
    /// This method runs within an implicit read transaction. If you need to read multiple
    /// documents with a consistent snapshot, use <see cref="IDocumentDatabase.BeginRead"/>
    /// to create an explicit read transaction.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var doc = await container.GetDocumentAsync("orders/order-001.xml");
    /// if (doc is not null)
    /// {
    ///     var xml = await doc.GetContentAsync();
    ///     Console.WriteLine($"Document {doc.Name}, {doc.SizeBytes} bytes, type: {doc.ContentType}");
    /// }
    /// </code>
    /// </example>
    ValueTask<IDocument?> GetDocumentAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a document from the container.
    /// </summary>
    /// <param name="name">The name of the document to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the document existed and was deleted; <c>false</c> if no document
    /// with the given name was found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Deletion removes the document content, its XDM node tree, all associated index entries,
    /// and all metadata. This operation is irreversible.
    /// </para>
    /// <para>
    /// This method runs within an implicit write transaction. The return value lets you
    /// distinguish between "deleted successfully" and "nothing to delete" without needing
    /// a separate <see cref="DocumentExistsAsync"/> call.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// bool wasDeleted = await container.DeleteDocumentAsync("obsolete/old-report.xml");
    /// if (!wasDeleted)
    ///     Console.WriteLine("Document was already gone.");
    /// </code>
    /// </example>
    ValueTask<bool> DeleteDocumentAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a document with the specified name exists in this container.
    /// </summary>
    /// <param name="name">The document name to check (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the document exists; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// <para>
    /// This is a lightweight existence check that does not load the document content.
    /// Use it for guard checks before operations where you want to handle the exists/not-exists
    /// cases differently.
    /// </para>
    /// <para>
    /// Note that in concurrent scenarios, the document could be created or deleted between
    /// this check and a subsequent operation. For atomic "create if not exists" behavior,
    /// use <see cref="PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
    /// with <c>Overwrite = false</c> and catch <see cref="DocumentExistsException"/>.
    /// </para>
    /// </remarks>
    ValueTask<bool> DocumentExistsAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents in the container.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="DocumentInfo"/> records, one per
    /// document. The enumeration order is implementation-defined.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each <see cref="DocumentInfo"/> contains lightweight metadata (name, size, timestamps,
    /// content type) without loading the full document content. This is efficient for building
    /// document inventories, dashboards, or migration scripts.
    /// </para>
    /// <para>
    /// For large containers, consider using the prefix-based overload
    /// <see cref="ListDocumentsAsync(string, CancellationToken)"/> to narrow results.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await foreach (var info in container.ListDocumentsAsync())
    /// {
    ///     Console.WriteLine($"{info.Name} — {info.ContentType}, {info.SizeBytes} bytes");
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists documents whose names start with the specified prefix.
    /// </summary>
    /// <param name="prefix">
    /// The name prefix to filter by. For example, <c>"orders/2024/"</c> returns all
    /// documents whose names begin with that string. The match is case-sensitive.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="DocumentInfo"/> records for
    /// documents matching the prefix.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is why path-style document names are recommended — they enable efficient
    /// hierarchical browsing. For example, with documents named
    /// <c>"orders/2024/q1/inv-001.xml"</c>, <c>"orders/2024/q2/inv-042.xml"</c>, etc.,
    /// you can list all 2024 orders with prefix <c>"orders/2024/"</c> or just Q1 with
    /// <c>"orders/2024/q1/"</c>.
    /// </para>
    /// <para>
    /// The prefix match is performed on the stored name index and is efficient even for
    /// containers with many documents.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // List all configuration documents
    /// await foreach (var info in container.ListDocumentsAsync("config/"))
    /// {
    ///     Console.WriteLine(info.Name);
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<DocumentInfo> ListDocumentsAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an XQuery expression against the documents in this container.
    /// </summary>
    /// <param name="query">
    /// An XQuery expression. The expression runs in the context of this container's
    /// document collection. Use <c>collection()</c> to access all documents, or
    /// <c>doc("name")</c> to access a specific document by name.
    /// </param>
    /// <param name="variables">
    /// Optional external variable bindings. Keys are variable names (without the <c>$</c>
    /// prefix); values are the variable values. These can be referenced in the query as
    /// <c>$variableName</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> yielding each item in the XQuery result sequence.
    /// Results may be XDM nodes, atomic values, or other XQuery items depending on the query.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Queries benefit from the indexes configured in <see cref="ContainerOptions.Indexes"/>.
    /// For example, a path index on <c>"//product/price"</c> accelerates queries like
    /// <c>//product[price &gt; 50]</c>. Without appropriate indexes, queries perform
    /// full document scans.
    /// </para>
    /// <para>
    /// The default namespace bindings from <see cref="ContainerOptions.DefaultNamespaces"/>
    /// are automatically available in query expressions, so you don't need to redeclare them.
    /// </para>
    /// <para>
    /// External variables are useful for parameterizing queries safely, avoiding
    /// string concatenation that could lead to injection issues.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Simple query across all documents:</para>
    /// <code>
    /// await foreach (var result in container.QueryAsync("collection()//product[price &gt; 100]"))
    /// {
    ///     Console.WriteLine(result);
    /// }
    /// </code>
    /// <para>Parameterized query with external variables:</para>
    /// <code>
    /// var variables = new Dictionary&lt;string, object&gt;
    /// {
    ///     ["minPrice"] = 50.0m,
    ///     ["category"] = "electronics"
    /// };
    /// await foreach (var result in container.QueryAsync(
    ///     """
    ///     for $p in collection()//product
    ///     where $p/price &gt; $minPrice and $p/category = $category
    ///     order by $p/price descending
    ///     return $p/name
    ///     """,
    ///     variables))
    /// {
    ///     Console.WriteLine(result);
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<object> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a single metadata key-value pair on a document.
    /// </summary>
    /// <param name="documentName">The name of the document to attach metadata to.</param>
    /// <param name="key">
    /// The metadata key. Keys are case-sensitive strings. If the key already exists,
    /// its value is replaced.
    /// </param>
    /// <param name="value">
    /// The metadata value. Supported types include strings, numbers, booleans, and dates.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// Metadata is a set of key-value pairs attached to a document, separate from the
    /// document's content. Use metadata for classification, tagging, workflow state, or
    /// any application-level attributes that you want to query without parsing document content.
    /// </para>
    /// <para>
    /// Metadata can also be set at document creation time via
    /// <see cref="DocumentOptions.Metadata"/>. Use this method to add or update metadata
    /// after the document has been stored.
    /// </para>
    /// <para>
    /// To make metadata queryable with <see cref="QueryMetadataAsync"/>, add a metadata
    /// index to the container's <see cref="IndexConfiguration"/> via
    /// <see cref="IndexConfiguration.AddMetadataIndex"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await container.SetMetadataAsync("reports/q1.xml", "status", "approved");
    /// await container.SetMetadataAsync("reports/q1.xml", "reviewer", "Jane Smith");
    /// await container.SetMetadataAsync("reports/q1.xml", "reviewDate", DateTimeOffset.UtcNow);
    /// </code>
    /// </example>
    ValueTask SetMetadataAsync(
        string documentName,
        string key,
        object value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single metadata value for a document by key.
    /// </summary>
    /// <param name="documentName">The name of the document.</param>
    /// <param name="key">The metadata key to retrieve (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The metadata value if the key exists, or <c>null</c> if the key is not set on
    /// this document.
    /// </returns>
    /// <remarks>
    /// To retrieve all metadata for a document at once, use
    /// <see cref="GetAllMetadataAsync"/> instead. Metadata can also be read through the
    /// <see cref="IDocument"/> interface via <see cref="IDocument.GetMetadataAsync"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var status = await container.GetMetadataAsync("reports/q1.xml", "status");
    /// if (status is string s)
    ///     Console.WriteLine($"Report status: {s}");
    /// </code>
    /// </example>
    ValueTask<object?> GetMetadataAsync(
        string documentName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata key-value pairs for a document.
    /// </summary>
    /// <param name="documentName">The name of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only dictionary containing all metadata entries for the document. Returns an
    /// empty dictionary if the document has no metadata.
    /// </returns>
    /// <remarks>
    /// This is useful when you need to inspect or display all metadata at once, for example
    /// in an admin UI or document detail view. For retrieving a single known key,
    /// <see cref="GetMetadataAsync"/> is more direct.
    /// </remarks>
    /// <example>
    /// <code>
    /// var allMeta = await container.GetAllMetadataAsync("reports/q1.xml");
    /// foreach (var (key, value) in allMeta)
    /// {
    ///     Console.WriteLine($"  {key} = {value}");
    /// }
    /// </code>
    /// </example>
    ValueTask<IReadOnlyDictionary<string, object>> GetAllMetadataAsync(
        string documentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds documents that have a specific metadata key-value pair.
    /// </summary>
    /// <param name="key">The metadata key to match on.</param>
    /// <param name="value">The metadata value to match. Equality comparison is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="DocumentInfo"/> records for all
    /// documents whose metadata contains the specified key with a matching value.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is most efficient when a metadata index has been configured for the
    /// given <paramref name="key"/> via <see cref="IndexConfiguration.AddMetadataIndex"/>.
    /// Without an index, the query requires scanning all documents' metadata.
    /// </para>
    /// <para>
    /// Common use cases include finding documents by status, author, category, or any
    /// other application-defined classification.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Find all approved reports (assumes a metadata index on "status")
    /// await foreach (var info in container.QueryMetadataAsync("status", "approved"))
    /// {
    ///     Console.WriteLine($"Approved: {info.Name}");
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<DocumentInfo> QueryMetadataAsync(
        string key,
        object value,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for an <see cref="IContainer"/>, controlling indexing, namespace
/// bindings, validation, and whitespace handling.
/// </summary>
/// <remarks>
/// <para>
/// <c>ContainerOptions</c> is passed to
/// <see cref="IDocumentDatabase.CreateContainerAsync"/> or
/// <see cref="IDocumentDatabase.OpenOrCreateContainerAsync"/> via a configuration action.
/// The options are applied at container creation time and govern how documents are stored,
/// validated, and indexed.
/// </para>
/// <para>
/// The most important property is <see cref="Indexes"/>, which provides a fluent API
/// for defining the indexes that accelerate XQuery queries. Indexes should be configured
/// before documents are added — existing documents are not retroactively indexed when
/// new index definitions are added later.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var container = await db.CreateContainerAsync("products", opts =>
/// {
///     // Index product IDs for fast lookup
///     opts.Indexes
///         .AddPathIndex("//product/@id")
///         .AddValueIndex("//product/price", XdmValueType.XdmDecimal)
///         .AddFullTextIndex("//product/description")
///         .AddMetadataIndex("category", XdmValueType.XdmString);
///
///     // Register namespaces so queries don't need to declare them
///     opts.DefaultNamespaces.Add("p", "http://example.com/products");
///
///     // Validate all incoming documents
///     opts.ValidationMode = ValidationMode.WellFormed;
/// });
/// </code>
/// </example>
public sealed class ContainerOptions
{
    /// <summary>
    /// Gets the index configuration for this container, providing a fluent API for
    /// defining path, value, full-text, name, and metadata indexes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Indexes are critical for query performance. Without indexes, XQuery expressions
    /// require full document scans. The <see cref="IndexConfiguration"/> class provides
    /// a fluent builder API — each <c>Add*Index</c> method returns the configuration
    /// instance for chaining.
    /// </para>
    /// <para>
    /// Configure indexes before inserting documents. See <see cref="IndexConfiguration"/>
    /// for the full set of available index types.
    /// </para>
    /// </remarks>
    public IndexConfiguration Indexes { get; } = new();

    /// <summary>
    /// Gets the default namespace prefix-to-URI bindings for this container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Namespaces registered here are automatically available in XQuery expressions
    /// executed via <see cref="IContainer.QueryAsync"/>. This avoids repeating
    /// <c>declare namespace</c> prologues in every query.
    /// </para>
    /// <para>
    /// Keys are namespace prefixes (e.g., <c>"p"</c>); values are namespace URIs
    /// (e.g., <c>"http://example.com/products"</c>).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// opts.DefaultNamespaces.Add("inv", "http://example.com/invoices");
    /// // Now queries can use: //inv:invoice/inv:total
    /// </code>
    /// </example>
    public Dictionary<string, string> DefaultNamespaces { get; } = new();

    /// <summary>
    /// Gets or sets the validation mode applied to documents when they are stored.
    /// Defaults to <see cref="Core.ValidationMode.None"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to <see cref="Core.ValidationMode.WellFormed"/>, documents are checked
    /// for well-formedness (valid XML structure or valid JSON syntax) on insert.
    /// When set to <see cref="Core.ValidationMode.Schema"/>, documents are additionally
    /// validated against an XML Schema if one is associated with the container.
    /// </para>
    /// <para>
    /// Validation adds overhead to write operations. Use <see cref="Core.ValidationMode.None"/>
    /// (the default) when you trust the document sources or validate externally.
    /// </para>
    /// </remarks>
    public ValidationMode ValidationMode { get; set; } = ValidationMode.None;

    /// <summary>
    /// Gets or sets whether to preserve insignificant whitespace in XML documents.
    /// Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>false</c> (the default), insignificant whitespace (e.g., indentation between
    /// elements) may be normalized during storage, reducing document size. When <c>true</c>,
    /// all whitespace is preserved exactly as provided, which is important for documents
    /// where formatting is semantically meaningful (e.g., source code, pre-formatted text).
    /// </para>
    /// <para>
    /// This setting only affects XML documents. JSON whitespace handling follows standard
    /// JSON parsing rules regardless of this setting.
    /// </para>
    /// </remarks>
    public bool PreserveWhitespace { get; set; }
}

/// <summary>
/// Specifies how documents should be validated when stored in a container.
/// </summary>
/// <remarks>
/// Set via <see cref="ContainerOptions.ValidationMode"/>. Validation is applied during
/// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
/// and its stream-based overload.
/// </remarks>
/// <seealso cref="ContainerOptions.ValidationMode"/>
public enum ValidationMode
{
    /// <summary>
    /// No validation is performed. Documents are assumed to be well-formed.
    /// This is the default and offers the best write performance.
    /// </summary>
    None,

    /// <summary>
    /// Validates documents against an XML Schema (XSD) if one is associated with the container.
    /// Schema validation catches structural and type errors but adds overhead to writes.
    /// </summary>
    Schema,

    /// <summary>
    /// Validates that documents are well-formed XML or valid JSON, without schema validation.
    /// This is a lightweight check that catches syntax errors without the cost of full
    /// schema validation.
    /// </summary>
    WellFormed
}

/// <summary>
/// Options controlling how a document is stored when calling
/// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// <para>
/// All properties have sensible defaults: content type is auto-detected, overwrite is enabled,
/// and no initial metadata is set. You only need to create a <c>DocumentOptions</c> instance
/// when you want to override one of these defaults.
/// </para>
/// <para>
/// <b>Common scenarios requiring explicit options:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Insert-only semantics:</b> Set <see cref="Overwrite"/> to <c>false</c> to get a
/// <see cref="DocumentExistsException"/> if the document name is already taken.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Ambiguous content:</b> Set <see cref="ContentType"/> explicitly when the auto-detection
/// might misidentify the format (e.g., a JSON document whose first non-whitespace character
/// could be interpreted as XML).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Initial metadata:</b> Set <see cref="Metadata"/> to attach key-value pairs at creation
/// time, rather than making a separate <see cref="IContainer.SetMetadataAsync"/> call.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new DocumentOptions
/// {
///     ContentType = ContentType.Xml,
///     Overwrite = false,
///     Metadata = new Dictionary&lt;string, object&gt;
///     {
///         ["author"] = "Jane Smith",
///         ["department"] = "Engineering"
///     }
/// };
/// await container.PutDocumentAsync("reports/q1-2024.xml", xmlContent, options);
/// </code>
/// </example>
public record DocumentOptions
{
    /// <summary>
    /// Gets the content type (XML or JSON) for the document. When <c>null</c> (the default),
    /// the content type is auto-detected from the document content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Auto-detection inspects the first non-whitespace character of the content: <c>&lt;</c>
    /// indicates XML, <c>{</c> or <c>[</c> indicates JSON. This works reliably for the
    /// vast majority of documents. Set this property explicitly only when you need to
    /// override auto-detection or want to be defensive about content format.
    /// </para>
    /// </remarks>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the initial metadata to attach to the document at creation time.
    /// When <c>null</c> (the default), no metadata is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting metadata at creation time is atomic with the document insert — either both
    /// the document and its metadata are stored, or neither is. This is more efficient and
    /// safer than storing the document first and then calling
    /// <see cref="IContainer.SetMetadataAsync"/> separately.
    /// </para>
    /// <para>
    /// Additional metadata can be added or updated later via
    /// <see cref="IContainer.SetMetadataAsync"/>. Metadata set here does not prevent
    /// later modifications.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets whether to overwrite an existing document with the same name.
    /// Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c> (the default), storing a document with a name that already exists
    /// silently replaces the previous content, metadata, and index entries. This is the
    /// typical "upsert" behavior.
    /// </para>
    /// <para>
    /// When <c>false</c>, a <see cref="DocumentExistsException"/> is thrown if a document
    /// with the same name already exists. Use this for insert-only workflows where
    /// accidental overwrites must be prevented, such as event sourcing or audit logging.
    /// </para>
    /// </remarks>
    public bool Overwrite { get; init; } = true;
}

/// <summary>
/// Specifies the content format of a stored document.
/// </summary>
/// <remarks>
/// Both XML and JSON documents are first-class citizens in PhoenixmlDb. Both are parsed into
/// the XQuery Data Model (XDM) and can be queried with XQuery expressions via
/// <see cref="IContainer.QueryAsync"/>. The content type is typically auto-detected
/// (see <see cref="DocumentOptions.ContentType"/>) but can be set explicitly.
/// </remarks>
public enum ContentType
{
    /// <summary>
    /// XML content, parsed according to XML 1.0 rules.
    /// </summary>
    Xml,

    /// <summary>
    /// JSON content, parsed according to RFC 8259 and mapped to the XDM via the
    /// XQuery 3.1 JSON representation.
    /// </summary>
    Json
}

/// <summary>
/// Lightweight summary information about a stored document, returned by
/// <see cref="IContainer.ListDocumentsAsync(CancellationToken)"/> and
/// <see cref="IContainer.QueryMetadataAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>DocumentInfo</c> provides document metadata without loading the full document content.
/// This makes it efficient for listing, filtering, and displaying document inventories.
/// To access the actual content, use <see cref="IContainer.GetDocumentAsync"/> to obtain
/// the full <see cref="IDocument"/>.
/// </para>
/// </remarks>
public record DocumentInfo
{
    /// <summary>Gets the database-assigned unique identifier for this document.</summary>
    public required DocumentId Id { get; init; }

    /// <summary>Gets the document name (the URI-like identifier passed to
    /// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the timestamp when the document was first stored in the container.</summary>
    public required DateTimeOffset Created { get; init; }

    /// <summary>Gets the timestamp of the most recent update to this document's content.</summary>
    public required DateTimeOffset Modified { get; init; }

    /// <summary>Gets the size of the stored document content in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Gets the content type (XML or JSON) of the document.</summary>
    public required ContentType ContentType { get; init; }
}
