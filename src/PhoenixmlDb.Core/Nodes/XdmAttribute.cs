using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML attribute node in the XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// Attribute nodes are associated with an <see cref="XdmElement"/> but are <em>not</em>
/// children of that element. They exist on a separate axis, accessed through
/// <see cref="XdmElement.Attributes"/>. An attribute's <see cref="XdmNode.Parent"/>
/// points to its owning element.
/// </para>
/// <para>
/// Unlike element and document nodes, an attribute's <see cref="XdmNode.StringValue"/>
/// is simply its <see cref="Value"/> — no tree traversal is needed.
/// </para>
/// <para>
/// Namespace declaration attributes (<c>xmlns:*</c>) are <em>not</em> represented as
/// <c>XdmAttribute</c> nodes. They are stored as <see cref="NamespaceBinding"/> entries
/// in <see cref="XdmElement.NamespaceDeclarations"/>.
/// </para>
/// </remarks>
public sealed class XdmAttribute : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Attribute;

    /// <summary>
    /// The attribute's namespace URI, stored as an interned <see cref="NamespaceId"/>.
    /// </summary>
    /// <remarks>
    /// Most attributes are in no namespace (<see cref="NamespaceId.None"/>). Attributes only
    /// have a namespace when explicitly prefixed (e.g., <c>xml:lang</c>, <c>xlink:href</c>).
    /// Unlike elements, an unprefixed attribute does <em>not</em> inherit the default namespace.
    /// </remarks>
    public required NamespaceId Namespace { get; init; }

    /// <summary>
    /// The attribute's local name (the part after the colon in a prefixed name, or the
    /// entire name for unprefixed attributes).
    /// </summary>
    public required string LocalName { get; init; }

    /// <summary>
    /// The namespace prefix used in the original document, preserved for round-trip serialization.
    /// </summary>
    /// <remarks>
    /// This is <c>null</c> for unprefixed attributes. The prefix is not significant for
    /// identity — only <see cref="Namespace"/> and <see cref="LocalName"/> matter.
    /// </remarks>
    public string? Prefix { get; init; }

    /// <summary>
    /// The attribute's string value (the text between the quotes in the source XML).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The XSD type annotation for this attribute (default: <c>xs:untypedAtomic</c>).
    /// </summary>
    /// <remarks>
    /// For documents that have not been schema-validated, this is always
    /// <see cref="XdmTypeName.UntypedAtomic"/>. After schema validation, it reflects
    /// the attribute's declared type from the schema (e.g., <c>xs:ID</c>, <c>xs:integer</c>).
    /// </remarks>
    public XdmTypeName TypeAnnotation { get; init; } = XdmTypeName.UntypedAtomic;

    /// <summary>
    /// Indicates whether this attribute is an ID attribute, as determined by the DTD
    /// or by being named <c>xml:id</c>.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, this attribute's value can be used with the <c>fn:id()</c>
    /// XPath function to locate the owning element.
    /// </remarks>
    public bool IsId { get; init; }

    public override XdmQName? NodeName => new XdmQName(Namespace, LocalName, Prefix);

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(Value);
}
