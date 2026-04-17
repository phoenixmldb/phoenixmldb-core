using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// Represents a stored document retrieved from a container, providing access to its
/// content, parsed XDM node tree, and application-defined metadata.
/// </summary>
/// <remarks>
/// <para>
/// An <c>IDocument</c> is an immutable snapshot of a document at the time it was retrieved.
/// It does not track changes — if you modify the content, you must call
/// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
/// again to persist the updated version.
/// </para>
/// <para>
/// <b>Accessing content:</b> Document content can be consumed in three ways, depending
/// on your use case:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="GetContentAsync"/> — returns the raw XML or JSON as a <see cref="string"/>.
/// Best for serialization, logging, or passing to external systems.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="GetContentStreamAsync"/> — returns a <see cref="Stream"/> for large documents
/// where you want to avoid allocating the entire content as a single string.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="GetRootNodeAsync"/> — returns the parsed XDM node tree (<see cref="IXdmNode"/>).
/// This is the representation that XQuery operates on. Use it for programmatic tree walking
/// or when you need structured access to the document's elements and attributes.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Metadata:</b> Each document can carry arbitrary key-value metadata that is stored
/// separately from the document content. Metadata is useful for classification, tagging,
/// workflow state, or application-specific attributes. Metadata can be set at document
/// creation time via <see cref="DocumentOptions.Metadata"/> or added later with
/// <see cref="IContainer.SetMetadataAsync"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Retrieve and inspect a document:</para>
/// <code>
/// var doc = await container.GetDocumentAsync("orders/order-001.xml");
/// if (doc is not null)
/// {
///     // Access raw content
///     string xml = await doc.GetContentAsync();
///
///     // Access parsed XDM tree
///     var root = await doc.GetRootNodeAsync();
///     Console.WriteLine($"Root element: {root.NodeName}");
///
///     // Read metadata
///     var status = await doc.GetMetadataAsync("status");
///     Console.WriteLine($"Document {doc.Name}, status={status}, size={doc.SizeBytes} bytes");
/// }
/// </code>
/// </example>
/// <seealso cref="IContainer.GetDocumentAsync"/>
/// <seealso cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
public interface IDocument
{
    /// <summary>
    /// Gets the database-assigned unique identifier for this document.
    /// </summary>
    /// <remarks>
    /// The <see cref="DocumentId"/> is assigned when the document is first stored and remains
    /// stable across updates. Use <see cref="Name"/> for human-readable identification; use
    /// <see cref="Id"/> when you need a compact, opaque key for internal bookkeeping.
    /// </remarks>
    DocumentId Id { get; }

    /// <summary>
    /// Gets the URI-like name that identifies this document within its container.
    /// </summary>
    /// <remarks>
    /// This is the name passed to
    /// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
    /// when the document was stored. Names are case-sensitive and unique within a container.
    /// Path-style names (e.g., <c>"orders/2024/order-001.xml"</c>) are conventional.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the identifier of the container that holds this document.
    /// </summary>
    /// <remarks>
    /// Use this to correlate a document back to its parent container when working
    /// with documents from multiple containers.
    /// </remarks>
    ContainerId Container { get; }

    /// <summary>
    /// Gets the timestamp when this document was first stored in the container.
    /// </summary>
    DateTimeOffset Created { get; }

    /// <summary>
    /// Gets the timestamp of the most recent update to this document's content.
    /// </summary>
    /// <remarks>
    /// Updated each time the document content is replaced via
    /// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>.
    /// Metadata changes do not update this timestamp.
    /// </remarks>
    DateTimeOffset Modified { get; }

    /// <summary>
    /// Gets the size of the stored document content in bytes.
    /// </summary>
    /// <remarks>
    /// This reflects the serialized content size, not the in-memory XDM tree size.
    /// Useful for monitoring storage consumption and enforcing size limits.
    /// </remarks>
    long SizeBytes { get; }

    /// <summary>
    /// Gets the content type (XML or JSON) of the document.
    /// </summary>
    /// <remarks>
    /// The content type is determined at storage time — either auto-detected from the content
    /// or explicitly set via <see cref="DocumentOptions.ContentType"/>.
    /// </remarks>
    ContentType ContentType { get; }

