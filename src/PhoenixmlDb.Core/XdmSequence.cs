using System.Collections;
using PhoenixmlDb.Xdm.Nodes;

// CA1062 (null check) / CA1024 (use property) suppressed: this is a thin value wrapper
// whose constructors are the obvious null-checking layer; properties that yield should
// stay methods.
#pragma warning disable CA1024

namespace PhoenixmlDb.Xdm;

/// <summary>
/// An ordered sequence of XDM items — the public wire-format for passing values
/// between transformations and queries without serializing through XML markup.
/// </summary>
/// <remarks>
/// <para>
/// An item may be:
/// </para>
/// <list type="bullet">
///   <item>An XDM node (<see cref="XdmNode"/> and its concrete subclasses)</item>
///   <item>An atomic value (<see cref="string"/>, <see cref="long"/>,
///         <see cref="bool"/>, <see cref="decimal"/>, <see cref="double"/>,
///         <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
///         <see cref="XsDateTime"/>, <see cref="XsDate"/>, <see cref="XsTime"/>,
///         <see cref="XsAnyUri"/>, <see cref="XdmQName"/>, …)</item>
///   <item>A map (<c>IDictionary&lt;object, object?&gt;</c>) or array
///         (<c>IReadOnlyList&lt;object?&gt;</c>)</item>
///   <item>A function item</item>
/// </list>
/// <para>
/// When the sequence contains <see cref="XdmNode"/> items, those nodes reference their
/// children by <c>NodeId</c> through the <see cref="Store"/> property. Producing engines
/// (<c>XsltTransformer</c>, XQuery's <c>QueryEngine</c>) populate <see cref="Store"/>
/// with the node-store backing their results so the sequence can be passed to another
/// engine without losing tree-navigation ability.
/// </para>
/// <para>
/// For pure-atomic sequences (no nodes), <see cref="Store"/> is null and the sequence
/// is fully self-contained.
/// </para>
/// </remarks>
public sealed class XdmSequence : IReadOnlyList<object?>
{
    private readonly object?[] _items;

    /// <summary>
    /// The node-store backing any <see cref="XdmNode"/> items in this sequence. Null
    /// for sequences containing only atomic values, or for sequences whose nodes do
    /// not require store-based child resolution. Consumed by engine overloads that
    /// accept <see cref="XdmSequence"/> as input.
    /// </summary>
    /// <remarks>
    /// Marked <see cref="object"/> rather than a typed interface to keep
    /// <c>PhoenixmlDb.Core</c> independent of the XSLT/XQuery node-store abstractions.
    /// Engines downcast as appropriate.
    /// </remarks>
    public object? Store { get; }

    private XdmSequence(object?[] items, object? store)
    {
        _items = items;
        Store = store;
    }

    /// <summary>The empty sequence.</summary>
    public static XdmSequence Empty { get; } = new(Array.Empty<object?>(), null);

    /// <summary>
    /// A sequence containing exactly one atomic item. For node items, use
    /// <see cref="OfNode(XdmNode, object)"/> so the sequence can carry the matching
    /// node-store reference.
    /// </summary>
    public static XdmSequence Of(object? item) =>
        item is null ? Empty : new XdmSequence([item], null);

    /// <summary>
    /// A sequence containing the supplied atomic items. Throws if any item is an
    /// <see cref="XdmNode"/> — node items require a paired node-store; use
    /// <see cref="OfNodes"/> for those.
    /// </summary>
    public static XdmSequence OfAtomics(params object?[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (item is XdmNode)
                throw new ArgumentException(
                    "XdmSequence.OfAtomics rejects node items — they need a paired node-store. " +
                    "Use OfNodes(store, …) or OfNode(node, store) instead.",
                    nameof(items));
        }
        return items.Length == 0 ? Empty : new XdmSequence((object?[])items.Clone(), null);
    }

    /// <summary>
    /// A sequence containing a single node item, paired with the node-store backing it.
    /// </summary>
    /// <param name="node">The node value.</param>
    /// <param name="store">
    /// The node-store backing the node's child references. Typically obtained from a
    /// previous transformation result (see <c>XsltTransformer.TransformToSequenceAsync</c>)
    /// or constructed directly (e.g. <c>XdmInMemoryStore</c>).
    /// </param>
    public static XdmSequence OfNode(XdmNode node, object store)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(store);
        return new XdmSequence([node], store);
    }

    /// <summary>
    /// A sequence of node items, all sharing the same backing node-store.
    /// </summary>
    public static XdmSequence OfNodes(object store, params XdmNode[] nodes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Length == 0)
            return Empty;
        var copy = new object?[nodes.Length];
        Array.Copy(nodes, copy, nodes.Length);
        return new XdmSequence(copy, store);
    }

    /// <summary>
    /// Internal factory used by engines to wrap a freshly-produced result. Bypasses
    /// the public type-segregation guards because the engine knows what's in the items.
    /// </summary>
    public static XdmSequence FromEngineResult(IReadOnlyList<object?> items, object? store)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            return Empty;
        var copy = new object?[items.Count];
        for (var i = 0; i < items.Count; i++)
            copy[i] = items[i];
        return new XdmSequence(copy, store);
    }

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <summary>True for the empty sequence.</summary>
    public bool IsEmpty => _items.Length == 0;

    /// <inheritdoc/>
    public object? this[int index] => _items[index];

    /// <summary>The first item, or null for the empty sequence (Saxon-style head accessor).</summary>
    public object? Head => _items.Length > 0 ? _items[0] : null;

    /// <summary>
    /// The sub-sequence containing all items except the first (Saxon-style tail
    /// accessor). Empty for a zero- or single-item sequence.
    /// </summary>
    public XdmSequence Tail
    {
        get
        {
            if (_items.Length <= 1)
                return Empty;
            var tail = new object?[_items.Length - 1];
            Array.Copy(_items, 1, tail, 0, tail.Length);
            return new XdmSequence(tail, Store);
        }
    }

    /// <summary>
    /// True when the sequence contains exactly one node item. Useful for guarding
    /// <c>AsSingleNode()</c> calls.
    /// </summary>
    public bool IsSingleNode => _items.Length == 1 && _items[0] is XdmNode;

    /// <summary>
    /// Returns the single node item, or null if the sequence is not exactly one
    /// node.
    /// </summary>
    public XdmNode? AsSingleNode() => _items.Length == 1 ? _items[0] as XdmNode : null;

    /// <inheritdoc/>
    public IEnumerator<object?> GetEnumerator() => ((IEnumerable<object?>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public override string ToString() =>
        Count switch
        {
            0 => "()",
            1 => $"XdmSequence[1]({DescribeItem(_items[0])})",
            _ => $"XdmSequence[{Count}]"
        };

    private static string DescribeItem(object? item) => item switch
    {
        null => "null",
        XdmNode n => n.GetType().Name,
        string s => s.Length <= 20 ? $"\"{s}\"" : $"\"{s[..20]}…\"",
        _ => item.GetType().Name,
    };
}
