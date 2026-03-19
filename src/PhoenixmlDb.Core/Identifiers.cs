using System;

namespace PhoenixmlDb.Core;

/// <summary>
/// Unique identifier for a container.
/// </summary>
public readonly record struct ContainerId(uint Value) : IComparable<ContainerId>
{
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
/// Unique identifier for a document within a container.
/// </summary>
public readonly record struct DocumentId(ulong Value) : IComparable<DocumentId>
{
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
/// Unique identifier for a node within a document.
/// </summary>
public readonly record struct NodeId(ulong Value) : IComparable<NodeId>
{
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
/// Interned namespace URI identifier.
/// </summary>
public readonly record struct NamespaceId(uint Value) : IComparable<NamespaceId>
{
    /// <summary>No namespace.</summary>
    public static NamespaceId None => new(0);

    /// <summary>http://www.w3.org/XML/1998/namespace</summary>
    public static NamespaceId Xml => new(1);

    /// <summary>http://www.w3.org/2000/xmlns/</summary>
    public static NamespaceId Xmlns => new(2);

    /// <summary>http://www.w3.org/2001/XMLSchema</summary>
    public static NamespaceId Xsd => new(3);

    /// <summary>http://www.w3.org/2001/XMLSchema-instance</summary>
    public static NamespaceId Xsi => new(4);

    /// <summary>http://www.w3.org/2005/xpath-functions</summary>
    public static NamespaceId Fn => new(5);

    /// <summary>http://www.w3.org/2005/xpath-functions/map</summary>
    public static NamespaceId Map => new(6);

    /// <summary>http://www.w3.org/2005/xpath-functions/array</summary>
    public static NamespaceId Array => new(7);

    /// <summary>http://www.w3.org/2005/xpath-functions/math</summary>
    public static NamespaceId Math => new(8);

    /// <summary>http://phoenixml.endpointsystems.com/dbxml</summary>
    public static NamespaceId Dbxml => new(9);

    /// <summary>http://www.w3.org/1999/XSL/Transform</summary>
    public static NamespaceId Xslt => new(10);

    /// <summary>First ID available for user namespaces.</summary>
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
