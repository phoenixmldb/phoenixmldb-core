using System.Collections;
using System.Diagnostics.CodeAnalysis;
using PhoenixmlDb.Xdm;

namespace PhoenixmlDb.Core.Metadata;

/// <summary>
/// All metadata for one document, keyed by qualified name.
/// </summary>
/// <remarks>
/// Replaces the previous <c>IReadOnlyDictionary&lt;string, object&gt;</c>, whose keys were
/// namespace and name joined by a colon and therefore could not be split back apart.
/// </remarks>
public sealed class MetadataCollection : IReadOnlyCollection<KeyValuePair<XdmQName, XdmValue>>
{
    private readonly IReadOnlyDictionary<XdmQName, XdmValue> _entries;

    /// <summary>An empty collection.</summary>
    public static MetadataCollection Empty { get; } =
        new(new Dictionary<XdmQName, XdmValue>());

    /// <param name="entries">The metadata entries. Taken by reference, not copied.</param>
    public MetadataCollection(IReadOnlyDictionary<XdmQName, XdmValue> entries)
        => _entries = entries ?? throw new ArgumentNullException(nameof(entries));

    /// <inheritdoc />
    public int Count => _entries.Count;

    /// <summary>The raw XDM value for a qualified name, or null if absent.</summary>
    [SuppressMessage("Design", "CA1043:Use Integral Or String Argument For Indexers",
        Justification = "XdmQName is this design's storage/index key — not an oversight.")]
    public XdmValue? this[XdmQName name] =>
        _entries.TryGetValue(name, out var v) ? v : null;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<XdmQName, XdmValue>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Entries grouped by namespace, without re-parsing any strings.</summary>
    public IEnumerable<IGrouping<NamespaceId, KeyValuePair<XdmQName, XdmValue>>> ByNamespace =>
        _entries.GroupBy(e => e.Key.Namespace);
}

/// <summary>Typed access to a <see cref="MetadataCollection"/>.</summary>
public static class MetadataCollectionExtensions
{
    /// <summary>The typed value for a property, or default if absent.</summary>
    public static T? Get<T>(this MetadataCollection collection, MetadataProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(property);
        var raw = collection[property.QName];
        return raw is null ? default : property.FromXdm(raw.Value);
    }

    /// <summary>Whether the collection contains a value for the property.</summary>
    public static bool Contains<T>(this MetadataCollection collection, MetadataProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(property);
        return collection[property.QName] is not null;
    }
}
