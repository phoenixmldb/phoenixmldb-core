using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// An OASIS XML Catalog (XML Catalogs 1.1): a table that says where the document a URI names is
/// really to be read from. A schema that refers to <c>http://www.w3.org/2001/xml.xsd</c> can so be
/// compiled from a local copy, with nothing fetched from the network.
/// </summary>
/// <remarks>
/// <para>
/// Supported entries: <c>uri</c>, <c>system</c>, <c>public</c>, <c>rewriteURI</c>,
/// <c>rewriteSystem</c>, <c>uriSuffix</c>, <c>systemSuffix</c>, <c>group</c>, <c>nextCatalog</c>,
/// and <c>xml:base</c> on any of them. The delegate entries are not supported and are ignored.
/// </para>
/// <para>
/// A catalog only renames. What it maps a URI to is then read like any other document, from a
/// supplied <see cref="SchemaSource"/> or through the <see cref="ISchemaAccessGate"/>, which sees
/// the mapped URI. The catalog files themselves, and every <c>nextCatalog</c>, are read the same
/// way when the catalog is loaded; one that cannot be read fails the load.
/// </para>
/// <para>
/// A catalog file may have a document type declaration, as most do. It is skipped: nothing is
/// fetched for it and no entity it declares is used.
/// </para>
/// </remarks>
public sealed class XmlCatalog
{
    private const string CatalogNamespace = "urn:oasis:names:tc:entity:xmlns:xml:catalog";
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The most catalog files one load reads, through <c>nextCatalog</c>.</summary>
    public const int MaxCatalogFiles = 64;

    private enum Kind
    {
        Uri,
        System,
        Public,
        RewriteUri,
        RewriteSystem,
        UriSuffix,
        SystemSuffix,
    }

    private readonly record struct Entry(Kind Kind, string Match, Uri Target);

    // One list for each catalog file, in the order they are consulted: a file's own entries are
    // all tried before any file it names as next.
    private readonly List<List<Entry>> _files;

    private XmlCatalog(List<List<Entry>> files, IReadOnlyList<SchemaDocumentVersion> documents)
    {
        _files = files;
        Documents = documents;
        var text = new StringBuilder();
        foreach (var file in files)
        {
            foreach (var entry in file)
                text.Append((int)entry.Kind).Append('\u0001').Append(entry.Match).Append('\u0001').Append(entry.Target.AbsoluteUri).Append('\n');
            text.Append('\u0002');
        }
        Identity = "catalog:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>A catalog with no entries.</summary>
    public static XmlCatalog Empty { get; } = new([], []);

    /// <summary>
    /// Names what the catalog says, derived from its entries: two catalogs with the same entries
    /// in the same order have the same identity. Part of what a cached schema is found by.
    /// </summary>
    public string Identity { get; }

    /// <summary>The catalog files that were read, each with the version read.</summary>
    public IReadOnlyList<SchemaDocumentVersion> Documents { get; }

    /// <summary>
    /// Where the document named by <paramref name="uri"/> is to be read from, or null when the
    /// catalog says nothing about it. The <c>uri</c> entries are tried, then the <c>system</c>
    /// ones; in each, an exact name before the longest matching rewrite prefix before the longest
    /// matching suffix. A file's entries are all tried before the files it names as next.
    /// </summary>
    public Uri? ResolveUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        foreach (var file in _files)
        {
            if (Match(file, uri, Kind.Uri, Kind.RewriteUri, Kind.UriSuffix) is { } byUri)
                return byUri;
            if (Match(file, uri, Kind.System, Kind.RewriteSystem, Kind.SystemSuffix) is { } bySystem)
                return bySystem;
        }
        return null;
    }

    /// <summary>Where the document with the public identifier <paramref name="publicId"/> is to be read from, or null.</summary>
    public Uri? ResolvePublic(string publicId)
    {
        ArgumentNullException.ThrowIfNull(publicId);
        var normalized = NormalizePublicId(publicId);
        foreach (var file in _files)
            foreach (var entry in file)
                if (entry.Kind == Kind.Public && entry.Match == normalized)
                    return entry.Target;
        return null;
    }