    /// <summary>
    /// Gets the document content as a string.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw XML or JSON content of the document.</returns>
    /// <remarks>
    /// This allocates the entire document content as a single string. For large documents,
    /// consider <see cref="GetContentStreamAsync"/> to reduce memory pressure. For structured
    /// access to the document's elements, use <see cref="GetRootNodeAsync"/> instead.
    /// </remarks>
    ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document content as a readable stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Stream"/> positioned at the beginning of the document content.</returns>
    /// <remarks>
    /// <para>
    /// Prefer this overload over <see cref="GetContentAsync"/> when the document is large
    /// and you want to process it incrementally (e.g., writing to an HTTP response, piping
    /// to an XML reader, or copying to a file).
    /// </para>
    /// <para>
    /// The caller is responsible for disposing the returned stream.
    /// </para>
    /// </remarks>
    ValueTask<Stream> GetContentStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the root node of the document's XDM (XQuery Data Model) tree.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The root <see cref="IXdmNode"/> of the document tree. For XML documents, this is a
    /// <see cref="XdmNodeKind.Document"/> node whose first child is the root element.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The XDM node tree is the parsed, in-memory representation of the document that the
    /// XQuery engine operates on. It provides structured access to elements, attributes,
    /// text nodes, and other XDM node types.
    /// </para>
    /// <para>
    /// Use this method when you need to walk the document tree programmatically rather than
    /// using XQuery. For most query use cases, prefer
    /// <see cref="IContainer.QueryAsync"/> which operates on XDM trees internally.
    /// </para>
    /// </remarks>
    ValueTask<IXdmNode> GetRootNodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single metadata value by key.
    /// </summary>
    /// <param name="key">The metadata key to retrieve (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata value if the key exists, or <c>null</c> if the key is not set.</returns>
    /// <remarks>
    /// Metadata is separate from document content — it consists of application-defined
    /// key-value pairs. To retrieve all metadata at once, use <see cref="GetAllMetadataAsync"/>.
    /// </remarks>
    ValueTask<object?> GetMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata key-value pairs for this document.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only dictionary of all metadata entries. Returns an empty dictionary if the
    /// document has no metadata.
    /// </returns>
    /// <remarks>
    /// Use this when you need to inspect or display all metadata at once. For a single
    /// known key, <see cref="GetMetadataAsync"/> is more direct.
    /// </remarks>
    ValueTask<IReadOnlyDictionary<string, object>> GetAllMetadataAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a node in the XQuery Data Model (XDM) tree — the parsed, structured
/// representation of a stored XML or JSON document.
/// </summary>
/// <remarks>
/// <para>
/// The XDM is the data model defined by the W3C XQuery and XPath specifications. Every
/// XML document is parsed into a tree of <c>IXdmNode</c> instances: a
/// <see cref="XdmNodeKind.Document"/> node at the root, containing an
/// <see cref="XdmNodeKind.Element"/> node for the root element, which in turn contains
/// child elements, <see cref="XdmNodeKind.Attribute"/> nodes, <see cref="XdmNodeKind.Text"/>
/// nodes, and so on.
/// </para>
/// <para>
/// The XDM tree is what the XQuery engine operates on internally. While most application
/// code should use <see cref="IContainer.QueryAsync"/> to query documents with XQuery,
/// <c>IXdmNode</c> is available for programmatic tree walking when you need to traverse
/// the document structure in C# code.
/// </para>
/// <para>
/// <b>Key properties:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="StringValue"/> — the text content of the node. For elements, this is the
/// concatenation of all descendant text nodes. For text nodes, it is the text itself.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="NodeName"/> — the qualified name (<see cref="QName"/>) for elements,
/// attributes, and processing instructions. Is <c>null</c> for document, text, and
/// comment nodes.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <para>Walk the XDM tree of a document:</para>
/// <code>
/// var doc = await container.GetDocumentAsync("catalog.xml");
/// var root = await doc!.GetRootNodeAsync();
///
/// // root is the document node; its string value is all text content
/// Console.WriteLine($"Node kind: {root.NodeKind}");   // Document
/// Console.WriteLine($"Text content: {root.StringValue}");
/// </code>
/// </example>
/// <seealso cref="IDocument.GetRootNodeAsync"/>
/// <seealso cref="XdmNodeKind"/>
public interface IXdmNode
{
    /// <summary>
    /// Gets the database-assigned unique identifier for this node.
    /// </summary>
    /// <remarks>
    /// Node IDs are unique within a document and stable across read transactions for the
    /// same document version. They are primarily used for internal indexing and node identity
    /// comparisons (the XPath <c>is</c> operator).
    /// </remarks>
    NodeId Id { get; }

