namespace PhoenixmlDb.Core;

/// <summary>
/// The single source of truth mapping well-known <see cref="NamespaceId"/> values to their
/// URIs and conventional prefixes.
/// </summary>
/// <remarks>
/// Namespace URIs are permanent identities. Once one has shipped in a stored document it can
/// never change — not even its scheme, which is part of the identity. Do not edit an existing
/// entry; add a new id instead.
/// </remarks>
public static class NamespaceRegistry
{
    private static readonly (NamespaceId Id, string Uri, string Prefix)[] Entries =
    [
        (NamespaceId.Xml,           "http://www.w3.org/XML/1998/namespace",         "xml"),
        (NamespaceId.Xmlns,         "http://www.w3.org/2000/xmlns/",                "xmlns"),
        (NamespaceId.Xsd,           "http://www.w3.org/2001/XMLSchema",             "xs"),
        (NamespaceId.Xsi,           "http://www.w3.org/2001/XMLSchema-instance",    "xsi"),
        (NamespaceId.Fn,            "http://www.w3.org/2005/xpath-functions",       "fn"),
        (NamespaceId.Map,           "http://www.w3.org/2005/xpath-functions/map",   "map"),
        (NamespaceId.Array,        "http://www.w3.org/2005/xpath-functions/array", "array"),
        (NamespaceId.Math,         "http://www.w3.org/2005/xpath-functions/math",  "math"),
        (NamespaceId.PhoenixmlDb,  "https://schemas.phoenixml.dev/2026/db",        "phx"),
        (NamespaceId.Xslt,         "http://www.w3.org/1999/XSL/Transform",         "xsl"),
        (NamespaceId.PhoenixmlMeta,"https://schemas.phoenixml.dev/2026/meta",      "dbxml"),
        (NamespaceId.DcTerms,      "http://purl.org/dc/terms/",                    "dcterms"),
    ];

    private static readonly Dictionary<string, NamespaceId> ByUri =
        Entries.ToDictionary(e => e.Uri, e => e.Id, StringComparer.Ordinal);

    /// <summary>Every namespace this registry knows.</summary>
    public static IReadOnlyList<NamespaceId> WellKnown { get; } =
        Entries.Select(e => e.Id).ToArray();

    /// <summary>The permanent URI for a well-known namespace, or <see langword="null"/> if not well-known.</summary>
    public static string? GetUri(NamespaceId id)
    {
        foreach (var e in Entries)
        {
            if (e.Id == id) return e.Uri;
        }

        return null;
    }

    /// <summary>The conventional prefix for a well-known namespace, or <see langword="null"/> if not well-known.</summary>
    public static string? GetConventionalPrefix(NamespaceId id)
    {
        foreach (var e in Entries)
        {
            if (e.Id == id) return e.Prefix;
        }

        return null;
    }

    /// <summary>Resolves a URI to its well-known <see cref="NamespaceId"/>, if any.</summary>
    /// <param name="uri">The namespace URI to look up.</param>
    /// <param name="id">The matching well-known id, if found.</param>
    /// <returns><see langword="true"/> if <paramref name="uri"/> is a well-known namespace; otherwise <see langword="false"/>.</returns>
    public static bool TryGetId(string uri, out NamespaceId id)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return ByUri.TryGetValue(uri, out id);
    }
}
