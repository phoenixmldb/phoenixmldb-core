using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML attribute.
/// </summary>
public sealed class XdmAttribute : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Attribute;

    /// <summary>
    /// The attribute's namespace URI (interned).
    /// </summary>
    public required NamespaceId Namespace { get; init; }

    /// <summary>
    /// The attribute's local name.
    /// </summary>
    public required string LocalName { get; init; }

    /// <summary>
    /// Prefix used in the original document.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// The attribute's string value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Type annotation (default: xs:untypedAtomic).
    /// </summary>
    public XdmTypeName TypeAnnotation { get; init; } = XdmTypeName.UntypedAtomic;

    /// <summary>
    /// Whether this attribute is an ID attribute (from DTD or xml:id).
    /// </summary>
    public bool IsId { get; init; }

    public override XdmQName? NodeName => new XdmQName(Namespace, LocalName, Prefix);

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(Value);
}