    private static Uri? Match(List<Entry> file, string uri, Kind exact, Kind rewrite, Kind suffix)
    {
        foreach (var entry in file)
            if (entry.Kind == exact && entry.Match == uri)
                return entry.Target;

        Entry? best = null;
        foreach (var entry in file)
            if (entry.Kind == rewrite && uri.StartsWith(entry.Match, StringComparison.Ordinal)
                && (best is null || entry.Match.Length > best.Value.Match.Length))
                best = entry;
        if (best is { } prefix)
        {
            // The rest of the URI is appended to the rewrite target as written.
            return Uri.TryCreate(prefix.Target.AbsoluteUri + uri[prefix.Match.Length..], UriKind.Absolute, out var rewritten)
                ? rewritten
                : null;
        }

        foreach (var entry in file)
            if (entry.Kind == suffix && uri.EndsWith(entry.Match, StringComparison.Ordinal)
                && (best is null || entry.Match.Length > best.Value.Match.Length))
                best = entry;
        return best?.Target;
    }

    /// <summary>Public identifiers compare with their white space normalized (XML Catalogs §6.2).</summary>
    private static string NormalizePublicId(string publicId)
        => string.Join(' ', publicId.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Loads the catalog files at <paramref name="catalogs"/> and every catalog they name as next.</summary>
    /// <param name="catalogs">The absolute URIs of the catalog files, in the order they are consulted.</param>
    /// <param name="gate">Reads every catalog file that <paramref name="sources"/> does not supply.</param>
    /// <param name="sources">Catalog files the host supplies, each named by its URI.</param>
    /// <param name="limits">The size limits for what is read; <see cref="SchemaCompileOptions.Default"/> when null.</param>
    /// <param name="cancellationToken">Given to the gate and the sources.</param>
    /// <exception cref="SchemaCompilationException">
    /// A catalog file is not available, is too large, is not well-formed, or there are more than
    /// <see cref="MaxCatalogFiles"/> of them.
    /// </exception>
    public static async ValueTask<XmlCatalog> LoadAsync(IEnumerable<Uri> catalogs, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(gate);
        var options = limits ?? SchemaCompileOptions.Default;
        var supplied = new Dictionary<string, SchemaSource>(StringComparer.Ordinal);
        foreach (var source in sources ?? [])
            supplied[source.Uri.AbsoluteUri] = source;

        var files = new List<List<Entry>>();
        var documents = new List<SchemaDocumentVersion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;

        // Depth first, so that a file's next catalogs come right after it and before its siblings.
        async ValueTask LoadOneAsync(SchemaDocumentRequest request)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(request.Uri.AbsoluteUri))
                return; // a catalog that is reached twice, or names itself, is consulted once
            if (seen.Count > MaxCatalogFiles)
                throw Failure($"The catalog is made of more than {MaxCatalogFiles} files; it is not loaded.", request);

            Stream content;
            string version;
            if (supplied.TryGetValue(request.Uri.AbsoluteUri, out var source))
            {
                content = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                version = source.Version;
            }
            else if (await gate.OpenAsync(request, cancellationToken).ConfigureAwait(false) is { } opened)
            {
                content = opened.Content;
                version = opened.Version;
            }
            else
            {
                throw Failure($"The catalog '{request.Location}' is not available: no source supplies it and the access gate does not admit it.", request);
            }

            byte[] bytes;
            await using (content.ConfigureAwait(false))
            {
                var limit = Math.Min(options.MaxDocumentBytes, options.MaxTotalBytes - total);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                while (true)
                {
                    var read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    if (buffer.Length + read > limit)
                        throw Failure($"The catalog '{request.Location}' is larger than the limit for what one load may read.", request);
                    await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                bytes = buffer.ToArray();
            }
            total += bytes.Length;
            documents.Add(new SchemaDocumentVersion(request.Uri, version));

            var entries = new List<Entry>();
            files.Add(entries);
            foreach (var next in Parse(bytes, request, entries))
                await LoadOneAsync(new SchemaDocumentRequest(next.Uri, next.Location, request.Uri)).ConfigureAwait(false);
        }

        foreach (var catalog in catalogs)
        {
            if (catalog is null || !catalog.IsAbsoluteUri)
                throw new ArgumentException($"A catalog needs an absolute URI; '{catalog}' is not one.", nameof(catalogs));
            await LoadOneAsync(new SchemaDocumentRequest(catalog, catalog.OriginalString, null)).ConfigureAwait(false);
        }
        return new XmlCatalog(files, documents);
    }

