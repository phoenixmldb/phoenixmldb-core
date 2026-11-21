using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// A store of XDM nodes: it resolves the <see cref="NodeId"/>s and <see cref="NamespaceId"/>s of
/// the nodes it holds.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="NodeId"/> and a <see cref="NamespaceId"/> have a meaning only inside the store
/// that assigned them: two stores can give the same id to different nodes, and different ids to
/// one namespace URI. A node's children, attributes and parent are ids, and its name is a
/// namespace id, so they must be resolved in the node's own store, which
/// <see cref="XdmNode.Store"/> names.
/// </para>
/// <para>
/// A store calls <see cref="XdmNode.AttachStore"/> on each node it holds.
/// </para>
/// </remarks>
public interface IXdmNodeStore
{
    /// <summary>
    /// The node of this store that has the id <paramref name="id"/>, or <c>null</c> when this
    /// store holds no such node.
    /// </summary>
    XdmNode? GetNode(NodeId id);

    /// <summary>
    /// The namespace URI that this store gave the id <paramref name="id"/>, or <c>null</c> when
    /// this store has not given that id. <see cref="NamespaceId.None"/> is no namespace.
    /// </summary>
    string? GetNamespaceUri(NamespaceId id);

    /// <summary>
    /// Finds the id that this store gave the namespace URI <paramref name="uri"/>, without giving
    /// a new one: <c>false</c> when the store has no id for it.
    /// </summary>
    bool TryGetNamespaceId(string uri, out NamespaceId id);
}
