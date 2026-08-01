namespace PhoenixmlDb.Core.Metadata;

/// <summary>Engine-reserved document metadata (<c>https://schemas.phoenixml.dev/2026/meta</c>).</summary>
public static class PhxMeta
{
    /// <summary>The document's media type, for example <c>application/xml</c>.</summary>
    public static readonly MetadataProperty<string> ContentType =
        new(NamespaceId.PhoenixmlMeta, "content-type");

    /// <summary>The stored size of the document in bytes.</summary>
    public static readonly MetadataProperty<long> Size =
        new(NamespaceId.PhoenixmlMeta, "size");
}

/// <summary>Dublin Core Terms (<c>http://purl.org/dc/terms/</c>), the standard document-metadata vocabulary.</summary>
public static class DcTerms
{
    /// <summary>An entity primarily responsible for making the resource.</summary>
    public static readonly MetadataProperty<string> Creator = new(NamespaceId.DcTerms, "creator");

    /// <summary>Date of creation of the resource.</summary>
    public static readonly MetadataProperty<DateTimeOffset> Created = new(NamespaceId.DcTerms, "created");

    /// <summary>A name given to the resource.</summary>
    public static readonly MetadataProperty<string> Title = new(NamespaceId.DcTerms, "title");
}
