using System;
using System.Collections.Generic;

namespace PhoenixmlDb.Core;

/// <summary>
/// Provides a fluent API for defining the indexes that accelerate XQuery and metadata queries
/// on a container's documents.
/// </summary>
/// <remarks>
/// <para>
/// Indexes are the primary mechanism for query performance in PhoenixmlDb. Without indexes,
/// XQuery expressions require full document scans, which is acceptable for small collections
/// but becomes slow as the number or size of documents grows.
/// </para>
/// <para>
/// <b>When to define indexes:</b> Indexes should be defined at container creation time via
/// <see cref="ContainerOptions.Indexes"/>. Documents inserted after index creation are
/// automatically indexed. However, existing documents are <em>not</em> retroactively indexed
/// when new index definitions are added later.
/// </para>
/// <para>
/// <b>Choosing the right index type:</b> Each index type serves a different query pattern:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="AddNameIndex(string?)"/> — speeds up element/attribute name lookups (e.g., <c>//product</c>).</description></item>
/// <item><description><see cref="AddPathIndex"/> — speeds up path expression evaluation (e.g., <c>//customer/address/city</c>).</description></item>
/// <item><description><see cref="AddValueIndex"/> — enables typed range queries (e.g., <c>//product[price &gt; 50]</c>, <c>//order[date &gt; xs:date('2024-01-01')]</c>).</description></item>
/// <item><description><see cref="AddFullTextIndex"/> — enables <c>ft:contains()</c> full-text search on text content.</description></item>
/// <item><description><see cref="AddMetadataIndex"/> — enables efficient queries by document metadata key-value pairs.</description></item>
/// <item><description><see cref="EnableStructuralIndex"/> — maintains parent-child structural relationships for fast axis navigation (enabled by default).</description></item>
/// </list>
/// <para>
/// The API is fluent — each method returns the <c>IndexConfiguration</c> instance, allowing
/// chained calls.
/// </para>
/// </remarks>
/// <example>
/// <para>A realistic index configuration for a product catalog:</para>
/// <code>
/// var container = await db.CreateContainerAsync("products", opts =>
/// {
///     opts.Indexes
///         // Fast lookup by element name across all namespaces
///         .AddNameIndex()
///         // Accelerate path-based queries
///         .AddPathIndex("//product/@id")
///         .AddPathIndex("//product/category")
///         // Typed range queries on price and date
///         .AddValueIndex("//product/price", XdmValueType.XdmDecimal)
///         .AddValueIndex("//product/releaseDate", XdmValueType.Date)
///         // Full-text search on descriptions
///         .AddFullTextIndex("//product/description", new FullTextIndexOptions
///         {
///             Language = "en",
///             Stemming = true,
///             CaseSensitive = false
///         })
///         // Query documents by metadata
///         .AddMetadataIndex("supplier", XdmValueType.XdmString)
///         .AddMetadataIndex("importBatch", XdmValueType.XdmInteger);
/// });
/// </code>
/// </example>
/// <seealso cref="ContainerOptions.Indexes"/>
public sealed class IndexConfiguration
{
    private readonly List<IndexDefinition> _indexes = [];
    private bool _structuralIndexEnabled = true;

    /// <summary>
    /// Adds a name index that speeds up element and attribute name lookups.
    /// </summary>
    /// <param name="namespaceUri">
    /// Namespace URI to restrict the index to, or <c>null</c> to index names in all namespaces.
    /// Pass <c>null</c> (the default) unless you want to limit indexing to a specific namespace
    /// for storage efficiency.
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// A name index accelerates queries that look up elements or attributes by name, such as
    /// <c>//product</c> or <c>//@id</c>. Without a name index, the query engine must scan
    /// every node in every document to find matching names.
    /// </para>
    /// </remarks>
    public IndexConfiguration AddNameIndex(string? namespaceUri = null)
    {
        var uri = string.IsNullOrEmpty(namespaceUri) ? null : new Uri(namespaceUri);
        _indexes.Add(new NameIndexDefinition { NamespaceUri = uri });
        return this;
    }

