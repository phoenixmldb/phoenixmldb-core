using System.Collections.Generic;
using System.Collections.Immutable;
using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML element node in the XDM tree, including its attributes,
/// children, and namespace declarations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Children:</b> An element's children are stored as a list of <see cref="NodeId"/> references
/// in <see cref="Children"/>. Valid child node kinds are element, text, comment, and
/// processing instruction. To access the actual child nodes, resolve each <see cref="NodeId"/>
/// through the storage layer's node lookup function.
/// </para>
/// <para>
/// <b>Attributes:</b> Attributes are separate <see cref="XdmAttribute"/> nodes referenced
/// by <see cref="Attributes"/>. Per the XDM spec, attributes are <em>not</em> children of
/// the element — they exist on a separate axis. Use the attribute list to enumerate or
/// look up attributes by name.
/// </para>
/// <para>
/// <b>Namespaces:</b> Each element carries its own <see cref="NamespaceDeclarations"/>, which
/// record the namespace declarations (<c>xmlns:prefix="uri"</c>) that appear on this element.
/// The element's own namespace is stored separately in <see cref="Namespace"/>.
/// </para>
/// <para>
/// <b>Construction:</b> Elements are typically created by <see cref="Parsing.XmlDocumentParser"/>
/// during XML parsing, or constructed directly using object initializers with the required
/// properties. Use <see cref="EmptyAttributes"/>, <see cref="EmptyChildren"/>, and
/// <see cref="EmptyNamespaceDeclarations"/> for elements that have no attributes, children,
/// or namespace declarations.
/// </para>
/// </remarks>
/// <example>
/// Constructing an element programmatically:
/// <code>
/// var element = new XdmElement
/// {
///     Id = nodeId,
///     Document = documentId,
///     Namespace = NamespaceId.None,
///     LocalName = "item",
///     Prefix = null,
///     Attributes = XdmElement.EmptyAttributes,
///     Children = XdmElement.EmptyChildren,
///     NamespaceDeclarations = XdmElement.EmptyNamespaceDeclarations
/// };
/// </code>
/// </example>
public sealed class XdmElement : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Element;

    /// <summary>
    /// The element's namespace URI, stored as an interned <see cref="NamespaceId"/>.
    /// </summary>
    /// <remarks>
    /// Namespace URIs are interned (deduplicated) across the database so that namespace
    /// comparisons are integer comparisons rather than string comparisons. Use the
    /// namespace resolver to convert back to the URI string when needed (e.g., for serialization).
    /// </remarks>
    public required NamespaceId Namespace { get; init; }

    /// <summary>
    /// The element's local name (the part after the colon in a prefixed name).
    /// </summary>
    public required string LocalName { get; init; }

    /// <summary>
    /// The namespace prefix used in the original document, preserved for round-trip serialization.
    /// </summary>
    /// <remarks>
    /// This is <c>null</c> for elements in no namespace or using the default namespace.
    /// The prefix is not significant for identity or comparison — only <see cref="Namespace"/>
    /// and <see cref="LocalName"/> determine element identity.
    /// </remarks>
    public string? Prefix { get; init; }

    /// <summary>
    /// The <see cref="NodeId"/> references to this element's <see cref="XdmAttribute"/> nodes.
    /// </summary>
    /// <remarks>
    /// Per the XDM specification, attributes are not children. They exist on a separate axis
    /// and do not appear in <see cref="Children"/>. Namespace declarations (<c>xmlns:*</c>)
    /// are stored in <see cref="NamespaceDeclarations"/>, not here.
    /// </remarks>
    public required IReadOnlyList<NodeId> Attributes { get; init; }

    /// <summary>
    /// The <see cref="NodeId"/> references to this element's child nodes (elements, text,
    /// comments, and processing instructions) in document order.
    /// </summary>
    public required IReadOnlyList<NodeId> Children { get; init; }

    /// <summary>
    /// The namespace declarations (<c>xmlns:prefix="uri"</c>) that appear on this element.
    /// </summary>
    /// <remarks>
    /// These are the <em>declared</em> namespaces, not the full set of in-scope namespaces.
    /// In-scope namespaces include those inherited from ancestor elements. The serializer uses
    /// these declarations to reproduce the original namespace output.
    /// </remarks>
    public required IReadOnlyList<NamespaceBinding> NamespaceDeclarations { get; init; }

    /// <summary>
    /// The XSD type annotation for this element (default: <c>xs:untyped</c>).
    /// </summary>
    /// <remarks>
    /// For documents that have not been schema-validated, this is always
    /// <see cref="XdmTypeName.Untyped"/>. After schema validation, it reflects
    /// the element's declared type from the schema.
    /// </remarks>
    public XdmTypeName TypeAnnotation { get; init; } = XdmTypeName.Untyped;

    /// <summary>
    /// True if this element's simple-content type is <c>xs:ID</c> or a type derived from
    /// <c>xs:ID</c> by restriction. Populated during XSD schema validation. Used by
    /// <c>fn:id</c> and <c>fn:element-with-id</c> to locate elements whose typed content
    /// matches a candidate ID.
    /// </summary>
    public bool IsIdContent { get; init; }

    public override XdmQName? NodeName => new XdmQName(Namespace, LocalName, Prefix);

    /// <summary>
    /// The string value of this element, which is the concatenation of all descendant text nodes.
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

    /// <summary>
    /// An empty, immutable attribute list for elements with no attributes.
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyAttributes => ImmutableArray<NodeId>.Empty;

    /// <summary>
    /// An empty, immutable children list for leaf elements (no child nodes).
    /// </summary>
    public static IReadOnlyList<NodeId> EmptyChildren => ImmutableArray<NodeId>.Empty;

    /// <summary>
    /// An empty, immutable namespace declarations list for elements with no namespace declarations.
    /// </summary>
    public static IReadOnlyList<NamespaceBinding> EmptyNamespaceDeclarations => ImmutableArray<NamespaceBinding>.Empty;
}
