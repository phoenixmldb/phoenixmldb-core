using System;

namespace PhoenixmlDb.Core;

/// <summary>
/// A lightweight, strongly-typed identifier for a container within a PhoenixmlDb database.
/// </summary>
/// <remarks>
/// <para>
/// <c>ContainerId</c> is a <c>readonly record struct</c> wrapping an unsigned integer,
/// providing type safety so that container IDs cannot be accidentally mixed with
/// <see cref="DocumentId"/>, <see cref="NodeId"/>, or other identifier types at compile time.
/// </para>
/// <para>
/// Container IDs are assigned by the database when a container is created and are generally
/// obtained from API responses (e.g., <see cref="IContainer.Id"/>). You typically do not
/// construct them manually.
/// </para>
/// <para>
/// The <see cref="None"/> value (0) serves as the null equivalent — it represents
/// "no container" and is never assigned to a real container.
/// </para>
/// </remarks>
/// <seealso cref="IContainer.Id"/>
/// <seealso cref="DocumentId"/>
public readonly record struct ContainerId(uint Value) : IComparable<ContainerId>
{
    /// <summary>The null/empty container ID. Never assigned to a real container.</summary>
    public static ContainerId None => new(0);

    public int CompareTo(ContainerId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"C:{Value}";

    public static bool operator <(ContainerId left, ContainerId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ContainerId left, ContainerId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ContainerId left, ContainerId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ContainerId left, ContainerId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// A lightweight, strongly-typed identifier for a document within a container.
/// </summary>
/// <remarks>
/// <para>
/// <c>DocumentId</c> is a <c>readonly record struct</c> wrapping an unsigned 64-bit integer,
/// providing type safety so that document IDs cannot be accidentally passed where a
/// <see cref="ContainerId"/> or <see cref="NodeId"/> is expected.
/// </para>
/// <para>
/// Document IDs are assigned by the database when a document is stored and are obtained
/// from <see cref="IDocument.Id"/> or <see cref="DocumentInfo.Id"/>. They remain stable
/// across updates to the same document name and are unique within a container.
/// </para>
/// <para>
/// The <see cref="None"/> value (0) serves as the null equivalent — it represents
/// "no document" and is never assigned to a real document.
/// </para>
/// </remarks>
/// <seealso cref="IDocument.Id"/>
/// <seealso cref="ContainerId"/>
public readonly record struct DocumentId(ulong Value) : IComparable<DocumentId>
{
    /// <summary>The null/empty document ID. Never assigned to a real document.</summary>
    public static DocumentId None => new(0);

    public int CompareTo(DocumentId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"D:{Value}";

    public static bool operator <(DocumentId left, DocumentId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(DocumentId left, DocumentId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(DocumentId left, DocumentId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(DocumentId left, DocumentId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// A lightweight, strongly-typed identifier for a node within a document's XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// <c>NodeId</c> is a <c>readonly record struct</c> wrapping an unsigned 64-bit integer,
/// providing type safety so that node IDs cannot be mixed with <see cref="DocumentId"/>
/// or <see cref="ContainerId"/> at compile time.
/// </para>
/// <para>
/// Node IDs are assigned internally when a document is parsed into its XDM tree. They are
/// obtained from <see cref="IXdmNode.Id"/> and are used for node identity comparisons
/// (the XPath <c>is</c> operator) and internal index lookups.
/// </para>
/// <para>
/// The <see cref="None"/> value (0) serves as the null equivalent — it represents
/// "no node" and is never assigned to a real node.
/// </para>
/// </remarks>
/// <seealso cref="IXdmNode.Id"/>
public readonly record struct NodeId(ulong Value) : IComparable<NodeId>
{
    /// <summary>The null/empty node ID. Never assigned to a real node.</summary>
    public static NodeId None => new(0);

    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"N:{Value}";

    public static bool operator <(NodeId left, NodeId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(NodeId left, NodeId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(NodeId left, NodeId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(NodeId left, NodeId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// A lightweight, strongly-typed identifier for an interned namespace URI.
/// </summary>
/// <remarks>
/// <para>
/// <c>NamespaceId</c> is a <c>readonly record struct</c> wrapping an unsigned integer that
/// represents an interned namespace URI. Rather than comparing full namespace URI strings
/// (which can be long), the database interns each unique namespace URI into a compact integer
/// for efficient storage and comparison. This is an internal optimization — application code
/// typically works with <see cref="QName"/> values and does not need to resolve namespace
/// IDs to URIs directly.
/// </para>
/// <para>
/// Pre-defined constants are provided for the standard XML and XPath/XQuery namespaces.
/// IDs below <see cref="FirstUserNamespaceId"/> (100) are reserved for system use.
/// User-defined namespaces are assigned IDs starting from 100 by the database engine.
/// </para>
/// <para>
/// The <see cref="None"/> value (0) represents the absence of a namespace — elements and
/// attributes with no namespace declaration use this value.
/// </para>
/// </remarks>
/// <seealso cref="QName"/>
public readonly record struct NamespaceId(uint Value) : IComparable<NamespaceId>
{
    /// <summary>No namespace (the empty namespace). Used for elements and attributes that are not in any namespace.</summary>
    public static NamespaceId None => new(0);

    /// <summary>
    /// The XML namespace (<c>http://www.w3.org/XML/1998/namespace</c>).
    /// Bound to the <c>xml</c> prefix by definition. Contains attributes like <c>xml:lang</c>,
    /// <c>xml:space</c>, and <c>xml:base</c>.
    /// </summary>
    public static NamespaceId Xml => new(1);

    /// <summary>
    /// The XML Namespaces namespace (<c>http://www.w3.org/2000/xmlns/</c>).
    /// Used internally for namespace declarations (<c>xmlns:*</c> attributes). Application
    /// code rarely needs to reference this directly.
    /// </summary>
    public static NamespaceId Xmlns => new(2);

    /// <summary>
    /// The XML Schema namespace (<c>http://www.w3.org/2001/XMLSchema</c>).
    /// Defines built-in types like <c>xs:string</c>, <c>xs:integer</c>, <c>xs:dateTime</c>, etc.
    /// Conventionally bound to the <c>xs</c> or <c>xsd</c> prefix.
    /// </summary>
    public static NamespaceId Xsd => new(3);

    /// <summary>
    /// The XML Schema Instance namespace (<c>http://www.w3.org/2001/XMLSchema-instance</c>).
    /// Used for schema-related attributes in instance documents, such as <c>xsi:type</c>,
    /// <c>xsi:nil</c>, and <c>xsi:schemaLocation</c>.
    /// </summary>
    public static NamespaceId Xsi => new(4);

    /// <summary>
    /// The XPath/XQuery Functions namespace (<c>http://www.w3.org/2005/xpath-functions</c>).
    /// Contains built-in functions like <c>fn:string()</c>, <c>fn:count()</c>, <c>fn:doc()</c>.
    /// Bound to the <c>fn</c> prefix by default in XQuery; functions in this namespace can be
    /// called without a prefix.
    /// </summary>
    public static NamespaceId Fn => new(5);

    /// <summary>
    /// The XPath/XQuery Map namespace (<c>http://www.w3.org/2005/xpath-functions/map</c>).
    /// Contains functions for working with XDM maps (XQuery 3.1), such as <c>map:merge()</c>
    /// and <c>map:get()</c>. Conventionally bound to the <c>map</c> prefix.
    /// </summary>
    public static NamespaceId Map => new(6);

    /// <summary>
    /// The XPath/XQuery Array namespace (<c>http://www.w3.org/2005/xpath-functions/array</c>).
    /// Contains functions for working with XDM arrays (XQuery 3.1), such as <c>array:size()</c>
    /// and <c>array:flatten()</c>. Conventionally bound to the <c>array</c> prefix.
    /// </summary>
    public static NamespaceId Array => new(7);

    /// <summary>
    /// The XPath/XQuery Math namespace (<c>http://www.w3.org/2005/xpath-functions/math</c>).
    /// Contains mathematical functions like <c>math:sqrt()</c>, <c>math:pi()</c>, and
    /// <c>math:pow()</c>. Conventionally bound to the <c>math</c> prefix.
    /// </summary>
    public static NamespaceId Math => new(8);

    /// <summary>
    /// The PhoenixmlDb extension namespace (<c>http://phoenixml.endpointsystems.com/dbxml</c>).
    /// Contains database-specific extension functions for use in XQuery expressions.
    /// Conventionally bound to the <c>dbxml</c> prefix.
    /// </summary>
    public static NamespaceId Dbxml => new(9);

    /// <summary>
    /// The XSLT namespace (<c>http://www.w3.org/1999/XSL/Transform</c>).
    /// Used for XSLT stylesheet elements. Conventionally bound to the <c>xsl</c> prefix.
    /// </summary>
    public static NamespaceId Xslt => new(10);

    /// <summary>
    /// The first namespace ID available for user-defined namespaces. IDs below this value
    /// (0-99) are reserved for system and standard namespaces.
    /// </summary>
    public const uint FirstUserNamespaceId = 100;

    public int CompareTo(NamespaceId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"NS:{Value}";

    public static bool operator <(NamespaceId left, NamespaceId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(NamespaceId left, NamespaceId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(NamespaceId left, NamespaceId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(NamespaceId left, NamespaceId right)
    {
        return left.CompareTo(right) >= 0;
    }
}
