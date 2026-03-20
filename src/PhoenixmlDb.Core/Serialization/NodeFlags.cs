using System;

namespace PhoenixmlDb.Xdm.Serialization;

/// <summary>
/// Flags used in node serialization header.
/// </summary>
[Flags]
public enum NodeFlags : byte
{
    None = 0,
    HasParent = 1 << 0,
    HasNamespace = 1 << 1,
    HasChildren = 1 << 2,
    InlineChildren = 1 << 3,
    HasAttributes = 1 << 4,
    HasNamespaceDecls = 1 << 5,
    HasPrefix = 1 << 6,
    Compressed = 1 << 7
}