    /// <summary>
    /// Adds a path index that speeds up evaluation of path expressions matching the given pattern.
    /// </summary>
    /// <param name="pathPattern">
    /// An XPath-like pattern specifying which paths to index.
    /// Supported syntax: <c>/</c> (child axis), <c>//</c> (descendant-or-self axis),
    /// <c>@</c> (attribute axis), <c>*</c> (wildcard).
    /// Examples: <c>"//customer/address"</c>, <c>"/root/item/@id"</c>, <c>"//order/*/price"</c>.
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// A path index pre-computes which documents contain nodes matching the specified path,
    /// enabling the query engine to skip documents that cannot possibly match. This is the
    /// most commonly used index type for structural XQuery queries.
    /// </para>
    /// </remarks>
    public IndexConfiguration AddPathIndex(string pathPattern)
    {
        _indexes.Add(new PathIndexDefinition { PathPattern = pathPattern });
        return this;
    }

    /// <summary>
    /// Adds a value index that enables typed comparisons and range queries on element or
    /// attribute values at the specified path.
    /// </summary>
    /// <param name="pathPattern">Path to the element or attribute whose values should be indexed.</param>
    /// <param name="valueType">
    /// The XDM type to use when indexing values. The value at the path is cast to this type
    /// for storage in the index. For example, use <see cref="XdmValueType.XdmDecimal"/> for
    /// monetary values or <see cref="XdmValueType.Date"/> for dates.
    /// </param>
    /// <param name="collation">
    /// Collation URI for string ordering. When <c>null</c> (the default), binary (codepoint)
    /// comparison is used. Specify a collation for locale-aware string sorting.
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// A value index stores the typed value of each matching node, enabling the query engine
    /// to evaluate predicates like <c>//product[price &gt; 50]</c> or
    /// <c>//order[date &gt; xs:date('2024-01-01')]</c> using an index lookup instead of
    /// scanning and parsing every document.
    /// </para>
    /// <para>
    /// The <paramref name="valueType"/> must match the type used in the query predicate.
    /// Indexing a price as <see cref="XdmValueType.XdmString"/> will not help a numeric
    /// comparison query.
    /// </para>
    /// </remarks>
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
    /// Adds a full-text index that enables <c>ft:contains()</c> full-text search on text content.
    /// </summary>
    /// <param name="pathPattern">
    /// Path to restrict full-text indexing to, or <c>null</c> to index all text content
    /// in the document. For example, <c>"//product/description"</c> indexes only description
    /// elements, reducing index size and improving relevance.
    /// </param>
    /// <param name="options">
    /// Tokenization and text analysis options controlling language, stemming, case sensitivity,
    /// and stop words. When <c>null</c>, <see cref="FullTextIndexOptions.Default"/> is used
    /// (English, stemming enabled, case-insensitive).
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Full-text indexes tokenize text content into searchable terms. This enables natural
    /// language queries using the <c>ft:contains()</c> function in XQuery, supporting
    /// stemming (e.g., "running" matches "run"), case-insensitive matching, and stop word
    /// filtering.
    /// </para>
    /// </remarks>
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
    /// Adds a metadata index that enables efficient queries by document metadata key-value pairs.
    /// </summary>
    /// <param name="metadataName">The metadata key to index (case-sensitive).</param>
    /// <param name="valueType">
    /// The XDM type for indexing the metadata value. Defaults to <see cref="XdmValueType.XdmString"/>.
    /// Use a numeric type if the metadata values are numbers and you need range queries.
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Without a metadata index, <see cref="IContainer.QueryMetadataAsync"/> must scan all
    /// documents' metadata to find matches. A metadata index enables direct lookup by key and value.
    /// </para>
    /// </remarks>
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
    /// Enables or disables structural indexing for parent-child axis navigation.
    /// Enabled by default.
    /// </summary>
    /// <param name="enabled">
    /// <c>true</c> to enable (the default), <c>false</c> to disable.
    /// </param>
    /// <returns>This <see cref="IndexConfiguration"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The structural index maintains parent-child and sibling relationships between nodes,
    /// enabling efficient evaluation of axis steps like <c>child::</c>, <c>parent::</c>,
    /// <c>following-sibling::</c>, and <c>descendant::</c>. It is enabled by default because
    /// most XQuery expressions use structural navigation.
    /// </para>
    /// <para>
    /// Disabling the structural index reduces storage overhead and write latency for containers
    /// where documents are only accessed by name (no XQuery navigation), but this is uncommon.
    /// </para>
    /// </remarks>
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
/// Enumerates the XDM (XQuery Data Model) value types that can be used for typed index
/// definitions in <see cref="IndexConfiguration.AddValueIndex"/> and
/// <see cref="IndexConfiguration.AddMetadataIndex"/>.
/// </summary>
/// <remarks>
/// <para>
/// The value type determines how indexed values are stored and compared. Choosing the correct
/// type is important: a <see cref="XdmDecimal"/> index supports numeric range queries
/// (<c>price &gt; 50</c>), while an <see cref="XdmString"/> index performs lexicographic
/// comparison (where <c>"9" &gt; "50"</c> is true — usually not what you want for numbers).
/// </para>
/// </remarks>
/// <seealso cref="IndexConfiguration.AddValueIndex"/>
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
/// Controls tokenization and text analysis for full-text indexes created via
/// <see cref="IndexConfiguration.AddFullTextIndex"/>.
/// </summary>
/// <remarks>
/// <para>
/// These options determine how text content is broken into searchable terms. The defaults
/// are suitable for English-language content with stemming enabled and case-insensitive
/// matching. Adjust these settings for other languages or when you need exact-match behavior.
/// </para>
/// </remarks>
/// <seealso cref="IndexConfiguration.AddFullTextIndex"/>
public record FullTextIndexOptions
{
    public string Language { get; init; } = "en";
    public bool CaseSensitive { get; init; }
    public bool Stemming { get; init; } = true;
    public IReadOnlySet<string>? StopWords { get; init; }

