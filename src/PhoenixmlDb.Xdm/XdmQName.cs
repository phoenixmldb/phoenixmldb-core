using System;
using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm;

/// <summary>
/// Represents an expanded QName (namespace URI + local name).
/// </summary>
public readonly record struct XdmQName
{
    public NamespaceId Namespace { get; }
    public string LocalName { get; }
    public string? Prefix { get; }

    public XdmQName(NamespaceId ns, string localName, string? prefix = null)
    {
        Namespace = ns;
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
        Prefix = prefix;
    }

    /// <summary>
    /// Returns the prefixed form (prefix:localname) if prefix is set,
    /// otherwise just the local name.
    /// </summary>
    public string PrefixedName => Prefix is null ? LocalName : $"{Prefix}:{LocalName}";

    /// <summary>
    /// Returns true if this QName has no namespace.
    /// </summary>
    public bool IsUnqualified => Namespace == NamespaceId.None;

    public override string ToString() => PrefixedName;

    /// <summary>
    /// Creates a QName with no namespace.
    /// </summary>
    public static XdmQName Local(string localName) => new(NamespaceId.None, localName);
}

/// <summary>
/// Represents a type name (for type annotations).
/// </summary>
public readonly record struct XdmTypeName
{
    public NamespaceId Namespace { get; }
    public string LocalName { get; }

    public XdmTypeName(NamespaceId ns, string localName)
    {
        Namespace = ns;
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
    }

    /// <summary>xs:untyped - for unvalidated element content.</summary>
    public static XdmTypeName Untyped { get; } = new(NamespaceId.Xsd, "untyped");

    /// <summary>xs:untypedAtomic - for unvalidated atomic values.</summary>
    public static XdmTypeName UntypedAtomic { get; } = new(NamespaceId.Xsd, "untypedAtomic");

    /// <summary>xs:anyType - the root of the type hierarchy.</summary>
    public static XdmTypeName AnyType { get; } = new(NamespaceId.Xsd, "anyType");

    /// <summary>xs:anySimpleType - base of all simple types.</summary>
    public static XdmTypeName AnySimpleType { get; } = new(NamespaceId.Xsd, "anySimpleType");

    /// <summary>xs:anyAtomicType - base of all atomic types.</summary>
    public static XdmTypeName AnyAtomicType { get; } = new(NamespaceId.Xsd, "anyAtomicType");

    /// <summary>xs:string</summary>
    public static XdmTypeName XsString { get; } = new(NamespaceId.Xsd, "string");

    /// <summary>xs:boolean</summary>
    public static XdmTypeName Boolean { get; } = new(NamespaceId.Xsd, "boolean");

    /// <summary>xs:decimal</summary>
    public static XdmTypeName XsDecimal { get; } = new(NamespaceId.Xsd, "decimal");

    /// <summary>xs:integer</summary>
    public static XdmTypeName XsInteger { get; } = new(NamespaceId.Xsd, "integer");

    /// <summary>xs:double</summary>
    public static XdmTypeName XsDouble { get; } = new(NamespaceId.Xsd, "double");

    /// <summary>xs:float</summary>
    public static XdmTypeName XsFloat { get; } = new(NamespaceId.Xsd, "float");

    /// <summary>xs:date</summary>
    public static XdmTypeName Date { get; } = new(NamespaceId.Xsd, "date");

    /// <summary>xs:dateTime</summary>
    public static XdmTypeName DateTime { get; } = new(NamespaceId.Xsd, "dateTime");

    /// <summary>xs:time</summary>
    public static XdmTypeName Time { get; } = new(NamespaceId.Xsd, "time");

    /// <summary>xs:duration</summary>
    public static XdmTypeName Duration { get; } = new(NamespaceId.Xsd, "duration");

    /// <summary>xs:QName</summary>
    public static XdmTypeName QName { get; } = new(NamespaceId.Xsd, "QName");

    /// <summary>xs:anyURI</summary>
    public static XdmTypeName AnyUri { get; } = new(NamespaceId.Xsd, "anyURI");

    /// <summary>xs:base64Binary</summary>
    public static XdmTypeName Base64Binary { get; } = new(NamespaceId.Xsd, "base64Binary");

    /// <summary>xs:hexBinary</summary>
    public static XdmTypeName HexBinary { get; } = new(NamespaceId.Xsd, "hexBinary");

    public override string ToString() => $"xs:{LocalName}";
}

/// <summary>
/// A namespace prefix binding.
/// </summary>
public readonly record struct NamespaceBinding(string Prefix, NamespaceId Namespace);
