using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Abstract base class for all XDM (XPath Data Model) nodes, representing the in-memory
/// structure of XML and JSON documents as defined by the W3C XDM specification.
/// </summary>
/// <remarks>
/// <para>
/// The XDM defines seven node kinds: <see cref="XdmNodeKind.Document"/>,
/// <see cref="XdmNodeKind.Element"/>, <see cref="XdmNodeKind.Attribute"/>,
/// <see cref="XdmNodeKind.Text"/>, <see cref="XdmNodeKind.Comment"/>,
/// <see cref="XdmNodeKind.ProcessingInstruction"/>, and <see cref="XdmNodeKind.Namespace"/>.
/// Each is represented by a concrete subclass of <c>XdmNode</c>.
/// </para>
/// <para>
/// <b>Navigation:</b> Every node except the document root has a <see cref="Parent"/> property
/// pointing to its containing node. To access children or attributes, cast the node to
/// <see cref="XdmElement"/> or <see cref="XdmDocument"/> and use their child/attribute lists.
/// Children and attributes are referenced by <see cref="NodeId"/>, which must be resolved
/// through a node lookup function (e.g., the one provided by the storage layer).
/// </para>
/// <para>
/// <b>Values:</b> <see cref="StringValue"/> returns the text content of the node as defined
/// by the XDM <c>dm:string-value</c> accessor. For element and document nodes, this is the
/// concatenation of all descendant text nodes. <see cref="TypedValue"/> returns the
/// schema-typed value (<c>dm:typed-value</c>), which is <c>xs:untypedAtomic</c> for
/// unvalidated content.
/// </para>
/// <para>
/// <b>Identity:</b> The <see cref="Id"/> property is a storage-assigned identifier used
/// for efficient node lookup and parent/child references. It is <em>not</em> related to
/// the <c>xml:id</c> attribute or the XDM <c>dm:is</c> identity function.
/// </para>
/// </remarks>
/// <example>
/// Checking a node's kind and accessing its value:
/// <code>
/// XdmNode node = ...;
/// if (node.IsElement)
/// {
///     var element = (XdmElement)node;
///     Console.WriteLine($"Element: {element.NodeName}");
///     Console.WriteLine($"Text content: {element.StringValue}");
/// }
/// </code>
/// </example>
public abstract class XdmNode
{
    /// <summary>
    /// Unique storage identifier for this node, assigned during parsing or construction.
    /// </summary>
    /// <remarks>
    /// This identifier is used internally for parent/child references and node lookup.
    /// It is a monotonically increasing value assigned by <see cref="Parsing.XmlDocumentParser"/>
    /// and has no relationship to <c>xml:id</c> attributes in the source document.
    /// </remarks>
    public required NodeId Id { get; init; }

    /// <summary>
    /// The identifier of the document that contains this node.
    /// </summary>
    /// <remarks>
    /// Every node belongs to exactly one document. This property links the node back to
    /// its owning document, which is necessary for storage operations and cross-document
    /// identity comparisons.
    /// </remarks>
    public required DocumentId Document { get; init; }

    /// <summary>
    /// The kind of this node (element, attribute, text, etc.).
    /// </summary>
    /// <remarks>
    /// Each concrete subclass returns a fixed value. Use the convenience properties
    /// (<see cref="IsElement"/>, <see cref="IsAttribute"/>, etc.) or the <see cref="Is"/>
    /// method for readable kind checks.
    /// </remarks>
    public abstract XdmNodeKind NodeKind { get; }

    /// <summary>
    /// The parent node's identifier, or <c>null</c> for document nodes (which have no parent).
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the XDM, every node except the document root has a parent. Element children,
    /// attributes, and namespace nodes all point back to their owning element. The parent
    /// of a top-level element, comment, or processing instruction is the document node.
    /// </para>
    /// <para>
    /// This property is mutable to support tree construction scenarios where the parent
    /// is assigned after the child node is created.
    /// </para>
    /// </remarks>
    public NodeId? Parent { get; set; }