    public static FullTextIndexOptions Default { get; } = new();
}

/// <summary>
/// Abstract base type for all index definitions. Each concrete subtype represents a
/// different index strategy.
/// </summary>
/// <seealso cref="NameIndexDefinition"/>
/// <seealso cref="PathIndexDefinition"/>
/// <seealso cref="ValueIndexDefinition"/>
/// <seealso cref="FullTextIndexDefinition"/>
/// <seealso cref="MetadataIndexDefinition"/>
public abstract record IndexDefinition;

/// <summary>
/// Defines a name index that accelerates element and attribute name lookups.
/// </summary>
/// <seealso cref="IndexConfiguration.AddNameIndex(string?)"/>
public record NameIndexDefinition : IndexDefinition
{
    public Uri? NamespaceUri { get; init; }
}

/// <summary>
/// Defines a path index that accelerates evaluation of XPath path expressions.
/// </summary>
/// <seealso cref="IndexConfiguration.AddPathIndex"/>
public record PathIndexDefinition : IndexDefinition
{
    public required string PathPattern { get; init; }
}

/// <summary>
/// Defines a value index that stores typed values for range queries and comparisons.
/// </summary>
/// <seealso cref="IndexConfiguration.AddValueIndex"/>
public record ValueIndexDefinition : IndexDefinition
{
    public required string PathPattern { get; init; }
    public required XdmValueType ValueType { get; init; }
    public string? Collation { get; init; }
}

/// <summary>
/// Defines a full-text index that tokenizes text content for <c>ft:contains()</c> search.
/// </summary>
/// <seealso cref="IndexConfiguration.AddFullTextIndex"/>
public record FullTextIndexDefinition : IndexDefinition
{
    public string? PathPattern { get; init; }
    public required FullTextIndexOptions Options { get; init; }
}

/// <summary>
/// Defines a metadata index that enables efficient queries by document metadata key-value pairs.
/// </summary>
/// <seealso cref="IndexConfiguration.AddMetadataIndex"/>
public record MetadataIndexDefinition : IndexDefinition
{
    public required string MetadataName { get; init; }
    public required XdmValueType ValueType { get; init; }
}
