using PhoenixmlDb.Xdm;

namespace PhoenixmlDb.Core.Metadata;

/// <summary>
/// A typed, discoverable handle on one metadata name in one namespace.
/// </summary>
/// <remarks>
/// <para>
/// Declaring properties as static readonly fields on a vocabulary class gives call sites
/// compile-time typing, rename refactoring, and IntelliSense discovery, and keeps namespace
/// URIs out of the call site entirely:
/// </para>
/// <code>
/// public static class Routing
/// {
///     public static readonly MetadataProperty&lt;string&gt; Status = new(RoutingNs, "status");
/// }
///
/// await container.SetMetadataAsync(docId, Routing.Status, "pending");
/// string? status = await container.GetMetadataAsync(docId, Routing.Status);
/// </code>
/// </remarks>
/// <typeparam name="T">The CLR type of the value. Must be supported by <see cref="XdmValue.From{T}"/>.</typeparam>
public sealed record MetadataProperty<T>
{
    /// <summary>The namespace this metadata name belongs to.</summary>
    public NamespaceId Namespace { get; }

    /// <summary>The local name, unqualified.</summary>
    public string Name { get; }

    /// <summary>The qualified name used as the storage and index key.</summary>
    public XdmQName QName { get; }

    /// <param name="namespace">The owning namespace.</param>
    /// <param name="name">The local name. Must not be empty.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="T"/> has no XDM representation, or is <see cref="object"/>.
    /// <see cref="object"/> is rejected even though <see cref="XdmValue.From{T}"/> accepts
    /// it (by dispatching on the value's runtime type) — a
    /// <see cref="MetadataProperty{T}"/> exists to give call sites a concrete, checked
    /// type, and <c>MetadataProperty&lt;object&gt;</c> would defeat that by forcing every
    /// read back into an unchecked cast.
    /// </exception>
    public MetadataProperty(NamespaceId @namespace, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (typeof(T) == typeof(object))
            throw new NotSupportedException(
                $"MetadataProperty<object> is not allowed: it would defeat the type safety " +
                "this descriptor exists to provide. Use the property's concrete value type instead.");
        if (!XdmValue.IsSupportedClrType(typeof(T)))
            throw new NotSupportedException(
                $"MetadataProperty<{typeof(T).Name}> is not storable: no XDM representation.");

        Namespace = @namespace;
        Name = name;
        QName = new XdmQName(@namespace, name);
    }

    /// <summary>Converts a value of this property's type to its XDM representation.</summary>
    public XdmValue ToXdm(T value) => XdmValue.From(value);

    /// <summary>Converts an XDM value back to this property's type.</summary>
    public T? FromXdm(XdmValue value) => XdmValue.To<T>(value);

    /// <inheritdoc />
    public override string ToString() =>
        NamespaceRegistry.GetConventionalPrefix(Namespace) is { } p ? $"{p}:{Name}" : Name;
}
