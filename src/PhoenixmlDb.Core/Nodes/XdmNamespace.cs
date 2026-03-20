using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents a namespace node in the XDM tree, corresponding to an in-scope namespace
/// binding on an element.
/// </summary>
/// <remarks>
/// <para>
/// Namespace nodes are rarely accessed directly in application code. They represent the
/// in-scope namespace bindings that the XDM specification requires on each element. In most
/// cases, namespace information is accessed through <see cref="XdmElement.NamespaceDeclarations"/>
/// or the <see cref="XdmElement.Namespace"/> property instead.
/// </para>
/// <para>
/// The <see cref="XdmNode.NodeName"/> of a namespace node uses the prefix as the local name
/// (with no namespace URI). The <see cref="XdmNode.StringValue"/> is the namespace URI itself.
/// The default namespace uses an empty string as the <see cref="Prefix"/>.
/// </para>
/// </remarks>
public sealed class XdmNamespace : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Namespace;

    /// <summary>
    /// The namespace prefix, or an empty string for the default namespace.
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// The namespace URI that this prefix is bound to.
    /// </summary>
    public required string Uri { get; init; }

    public override XdmQName? NodeName => new XdmQName(NamespaceId.None, Prefix, null);

    public override string StringValue => Uri;

    public override XdmValue TypedValue => XdmValue.XsString(Uri);
}
