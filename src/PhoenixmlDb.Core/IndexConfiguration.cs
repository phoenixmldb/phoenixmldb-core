using System;
using System.Collections.Generic;

namespace PhoenixmlDb.Core;

/// <summary>
/// Configuration for container indexes.
/// </summary>
public sealed class IndexConfiguration
{
    private readonly List<IndexDefinition> _indexes = [];
    private bool _structuralIndexEnabled = true;

    /// <summary>
    /// Adds a name index for fast element/attribute lookup.
    /// </summary>
    /// <param name="namespaceUri">
    /// Namespace to index (null = all namespaces).
    /// </param>
    public IndexConfiguration AddNameIndex(string? namespaceUri = null)
    {
        var uri = string.IsNullOrEmpty(namespaceUri) ? null : new Uri(namespaceUri);
        _indexes.Add(new NameIndexDefinition { NamespaceUri = uri });
        return this;
    }

    /// <summary>
    /// Adds a path index for specific path patterns.
    /// </summary>
    /// <param name="pathPattern">
    /// XPath-like pattern: //customer/address, /root/item/@id
    /// Supports: / (child), // (descendant), @ (attribute), * (wildcard)
    /// </param>
    public IndexConfiguration AddPathIndex(string pathPattern)
    {
        _indexes.Add(new PathIndexDefinition { PathPattern = pathPattern });
        return this;
    }

    /// <summary>
    /// Adds a value index for typed comparisons and range queries.
    /// </summary>
    /// <param name="pathPattern">Path to the element/attribute to index.</param>
    /// <param name="valueType">XDM type for value conversion.</param>
    /// <param name="collation">Collation for string ordering (null for binary).</param>
    public IndexConfiguration AddValueIndex(
        string pathPattern,
        XdmValueType valueType,
        string? collation = null)
    {
        _indexes.Add(new ValueIndexDefinition
        {
            PathPattern = pathPattern,
            ValueType = valueType,
            Collation = collation
        });
        return this;
    }

    /// <summary>
    /// Adds a full-text index for text search.
    /// </summary>
    /// <param name="pathPattern">Path to index (null for all text content).</param>
    /// <param name="options">Tokenization and analysis options.</param>
    public IndexConfiguration AddFullTextIndex(
        string? pathPattern = null,
        FullTextIndexOptions? options = null)
    {
        _indexes.Add(new FullTextIndexDefinition
        {
            PathPattern = pathPattern,
            Options = options ?? FullTextIndexOptions.Default
        });
        return this;
    }

    /// <summary>
    /// Adds a metadata index for document-level queries.
    /// </summary>
    public IndexConfiguration AddMetadataIndex(
        string metadataName,
        XdmValueType valueType = XdmValueType.XdmString)
    {
        _indexes.Add(new MetadataIndexDefinition
        {
            MetadataName = metadataName,
            ValueType = valueType
        });
        return this;
    }

    /// <summary>
    /// Enables or disables structural indexing for axis navigation.
    /// Enabled by default.
    /// </summary>
    public IndexConfiguration EnableStructuralIndex(bool enabled = true)
    {
        _structuralIndexEnabled = enabled;
        return this;
    }

    public IReadOnlyList<IndexDefinition> Indexes => _indexes;
    public bool StructuralIndexEnabled => _structuralIndexEnabled;

    public IndexConfiguration AddNameIndex(Uri? namespaceUri = null)
    {
        _indexes.Add(new NameIndexDefinition { NamespaceUri = namespaceUri });
        return this;
    }
}

/// <summary>
/// XDM value types for index typing.
/// </summary>
public enum XdmValueType
{
    XdmString,
    XdmInteger,
    XdmLong,
    XdmDecimal,
    XdmDouble,
    XdmFloat,
    Boolean,
    DateTime,
    Date,
    Time,
    Duration,
    AnyUri,
    QName,
    Base64Binary,
    HexBinary
}

/// <summary>
/// Options for full-text indexing.
/// </summary>
public record FullTextIndexOptions
{
    public string Language { get; init; } = "en";
    public bool CaseSensitive { get; init; }
    public bool Stemming { get; init; } = true;
    public IReadOnlySet<string>? StopWords { get; init; }

    public static FullTextIndexOptions Default { get; } = new();
}

/// <summary>
/// Base type for index definitions.
/// </summary>
public abstract record IndexDefinition;

/// <summary>
/// Name index definition.
/// </summary>
public record NameIndexDefinition : IndexDefinition
{
    public Uri? NamespaceUri { get; init; }
}

/// <summary>
/// Path index definition.
/// </summary>
public record PathIndexDefinition : IndexDefinition
{
    public required string PathPattern { get; init; }
}

/// <summary>
/// Value index definition.
/// </summary>
public record ValueIndexDefinition : IndexDefinition
{
    public required string PathPattern { get; init; }
    public required XdmValueType ValueType { get; init; }
    public string? Collation { get; init; }
}

/// <summary>
/// Full-text index definition.
/// </summary>
public record FullTextIndexDefinition : IndexDefinition
{
    public string? PathPattern { get; init; }
    public required FullTextIndexOptions Options { get; init; }
}

/// <summary>
/// Metadata index definition.
/// </summary>
public record MetadataIndexDefinition : IndexDefinition
{
    public required string MetadataName { get; init; }
    public required XdmValueType ValueType { get; init; }
}
