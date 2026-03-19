using System.Collections.Generic;
using System.Collections.Immutable;
using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML document root.
/// </summary>
public sealed class XdmDocument : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Document;

    /// <summary>
    /// The document URI (dm:document-uri).
    /// </summary>
    public string? DocumentUri { get; set; }

    /// <summary>
    /// Child nodes (element, comment, processing-instruction only - per XDM spec, no text children).
    /// </summary>
    public required IReadOnlyList<NodeId> Children { get; init; }

    /// <summary>
    /// The document element (first element child).
    /// </summary>
    public NodeId? DocumentElement { get; init; }

    /// <summary>
    /// Local name of the document element, for efficient document-node(element(name)) type checks.
    /// </summary>
    public string? DocumentElementLocalName { get; set; }

    /// <summary>
    /// String value requires tree traversal - returns empty string.
    /// Use a tree walker to compute actual string value.
    /// </summary>
    public override string StringValue => _stringValue ?? string.Empty;

    /// <summary>
    /// Sets the computed string value (after tree traversal).
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
    /// Creates an empty document children list.
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyChildren => ImmutableArray<NodeId>.Empty;
}
