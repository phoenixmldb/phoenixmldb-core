using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an in-scope namespace binding on an element.
/// These are typically not stored separately but computed from element declarations.
/// </summary>
public sealed class XdmNamespace : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Namespace;

    /// <summary>
    /// The namespace prefix (empty string for default namespace).
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// The namespace URI.
    /// </summary>
    public required string Uri { get; init; }

    public override XdmQName? NodeName => new XdmQName(NamespaceId.None, Prefix, null);

    public override string StringValue => Uri;

    public override XdmValue TypedValue => XdmValue.XsString(Uri);
}