    /// <summary>
    /// Gets the kind of this node (document, element, attribute, text, comment, etc.).
    /// </summary>
    /// <remarks>
    /// Use this property to determine what operations are valid on the node. For example,
    /// only <see cref="XdmNodeKind.Element"/> and <see cref="XdmNodeKind.Attribute"/> nodes
    /// have a <see cref="NodeName"/>; text and comment nodes do not.
    /// </remarks>
    XdmNodeKind NodeKind { get; }

    /// <summary>
    /// Gets the parent node, or <c>null</c> if this is the root document node.
    /// </summary>
    /// <remarks>
    /// Every node except the root <see cref="XdmNodeKind.Document"/> node has a parent.
    /// Attribute nodes report their owning element as the parent, consistent with the
    /// XDM specification.
    /// </remarks>
    IXdmNode? Parent { get; }

    /// <summary>
    /// Gets the string value of this node, as defined by the XDM specification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The string value depends on the node kind:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Document/Element:</b> concatenation of all descendant text nodes.</description></item>
    /// <item><description><b>Attribute:</b> the attribute's value.</description></item>
    /// <item><description><b>Text:</b> the text content itself.</description></item>
    /// <item><description><b>Comment:</b> the comment text (without <c>&lt;!--</c> delimiters).</description></item>
    /// <item><description><b>Processing instruction:</b> the PI's data (content after the target name).</description></item>
    /// </list>
    /// <para>
    /// This matches the semantics of the XPath <c>string()</c> function.
    /// </para>
    /// </remarks>
    string StringValue { get; }

