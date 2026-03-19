using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Base type for all XDM nodes.
/// </summary>
public abstract class XdmNode
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public required NodeId Id { get; init; }

    /// <summary>
    /// The document containing this node.
    /// </summary>
    public required DocumentId Document { get; init; }

    /// <summary>
    /// The kind of this node.
    /// </summary>
    public abstract XdmNodeKind NodeKind { get; }

    /// <summary>
    /// Parent node, if any. Document nodes have no parent.
    /// </summary>
    public NodeId? Parent { get; set; }

    /// <summary>
    /// The string value of this node (dm:string-value).
    /// For nodes with children, this requires tree traversal.
    /// </summary>
    public abstract string StringValue { get; }

    /// <summary>
    /// The typed value of this node (dm:typed-value).
    /// </summary>
    public abstract XdmValue TypedValue { get; }

    /// <summary>
    /// The name of this node (dm:node-name), if any.
    /// </summary>
    public virtual XdmQName? NodeName => null;

    /// <summary>
    /// Base URI for this node (dm:base-uri).
    /// </summary>
    public virtual string? BaseUri { get; set; }

    /// <summary>
    /// Base URI inherited from xsl:copy source element.
    /// Used as fallback for orphaned nodes (no parent, no xml:base).
    /// Unlike BaseUri (entity-derived, overrides parent), this is only
    /// used when no parent base URI is available.
    /// </summary>
    public string? CopySourceBaseUri { get; set; }

    /// <summary>
    /// Returns true if this node has the specified node kind.
    /// </summary>
    public bool Is(XdmNodeKind kind) => NodeKind == kind;

    /// <summary>
    /// Returns true if this node is a document node.
    /// </summary>
    public bool IsDocument => NodeKind == XdmNodeKind.Document;

    /// <summary>
    /// Returns true if this node is an element node.
    /// </summary>
    public bool IsElement => NodeKind == XdmNodeKind.Element;

    /// <summary>
    /// Returns true if this node is an attribute node.
    /// </summary>
    public bool IsAttribute => NodeKind == XdmNodeKind.Attribute;

    /// <summary>
    /// Returns true if this node is a text node.
    /// </summary>
    public bool IsText => NodeKind == XdmNodeKind.Text;

    /// <summary>
    /// Returns true if this node is a comment node.
    /// </summary>
    public bool IsComment => NodeKind == XdmNodeKind.Comment;

    /// <summary>
    /// Returns true if this node is a processing instruction node.
    /// </summary>
    public bool IsProcessingInstruction => NodeKind == XdmNodeKind.ProcessingInstruction;

    /// <summary>
    /// Returns true if this node is a namespace node.
    /// </summary>
    public bool IsNamespace => NodeKind == XdmNodeKind.Namespace;

    /// <summary>
    /// Returns the string value of this node (dm:string-value).
    /// </summary>
    public override string ToString() => StringValue;
}