    /// <summary>
    /// <see cref="LoadAsync"/> for a caller that cannot wait asynchronously; it blocks the calling
    /// thread until the gate and the sources have answered.
    /// </summary>
    public static XmlCatalog Load(IEnumerable<Uri> catalogs, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? limits = null)
    {
        var pending = LoadAsync(catalogs, gate, sources, limits, CancellationToken.None);
        return pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
    }

    private static SchemaCompilationException Failure(string message, SchemaDocumentRequest request)
        => new(message, [new SchemaDiagnostic(SchemaSeverity.Error, message, request.Referrer?.AbsoluteUri)]);

    /// <summary>Reads one catalog file's entries into <paramref name="entries"/>, and returns the catalogs it names as next.</summary>
    private static List<(Uri Uri, string Location)> Parse(byte[] document, SchemaDocumentRequest request, List<Entry> entries)
    {
        var next = new List<(Uri, string)>();
        // The declaration most catalog files carry is skipped, with nothing fetched for it.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        // The base in force at each depth: xml:base on the catalog, a group or an entry.
        var bases = new Stack<(int Depth, Uri Base)>();
        bases.Push((-1, request.Uri));
        try
        {
            using var xml = XmlReader.Create(new MemoryStream(document, writable: false), settings);
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element)
                    continue;
                if (xml.Depth >= SchemaCompiler.MaxNestingDepth)
                    throw Failure($"The catalog '{request.Location}' nests elements too deeply; it is not loaded.", request);
                while (bases.Peek().Depth >= xml.Depth)
                    bases.Pop();
                var baseUri = bases.Peek().Base;
                if (xml.GetAttribute("base", XmlNamespace) is { Length: > 0 } declared
                    && Uri.TryCreate(baseUri, declared, out var rebased))
                    baseUri = rebased;
                if (!xml.IsEmptyElement)
                    bases.Push((xml.Depth, baseUri));
                if (xml.NamespaceURI != CatalogNamespace)
                    continue;

                Uri? Target(string attribute)
                    => xml.GetAttribute(attribute) is { Length: > 0 } value && Uri.TryCreate(baseUri, value, out var target)
                        ? target
                        : null;
                void Add(Kind kind, string matchAttribute, string targetAttribute)
                {
                    if (xml.GetAttribute(matchAttribute) is { Length: > 0 } match && Target(targetAttribute) is { } target)
                        entries.Add(new Entry(kind, kind == Kind.Public ? NormalizePublicId(match) : match, target));
                }

                switch (xml.LocalName)
                {
                    case "uri": Add(Kind.Uri, "name", "uri"); break;
                    case "system": Add(Kind.System, "systemId", "uri"); break;
                    case "public": Add(Kind.Public, "publicId", "uri"); break;
                    case "rewriteURI": Add(Kind.RewriteUri, "uriStartString", "rewritePrefix"); break;
                    case "rewriteSystem": Add(Kind.RewriteSystem, "systemIdStartString", "rewritePrefix"); break;
                    case "uriSuffix": Add(Kind.UriSuffix, "uriSuffix", "uri"); break;
                    case "systemSuffix": Add(Kind.SystemSuffix, "systemIdSuffix", "uri"); break;
                    case "nextCatalog":
                        if (xml.GetAttribute("catalog") is { Length: > 0 } location && Uri.TryCreate(baseUri, location, out var nextUri))
                            next.Add((nextUri, location));
                        break;
                }
            }
        }
        catch (XmlException ex)
        {
            throw new SchemaCompilationException($"The catalog '{request.Location}' is not well-formed: {ex.Message}",
                [new SchemaDiagnostic(SchemaSeverity.Error, ex.Message, request.Uri.AbsoluteUri, ex.LineNumber, ex.LinePosition)], ex);
        }
        return next;
    }
}
