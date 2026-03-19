using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixmlDb.Core;

/// <summary>
/// A stored document with content and metadata.
/// </summary>
public interface IDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    DocumentId Id { get; }

    /// <summary>
    /// Document name (URI-like identifier).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Container this document belongs to.
    /// </summary>
    ContainerId Container { get; }

    /// <summary>
    /// When the document was created.
    /// </summary>
    DateTimeOffset Created { get; }

    /// <summary>
    /// When the document was last modified.
    /// </summary>
    DateTimeOffset Modified { get; }

    /// <summary>
    /// Document size in bytes.
    /// </summary>
    long SizeBytes { get; }

    /// <summary>
    /// Content type (XML or JSON).
    /// </summary>
    ContentType ContentType { get; }

    /// <summary>
    /// Gets the document content as a string.
    /// </summary>
    ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document content as a stream.
    /// </summary>
    ValueTask<Stream> GetContentStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the root node of the document.
    /// </summary>
    ValueTask<IXdmNode> GetRootNodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a metadata value.
    /// </summary>
    ValueTask<object?> GetMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, object>> GetAllMetadataAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for XDM nodes.
/// </summary>
public interface IXdmNode
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// The kind of node (element, attribute, text, etc.).
    /// </summary>
    XdmNodeKind NodeKind { get; }

    /// <summary>
    /// Parent node, if any.
    /// </summary>
    IXdmNode? Parent { get; }

    /// <summary>
    /// String value of the node.
    /// </summary>
    string StringValue { get; }

    /// <summary>
    /// Node name (for elements, attributes, PIs).
    /// </summary>
    QName? NodeName { get; }
}

/// <summary>
/// XDM node kinds per XQuery Data Model specification.
/// </summary>
#pragma warning disable CA1028
public enum XdmNodeKind : byte
{
    None = 0,
    Document = 1,
    Element = 2,
    Attribute = 3,
    Text = 4,
    Comment = 5,
    ProcessingInstruction = 6,
    Namespace = 7
}

/// <summary>
/// Qualified name (namespace + local name).
/// </summary>
public readonly record struct QName : IEquatable<QName>
{
    public NamespaceId Namespace { get; }
    public string LocalName { get; }
    public string? Prefix { get; }
    /// <summary>
    /// When non-null, holds the namespace URI from EQName syntax (Q{uri}local).
    /// Takes precedence over NamespaceId for namespace resolution.
    /// Affects ToString() output (uses Q{uri}local format when prefix is absent).
    /// </summary>
    public string? ExpandedNamespace { get; init; }

    /// <summary>
    /// When non-null, holds the resolved namespace for runtime-created QNames
    /// (e.g., from fn:QName() or xs:QName()). Does NOT affect ToString() output.
    /// Used by namespace-uri-from-QName() and QName eq comparison.
    /// </summary>
    public string? RuntimeNamespace { get; init; }

    public QName(NamespaceId ns, string localName, string? prefix = null)
    {
        Namespace = ns;
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
        Prefix = prefix;
        ExpandedNamespace = null;
        RuntimeNamespace = null;
    }

    public string PrefixedName => Prefix is null ? LocalName : $"{Prefix}:{LocalName}";

    /// <summary>
    /// Gets the resolved namespace name, preferring ExpandedNamespace, then ResolvedNamespaceUri.
    /// Returns null if neither is available.
    /// </summary>
    public string? ResolvedNamespace => ExpandedNamespace ?? RuntimeNamespace;

    // QName equality is defined by namespace URI + local name per XPath/XSLT spec.
    // The prefix is NOT part of the identity.
    public bool Equals(QName other) => Namespace == other.Namespace && LocalName == other.LocalName;

    public override int GetHashCode() => HashCode.Combine(Namespace, LocalName);

    public override string ToString() => ExpandedNamespace != null ? $"Q{{{ExpandedNamespace}}}{LocalName}" : PrefixedName;
}
