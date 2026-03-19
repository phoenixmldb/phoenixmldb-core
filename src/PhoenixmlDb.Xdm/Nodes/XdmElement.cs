using System.Collections.Generic;
using System.Collections.Immutable;
using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML element.
/// </summary>
public sealed class XdmElement : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Element;

    /// <summary>
    /// The element's namespace URI (interned).
    /// </summary>
    public required NamespaceId Namespace { get; init; }

    /// <summary>
    /// The element's local name.
    /// </summary>
    public required string LocalName { get; init; }

    /// <summary>
    /// Prefix used in the original document (for serialization).
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Attribute nodes.
    /// </summary>
    public required IReadOnlyList<NodeId> Attributes { get; init; }

    /// <summary>
    /// Child nodes (element, text, comment, processing-instruction).
    /// </summary>
    public required IReadOnlyList<NodeId> Children { get; init; }

    /// <summary>
    /// In-scope namespace declarations at this element.
    /// </summary>
    public required IReadOnlyList<NamespaceBinding> NamespaceDeclarations { get; init; }

    /// <summary>
    /// Type annotation (default: xs:untyped).
    /// </summary>
    public XdmTypeName TypeAnnotation { get; init; } = XdmTypeName.Untyped;

    public override XdmQName? NodeName => new XdmQName(Namespace, LocalName, Prefix);

    /// <summary>
    /// String value requires tree traversal - returns empty string by default.
    /// Use a tree walker to compute actual string value.
    /// </summary>
    public override string StringValue => _stringValue ?? string.Empty;

    /// <summary>
    /// Sets the computed string value (after tree traversal).
    /// </summary>
    internal string? _stringValue;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(StringValue);

    /// <summary>
    /// Creates empty attribute list.
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyAttributes => ImmutableArray<NodeId>.Empty;

    /// <summary>
    /// Creates empty children list.
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyChildren => ImmutableArray<NodeId>.Empty;

    /// <summary>
    /// Creates empty namespace declarations list.
    /// </summary>
    public static IReadOnlyList<NamespaceBinding> EmptyNamespaceDeclarations => ImmutableArray<NamespaceBinding>.Empty;
}