    /// <summary>
    /// Gets the qualified name of this node, or <c>null</c> for node kinds that do not have names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Elements, attributes, and processing instructions have qualified names. Document,
    /// text, comment, and namespace nodes return <c>null</c>.
    /// </para>
    /// <para>
    /// The <see cref="QName"/> includes the namespace URI, local name, and an optional prefix.
    /// Two nodes with the same namespace URI and local name are considered to have the same
    /// name, regardless of prefix — the prefix is purely a serialization convenience.
    /// </para>
    /// </remarks>
    /// <seealso cref="QName"/>
    QName? NodeName { get; }
}

/// <summary>
/// Enumerates the seven node kinds defined by the XQuery Data Model (XDM) specification,
/// plus <see cref="None"/> as a sentinel value.
/// </summary>
/// <remarks>
/// <para>
/// These correspond to the node kinds in the W3C XQuery and XPath Data Model 3.1 specification.
/// Each kind has different rules for what properties are available (e.g., only
/// <see cref="Element"/> and <see cref="Attribute"/> nodes have a <see cref="IXdmNode.NodeName"/>).
/// </para>
/// </remarks>
/// <seealso cref="IXdmNode.NodeKind"/>
#pragma warning disable CA1028
public enum XdmNodeKind : byte
{
    /// <summary>No node kind (sentinel/default value).</summary>
    None = 0,
    /// <summary>The root node of a document tree. Contains exactly one element child (the document element).</summary>
    Document = 1,
    /// <summary>An XML element, which may have attributes, child elements, and text content.</summary>
    Element = 2,
    /// <summary>An attribute of an element, consisting of a name and a string value.</summary>
    Attribute = 3,
    /// <summary>A text node containing character data.</summary>
    Text = 4,
    /// <summary>An XML comment (<c>&lt;!-- ... --&gt;</c>).</summary>
    Comment = 5,
    /// <summary>An XML processing instruction (<c>&lt;?target data?&gt;</c>).</summary>
    ProcessingInstruction = 6,
    /// <summary>A namespace binding on an element. Rarely accessed directly in application code.</summary>
    Namespace = 7
}

/// <summary>
/// Represents a qualified name (QName) consisting of a namespace, a local name, and an
/// optional prefix — the XDM representation of element and attribute names.
/// </summary>
/// <remarks>
/// <para>
/// In XML, a qualified name identifies an element or attribute within a namespace. For example,
/// in <c>&lt;p:product xmlns:p="http://example.com/products"&gt;</c>, the QName has namespace
/// URI <c>http://example.com/products</c>, local name <c>product</c>, and prefix <c>p</c>.
/// </para>
/// <para>
/// <b>Identity semantics:</b> Two QNames are equal if and only if they share the same
/// <see cref="Namespace"/> and <see cref="LocalName"/>. The <see cref="Prefix"/> is
/// <em>not</em> part of the identity — it exists purely for serialization and display
/// purposes. This matches the XML Namespaces specification.
/// </para>
/// <para>
/// <b>EQName syntax:</b> The <see cref="ExpandedNamespace"/> property supports the
/// EQName notation <c>Q{namespace-uri}local-name</c>, which is the unambiguous form
/// that does not rely on prefix bindings. When <see cref="ExpandedNamespace"/> is set,
/// <see cref="ToString"/> produces the EQName form.
/// </para>
/// <para>
/// <b>Common use cases:</b> QNames appear when inspecting <see cref="IXdmNode.NodeName"/>,
/// when setting XSLT initial template names or modes by qualified name, and when working
/// with XPath/XQuery name tests programmatically.
/// </para>
/// </remarks>
/// <example>
/// <para>Create and inspect a QName:</para>
/// <code>
/// var name = new QName(NamespaceId.None, "product");
/// Console.WriteLine(name.LocalName);    // "product"
/// Console.WriteLine(name.PrefixedName); // "product"
///
/// var nsName = new QName(NamespaceId.Xsd, "string", "xs");
/// Console.WriteLine(nsName.PrefixedName); // "xs:string"
/// </code>
/// </example>
/// <seealso cref="IXdmNode.NodeName"/>
/// <seealso cref="NamespaceId"/>
public readonly record struct QName : IEquatable<QName>
{
    /// <summary>Gets the interned namespace identifier for this QName.</summary>
    public NamespaceId Namespace { get; }
    /// <summary>Gets the local (unqualified) part of the name.</summary>
    public string LocalName { get; }
    /// <summary>Gets the optional namespace prefix, used for display only. Not part of QName identity.</summary>
    public string? Prefix { get; }
    /// <summary>
    /// When non-null, holds the namespace URI from EQName syntax (Q{uri}local).
    /// Takes precedence over NamespaceId for namespace resolution.
    /// Affects ToString() output (uses Q{uri}local format when prefix is absent).
    /// </summary>
    public string? ExpandedNamespace { get; init; }

    /// <summary>
    /// When non-null, holds the resolved namespace for runtime-created QNames
    /// (e.g., from fn:QName() or xs:QName()). Does NOT affect ToString() output.
    /// Used by namespace-uri-from-QName() and QName eq comparison.
    /// </summary>
    public string? RuntimeNamespace { get; init; }

    /// <summary>
    /// Creates a new QName with the specified namespace, local name, and optional prefix.
    /// </summary>
    /// <param name="ns">The interned namespace identifier. Use <see cref="NamespaceId.None"/> for names with no namespace.</param>
    /// <param name="localName">The local (unqualified) part of the name. Must not be <c>null</c>.</param>
    /// <param name="prefix">Optional namespace prefix for display purposes. Does not affect identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="localName"/> is <c>null</c>.</exception>
    public QName(NamespaceId ns, string localName, string? prefix = null)
    {
        Namespace = ns;
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
        Prefix = prefix;
        ExpandedNamespace = null;
        RuntimeNamespace = null;
    }

    /// <summary>
    /// Gets the display form of the name: <c>prefix:localName</c> if a prefix is present,
    /// or just <c>localName</c> otherwise.
    /// </summary>
    public string PrefixedName => string.IsNullOrEmpty(Prefix) ? LocalName : $"{Prefix}:{LocalName}";

    /// <summary>
    /// Gets the resolved namespace name, preferring ExpandedNamespace, then ResolvedNamespaceUri.
    /// Returns null if neither is available.
    /// </summary>
    public string? ResolvedNamespace => ExpandedNamespace ?? RuntimeNamespace;

    // QName equality is defined by namespace URI + local name per XPath/XSLT spec.
    // The prefix is NOT part of the identity.
    public bool Equals(QName other) => Namespace == other.Namespace && LocalName == other.LocalName;

    public override int GetHashCode() => HashCode.Combine(Namespace, LocalName);

    public override string ToString() => ExpandedNamespace != null ? $"Q{{{ExpandedNamespace}}}{LocalName}" : PrefixedName;
}
