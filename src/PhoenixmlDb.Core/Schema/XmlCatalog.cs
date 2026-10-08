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
/// Every entry of the specification is supported: <c>uri</c>, <c>system</c>, <c>public</c>,
/// <c>rewriteURI</c>, <c>rewriteSystem</c>, <c>uriSuffix</c>, <c>systemSuffix</c>,
/// <c>delegateURI</c>, <c>delegateSystem</c>, <c>delegatePublic</c>, <c>group</c>,
/// <c>nextCatalog</c>, with <c>xml:base</c> and <c>prefer</c> where they apply. Names are
/// compared after the normalization the specification gives (§6.3, §6.4): characters a URI may
/// not hold are percent-encoded, and a <c>urn:publicid:</c> name is read as the public
/// identifier it stands for.
/// </para>
/// <para>
/// A catalog that an entry delegates to is read when the catalog is loaded, with the rest, and
/// not when a name is first looked up: a loaded catalog does not change and reads nothing.
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
        DelegateUri,
        DelegateSystem,
        DelegatePublic,
    }

    /// <summary>
    /// One entry. <paramref name="PreferPublic"/> is the <c>prefer</c> setting in force where a
    /// <c>public</c> or <c>delegatePublic</c> entry stands; a delegate entry has the catalog it
    /// names in <paramref name="Delegated"/> and no target.
    /// </summary>
    private readonly record struct Entry(Kind Kind, string Match, Uri? Target, bool PreferPublic = true,
        List<List<Entry>>? Delegated = null);

    /// <summary>How deep one lookup follows delegation; a catalog that delegates to itself ends there.</summary>
    private const int MaxDelegationDepth = 16;

    // One list for each catalog file, in the order they are consulted: a file's own entries are
    // all tried before any file it names as next.
    private readonly List<List<Entry>> _files;

    private XmlCatalog(List<List<Entry>> files, IReadOnlyList<SchemaDocumentVersion> documents)
    {
        _files = files;
        Documents = documents;
        var text = new StringBuilder();
        Describe(files, text, new HashSet<object>(ReferenceEqualityComparer.Instance));
        Identity = "catalog:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static void Describe(List<List<Entry>> files, StringBuilder text, HashSet<object> described)
    {
        if (!described.Add(files))
        {
            text.Append('\u0004');
            return;
        }
        foreach (var file in files)
        {
            foreach (var entry in file)
            {
                text.Append((int)entry.Kind).Append('\u0001').Append(entry.Match).Append('\u0001')
                    .Append(entry.Target?.AbsoluteUri).Append('\u0001').Append(entry.PreferPublic ? '1' : '0');
                if (entry.Delegated is { } delegated)
                {
                    text.Append('\u0003');
                    Describe(delegated, text, described);
                    text.Append('\u0003');
                }
                text.Append('\n');
            }
            text.Append('\u0002');
        }
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
    /// matching suffix before delegation. A file's entries are all tried before the files it
    /// names as next. A <c>urn:publicid:</c> name is looked up as a public identifier.
    /// </summary>
    public Uri? ResolveUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (UnwrapPublicIdUrn(uri) is { } publicId)
            return ResolvePublic(publicId);
        var name = NormalizeUri(uri);
        return ResolveName(_files, name, uriEntries: true, 0) ?? ResolveName(_files, name, uriEntries: false, 0);
    }

    /// <summary>Where the document with the public identifier <paramref name="publicId"/> is to be read from, or null.</summary>
    public Uri? ResolvePublic(string publicId)
    {
        ArgumentNullException.ThrowIfNull(publicId);
        return ResolvePublicId(_files, NormalizePublicId(UnwrapPublicIdUrn(publicId) ?? publicId), hasSystemId: false, 0);
    }

    /// <summary>
    /// Where an external identifier is to be read from (XML Catalogs §7.1.2), or null. The system
    /// identifier is looked up first. The public identifier is looked up after it, in the
    /// <c>public</c> and <c>delegatePublic</c> entries that <c>prefer="public"</c> is in force
    /// for; when there is no system identifier, in all of them.
    /// </summary>
    /// <param name="publicId">The public identifier, or null.</param>
    /// <param name="systemId">The system identifier, or null.</param>
    public Uri? ResolveExternalIdentifier(string? publicId, string? systemId)
    {
        // A urn:publicid: system identifier is a public identifier, and takes the place of one
        // that says something else (§7.1.1).
        if (systemId is not null && UnwrapPublicIdUrn(systemId) is { } fromSystem)
        {
            publicId = fromSystem;
            systemId = null;
        }
        if (publicId is not null && UnwrapPublicIdUrn(publicId) is { } unwrapped)
            publicId = unwrapped;

        if (systemId is not null && ResolveName(_files, NormalizeUri(systemId), uriEntries: false, 0) is { } bySystem)
            return bySystem;
        return publicId is null ? null : ResolvePublicId(_files, NormalizePublicId(publicId), systemId is not null, 0);
    }

    /// <summary>
    /// One kind of name (URI or system identifier) in one list of catalog files. A file whose
    /// delegate entries match the name ends the lookup: the answer is what the catalogs they
    /// name say, and the files after it are not asked.
    /// </summary>
    private static Uri? ResolveName(List<List<Entry>> files, string name, bool uriEntries, int depth)
    {
        if (depth > MaxDelegationDepth)
            return null;
        var (exact, rewrite, suffix, delegating) = uriEntries
            ? (Kind.Uri, Kind.RewriteUri, Kind.UriSuffix, Kind.DelegateUri)
            : (Kind.System, Kind.RewriteSystem, Kind.SystemSuffix, Kind.DelegateSystem);
        foreach (var file in files)
        {
            if (Match(file, name, exact, rewrite, suffix) is { } found)
                return found;
            var delegates = Delegates(file, name, delegating, requirePreferPublic: false);
            if (delegates.Count == 0)
                continue;
            foreach (var entry in delegates)
                if (ResolveName(entry.Delegated!, name, uriEntries, depth + 1) is { } delegated)
                    return delegated;
            return null;
        }
        return null;
    }

    private static Uri? ResolvePublicId(List<List<Entry>> files, string publicId, bool hasSystemId, int depth)
    {
        if (depth > MaxDelegationDepth)
            return null;
        foreach (var file in files)
        {
            foreach (var entry in file)
                if (entry.Kind == Kind.Public && entry.Match == publicId && (entry.PreferPublic || !hasSystemId))
                    return entry.Target;
            var delegates = Delegates(file, publicId, Kind.DelegatePublic, requirePreferPublic: hasSystemId);
            if (delegates.Count == 0)
                continue;
            // The delegated catalogs are asked about the public identifier alone.
            foreach (var entry in delegates)
                if (ResolvePublicId(entry.Delegated!, publicId, hasSystemId: false, depth + 1) is { } delegated)
                    return delegated;
            return null;
        }
        return null;
    }

    /// <summary>The delegate entries whose prefix the name starts with, the longest prefix first.</summary>
    private static List<Entry> Delegates(List<Entry> file, string name, Kind kind, bool requirePreferPublic)
    {
        var matching = new List<Entry>();
        foreach (var entry in file)
            if (entry.Kind == kind && entry.Delegated is not null && name.StartsWith(entry.Match, StringComparison.Ordinal)
                && (entry.PreferPublic || !requirePreferPublic))
                matching.Add(entry);
        if (matching.Count > 1)
        {
            // A stable order: entries with prefixes of one length stay as they were written.
            matching = matching.Select((entry, index) => (entry, index))
                .OrderByDescending(x => x.entry.Match.Length).ThenBy(x => x.index).Select(x => x.entry).ToList();
        }
        return matching;
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
            return Uri.TryCreate(prefix.Target!.AbsoluteUri + uri[prefix.Match.Length..], UriKind.Absolute, out var rewritten)
                ? rewritten
                : null;
        }

        foreach (var entry in file)
            if (entry.Kind == suffix && uri.EndsWith(entry.Match, StringComparison.Ordinal)
                && (best is null || entry.Match.Length > best.Value.Match.Length))
                best = entry;
        return best?.Target;
    }

    /// <summary>
    /// A system identifier or URI as it is compared (XML Catalogs §6.3): each character a URI
    /// may not hold is replaced by the percent-encoding of its UTF-8 bytes. A percent sign is
    /// left as it is, so a name that is already encoded is not encoded again.
    /// </summary>
    internal static string NormalizeUri(string uri)
    {
        StringBuilder? normalized = null;
        Span<byte> utf8 = stackalloc byte[4];
        for (var i = 0; i < uri.Length; i++)
        {
            var c = uri[i];
            var keep = c is > ' ' and < (char)0x7F && c is not ('"' or '<' or '>' or '\\' or '^' or '`' or '{' or '|' or '}');
            if (keep)
            {
                normalized?.Append(c);
                continue;
            }
            normalized ??= new StringBuilder(uri.Length + 12).Append(uri, 0, i);
            var rune = char.IsHighSurrogate(c) && i + 1 < uri.Length && char.IsLowSurrogate(uri[i + 1])
                ? new Rune(c, uri[++i])
                : Rune.TryCreate(c, out var single) ? single : Rune.ReplacementChar;
            var length = rune.EncodeToUtf8(utf8);
            for (var b = 0; b < length; b++)
                normalized.Append('%').Append(utf8[b].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return normalized?.ToString() ?? uri;
    }

    /// <summary>
    /// The public identifier a <c>urn:publicid:</c> name stands for (XML Catalogs §6.4), or null
    /// when the name is not one.
    /// </summary>
    internal static string? UnwrapPublicIdUrn(string name)
    {
        const string prefix = "urn:publicid:";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var urn = name.AsSpan(prefix.Length);
        var text = new StringBuilder(urn.Length);
        for (var i = 0; i < urn.Length; i++)
        {
            var c = urn[i];
            if (c == '%' && i + 2 < urn.Length)
            {
                var replaced = urn.Slice(i + 1, 2).ToString().ToUpperInvariant() switch
                {
                    "2B" => '+',
                    "3A" => ':',
                    "2F" => '/',
                    "3B" => ';',
                    "27" => '\'',
                    "3F" => '?',
                    "23" => '#',
                    "25" => '%',
                    _ => '\0',
                };
                if (replaced != '\0')
                {
                    text.Append(replaced);
                    i += 2;
                    continue;
                }
            }
            switch (c)
            {
                case '+': text.Append(' '); break;
                case ':': text.Append("//"); break;
                case ';': text.Append("::"); break;
                default: text.Append(c); break;
            }
        }
        return NormalizePublicId(text.ToString());
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
        // Each file is read once. A catalog that entries delegate to is one list of files of its
        // own, shared by every entry that names it (and by itself, where it delegates to itself).
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var delegated = new Dictionary<string, List<List<Entry>>>(StringComparer.Ordinal);
        long total = 0;

        // Depth first, so that a file's next catalogs come right after it and before its siblings.
        async ValueTask LoadOneAsync(SchemaDocumentRequest request, List<List<Entry>> into, HashSet<string> seen)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(request.Uri.AbsoluteUri))
                return; // a catalog that is reached twice, or names itself, is consulted once
            var bytes = await ReadOneAsync(request).ConfigureAwait(false);
            var entries = new List<Entry>();
            into.Add(entries);
            var (next, delegates) = Parse(bytes, request, entries);
            foreach (var (index, target, location) in delegates)
            {
                if (!delegated.TryGetValue(target.AbsoluteUri, out var tree))
                {
                    delegated[target.AbsoluteUri] = tree = [];
                    await LoadOneAsync(new SchemaDocumentRequest(target, location, request.Uri), tree,
                        new HashSet<string>(StringComparer.Ordinal)).ConfigureAwait(false);
                }
                entries[index] = entries[index] with { Delegated = tree };
            }
            foreach (var (nextUri, location) in next)
                await LoadOneAsync(new SchemaDocumentRequest(nextUri, location, request.Uri), into, seen).ConfigureAwait(false);
        }

        async ValueTask<byte[]> ReadOneAsync(SchemaDocumentRequest request)
        {
            if (content.TryGetValue(request.Uri.AbsoluteUri, out var read))
                return read;
            if (content.Count >= MaxCatalogFiles)
                throw Failure($"The catalog is made of more than {MaxCatalogFiles} files; it is not loaded.", request);

            Stream stream;
            string version;
            if (supplied.TryGetValue(request.Uri.AbsoluteUri, out var source))
            {
                stream = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                version = source.Version;
            }
            else if (await gate.OpenAsync(request, cancellationToken).ConfigureAwait(false) is { } opened)
            {
                stream = opened.Content;
                version = opened.Version;
            }
            else
            {
                throw Failure($"The catalog '{request.Location}' is not available: no source supplies it and the access gate does not admit it.", request);
            }

            byte[] bytes;
            await using (stream.ConfigureAwait(false))
            {
                var limit = Math.Min(options.MaxDocumentBytes, options.MaxTotalBytes - total);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                while (true)
                {
                    var count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                    if (count == 0)
                        break;
                    if (buffer.Length + count > limit)
                        throw Failure($"The catalog '{request.Location}' is larger than the limit for what one load may read.", request);
                    await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                }
                bytes = buffer.ToArray();
            }
            total += bytes.Length;
            documents.Add(new SchemaDocumentVersion(request.Uri, version));
            content[request.Uri.AbsoluteUri] = bytes;
            return bytes;
        }

        var top = new HashSet<string>(StringComparer.Ordinal);

        foreach (var catalog in catalogs)
        {
            if (catalog is null || !catalog.IsAbsoluteUri)
                throw new ArgumentException($"A catalog needs an absolute URI; '{catalog}' is not one.", nameof(catalogs));
            await LoadOneAsync(new SchemaDocumentRequest(catalog, catalog.OriginalString, null), files, top).ConfigureAwait(false);
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

    /// <summary>
    /// Reads one catalog file's entries into <paramref name="entries"/>, and returns the catalogs
    /// it names as next and, for each delegate entry (by its index), the catalog it names.
    /// </summary>
    private static (List<(Uri Uri, string Location)> Next, List<(int Index, Uri Uri, string Location)> Delegates) Parse(
        byte[] document, SchemaDocumentRequest request, List<Entry> entries)
    {
        var next = new List<(Uri, string)>();
        var delegates = new List<(int, Uri, string)>();
        // The declaration most catalog files carry is skipped, with nothing fetched for it.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        // What is in force at each depth: xml:base and prefer, on the catalog, a group or an entry.
        var scopes = new Stack<(int Depth, Uri Base, bool PreferPublic)>();
        scopes.Push((-1, request.Uri, true));
        try
        {
            using var xml = XmlReader.Create(new MemoryStream(document, writable: false), settings);
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element)
                    continue;
                if (xml.Depth >= SchemaCompiler.MaxNestingDepth)
                    throw Failure($"The catalog '{request.Location}' nests elements too deeply; it is not loaded.", request);
                while (scopes.Peek().Depth >= xml.Depth)
                    scopes.Pop();
                var (_, baseUri, preferPublic) = scopes.Peek();
                if (xml.GetAttribute("base", XmlNamespace) is { Length: > 0 } declared
                    && Uri.TryCreate(baseUri, declared, out var rebased))
                    baseUri = rebased;
                if (xml.NamespaceURI == CatalogNamespace && xml.LocalName is "catalog" or "group"
                    && xml.GetAttribute("prefer") is { } prefer)
                    preferPublic = prefer != "system";
                if (!xml.IsEmptyElement)
                    scopes.Push((xml.Depth, baseUri, preferPublic));
                if (xml.NamespaceURI != CatalogNamespace)
                    continue;

                Uri? Target(string attribute)
                    => xml.GetAttribute(attribute) is { Length: > 0 } value && Uri.TryCreate(baseUri, value, out var target)
                        ? target
                        : null;
                string Name(Kind kind, string match)
                    => kind is Kind.Public or Kind.DelegatePublic ? NormalizePublicId(match) : NormalizeUri(match);
                void Add(Kind kind, string matchAttribute, string targetAttribute)
                {
                    if (xml.GetAttribute(matchAttribute) is not { Length: > 0 } match || Target(targetAttribute) is not { } target)
                        return;
                    // A name written as a urn:publicid: is an entry for that public identifier (§6.4).
                    if (kind is Kind.System or Kind.Uri && UnwrapPublicIdUrn(match) is { } publicId)
                        entries.Add(new Entry(Kind.Public, publicId, target, preferPublic));
                    else
                        entries.Add(new Entry(kind, Name(kind, match), target, preferPublic));
                }
                void Delegate(Kind kind, string matchAttribute)
                {
                    if (xml.GetAttribute(matchAttribute) is not { Length: > 0 } match
                        || xml.GetAttribute("catalog") is not { Length: > 0 } location || Target("catalog") is not { } target)
                        return;
                    delegates.Add((entries.Count, target, location));
                    entries.Add(new Entry(kind, Name(kind, match), null, preferPublic));
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
                    case "delegateURI": Delegate(Kind.DelegateUri, "uriStartString"); break;
                    case "delegateSystem": Delegate(Kind.DelegateSystem, "systemIdStartString"); break;
                    case "delegatePublic": Delegate(Kind.DelegatePublic, "publicIdStartString"); break;
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
        return (next, delegates);
    }
}
