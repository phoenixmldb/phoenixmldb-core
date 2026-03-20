using System.Collections.Generic;
using System.Collections.Immutable;
using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML document root node in the XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// The document node is the root of every XDM document tree. Per the XDM specification,
/// a document node's children can only be element, comment, and processing instruction nodes
/// — text nodes at the document level are not permitted. Typically, a well-formed XML document
/// has exactly one element child (the document element), plus optional comments and PIs.
/// </para>
/// <para>
/// <b>Document URI:</b> The <see cref="DocumentUri"/> identifies the document and is used
/// by functions like <c>fn:document-uri()</c> and <c>fn:doc()</c>. For documents loaded from
/// the database, this is typically the storage URI; for parsed strings, it can be explicitly set.
/// </para>
/// <para>
/// <b>Base URI:</b> The document node's <see cref="XdmNode.BaseUri"/> defaults to the
/// <see cref="DocumentUri"/> when not explicitly set, providing the root base URI from which
/// all descendant nodes inherit.
/// </para>
/// </remarks>
/// <example>
/// Accessing the document element:
/// <code>
/// XdmDocument doc = parseResult.Document;
/// if (doc.DocumentElement is NodeId elemId)
/// {
///     var rootElement = (XdmElement)resolveNode(elemId);
///     Console.WriteLine($"Root element: {rootElement.LocalName}");
/// }
/// </code>
/// </example>
public sealed class XdmDocument : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Document;

    /// <summary>
    /// The document URI (<c>dm:document-uri</c>), which uniquely identifies this document.
    /// </summary>
    /// <remarks>
    /// This corresponds to the XDM <c>dm:document-uri</c> accessor. It may be <c>null</c>
    /// for documents constructed in memory without an associated URI.
    /// </remarks>
    public string? DocumentUri { get; set; }

    /// <summary>
    /// The <see cref="NodeId"/> references to this document's child nodes in document order.
    /// </summary>
    /// <remarks>
    /// Per the XDM specification, only element, comment, and processing instruction nodes
    /// are valid children of a document node. Text nodes at the document level are discarded
    /// during parsing.
    /// </remarks>
    public required IReadOnlyList<NodeId> Children { get; init; }

    /// <summary>
    /// The <see cref="NodeId"/> of the document element (the first element child), or <c>null</c>
    /// if the document has no element children.
    /// </summary>
    /// <remarks>
    /// Well-formed XML documents always have exactly one document element. This property
    /// provides direct access without iterating <see cref="Children"/>.
    /// </remarks>
    public NodeId? DocumentElement { get; init; }

    /// <summary>
    /// The local name of the document element, cached for efficient
    /// <c>document-node(element(name))</c> type checks in XPath/XQuery.
    /// </summary>
    public string? DocumentElementLocalName { get; set; }

    /// <summary>
    /// The string value of this document, which is the concatenation of all descendant text nodes.
    /// </summary>
    /// <remarks>
    /// Computing the string value requires a tree traversal. Until the traversal is performed
    /// and the result cached via the internal <c>_stringValue</c> field, this returns
    /// <see cref="string.Empty"/>.
    /// </remarks>
    public override string StringValue => _stringValue ?? string.Empty;

    /// <summary>
    /// Internal backing field for the lazily-computed string value.
    /// Set by tree walkers after traversing descendant text nodes.
    /// </summary>
    internal string? _stringValue;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(StringValue);

    private string? _baseUri;
    public override string? BaseUri
    {
        get => _baseUri ?? DocumentUri;
        set => _baseUri = value;
    }

    /// <summary>
    /// An empty, immutable children list for documents with no child nodes.
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyChildren => ImmutableArray<NodeId>.Empty;
}