    /// <summary>
    /// The string value of this node, as defined by the XDM <c>dm:string-value</c> accessor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The meaning depends on the node kind:
    /// for text, comment, and PI nodes, it is the node's own text content;
    /// for attribute nodes, it is the attribute value;
    /// for element and document nodes, it is the concatenation of all descendant text nodes.
    /// </para>
    /// <para>
    /// For <see cref="XdmElement"/> and <see cref="XdmDocument"/>, the string value requires
    /// a tree traversal and is lazily computed. Until computed, it returns <see cref="string.Empty"/>.
    /// </para>
    /// </remarks>
    public abstract string StringValue { get; }

    /// <summary>
    /// The typed value of this node, as defined by the XDM <c>dm:typed-value</c> accessor.
    /// </summary>
    /// <remarks>
    /// For unvalidated (non-schema-validated) documents, element content returns
    /// <see cref="XdmType.UntypedAtomic"/> and attribute values return
    /// <see cref="XdmType.UntypedAtomic"/>. Schema-validated documents may return
    /// more specific types (e.g., <see cref="XdmType.XsInteger"/> for an <c>xs:integer</c>
    /// typed attribute).
    /// </remarks>
    public abstract XdmValue TypedValue { get; }

    /// <summary>
    /// The qualified name of this node (<c>dm:node-name</c>), or <c>null</c> for node kinds
    /// that have no name (text, comment, document).
    /// </summary>
    /// <remarks>
    /// Elements and attributes always have a name. Processing instructions have a name
    /// with no namespace. Namespace nodes use the prefix as the local name.
    /// Text, comment, and document nodes return <c>null</c>.
    /// </remarks>
    public virtual XdmQName? NodeName => null;

    /// <summary>
    /// The base URI for this node (<c>dm:base-uri</c>), used for resolving relative URIs.
    /// </summary>
    /// <remarks>
    /// For document nodes, this defaults to the document URI. For other nodes, it is typically
    /// inherited from the parent or set explicitly via <c>xml:base</c> attributes. The base URI
    /// is essential for functions like <c>fn:resolve-uri()</c> and <c>fn:doc()</c>.
    /// </remarks>
    public virtual string? BaseUri { get; set; }

    /// <summary>
    /// Base URI inherited from the source element during <c>xsl:copy</c> operations.
    /// </summary>
    /// <remarks>
    /// This is a fallback for orphaned nodes (those with no parent and no <c>xml:base</c>).
    /// Unlike <see cref="BaseUri"/>, which is entity-derived and overrides the parent's base URI,
    /// this value is only consulted when no parent base URI is available. It preserves the
    /// original document context for nodes extracted during XSLT transformations.
    /// </remarks>
    public string? CopySourceBaseUri { get; set; }

    /// <summary>
    /// Returns <c>true</c> if this node has the specified <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The node kind to test against.</param>
    public bool Is(XdmNodeKind kind) => NodeKind == kind;

    /// <summary>
    /// Returns <c>true</c> if this node is a <see cref="XdmDocument"/> node.
    /// </summary>
    public bool IsDocument => NodeKind == XdmNodeKind.Document;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmElement"/> node.
    /// </summary>
    public bool IsElement => NodeKind == XdmNodeKind.Element;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmAttribute"/> node.
    /// </summary>
    public bool IsAttribute => NodeKind == XdmNodeKind.Attribute;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmText"/> node.
    /// </summary>
    public bool IsText => NodeKind == XdmNodeKind.Text;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmComment"/> node.
    /// </summary>
    public bool IsComment => NodeKind == XdmNodeKind.Comment;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmProcessingInstruction"/> node.
    /// </summary>
    public bool IsProcessingInstruction => NodeKind == XdmNodeKind.ProcessingInstruction;

    /// <summary>
    /// Returns <c>true</c> if this node is an <see cref="XdmNamespace"/> node.
    /// </summary>
    public bool IsNamespace => NodeKind == XdmNodeKind.Namespace;

    /// <summary>
    /// Returns <see cref="StringValue"/>, providing the XDM string value of this node.
    /// </summary>
    public override string ToString() => StringValue;
}
