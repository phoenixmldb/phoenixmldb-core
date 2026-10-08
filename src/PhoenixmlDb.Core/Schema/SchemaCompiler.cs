using System.Xml;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// Compiles a schema from root documents, reading every document of it through one
/// <see cref="ISchemaAccessGate"/> or from the <see cref="SchemaSource"/> documents the host supplies.
/// </summary>
/// <remarks>
/// A compilation has two steps. First the whole closure is read: the roots, then every document
/// they include, import or redefine, and so on, each one once, each through the gate or from a
/// source, within the limits of <see cref="SchemaCompileOptions"/>. Then the schema is compiled
/// from what was read, in memory. Nothing is fetched during the second step: a document the
/// schema compiler asks for that the first step did not read fails the compilation.
/// </remarks>
public static class SchemaCompiler
{
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";

    /// <summary>
    /// How deeply a schema document's elements may nest. Real schemas stay well under a hundred
    /// levels, and the schema loader slows sharply with depth and cannot be cancelled, so a small
    /// document nested tens of thousands of levels deep could occupy a host for minutes.
    /// </summary>
    public const int MaxNestingDepth = 512;

    /// <summary>Compiles the schema rooted at <paramref name="roots"/>.</summary>
    /// <param name="roots">The absolute URIs of the root documents.</param>
    /// <param name="gate">Reads every document that <paramref name="sources"/> does not supply.</param>
    /// <param name="sources">Documents the host supplies, each named by its URI.</param>
    /// <param name="options">Limits on what is read; <see cref="SchemaCompileOptions.Default"/> when null.</param>
    /// <param name="cancellationToken">
    /// Given to the gate and the sources, and looked at between documents. The compile step
    /// itself, once everything is read, cannot be interrupted.
    /// </param>
    /// <exception cref="SchemaCompilationException">
    /// A document is not available, is too large, is not well-formed, has a document type
    /// declaration or nests too deeply, or the schema does not compile.
    /// <see cref="SchemaCompilationException.Diagnostics"/> has what was reported.
    /// </exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<CompiledSchema> CompileAsync(IEnumerable<Uri> roots, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(gate);
        var rootList = roots.ToArray();
        if (rootList.Length == 0)
            throw new ArgumentException("At least one root schema is needed.", nameof(roots));
        foreach (var root in rootList)
            if (root is null || !root.IsAbsoluteUri)
                throw new ArgumentException($"A root schema needs an absolute URI; '{root}' is not one.", nameof(roots));

        var closure = new Closure(gate, sources, options ?? SchemaCompileOptions.Default);
        await closure.ReadAsync(rootList, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<SchemaDiagnostic>(closure.Errors);
        if (diagnostics.Count == 0)
            Build(rootList, closure, diagnostics);

        var errors = diagnostics.Where(d => d.Severity == SchemaSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new SchemaCompilationException(
                "The schema could not be compiled: " + errors[0].Message
                + (errors.Length > 1 ? $" (and {errors.Length - 1} more)" : ""),
                diagnostics);
        return new CompiledSchema(closure.Set!, rootList, closure.Documents, diagnostics, gate.Identity, closure.Requests);
    }

    /// <summary>
    /// <see cref="CompileAsync"/> for a caller that cannot wait asynchronously. It blocks the
    /// calling thread until the gate and the sources have answered, so it is for hosts whose gate
    /// and sources answer at once (files, text, resources) or that may block a thread.
    /// </summary>
    public static CompiledSchema Compile(IEnumerable<Uri> roots, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? options = null)
    {
        var pending = CompileAsync(roots, gate, sources, options, CancellationToken.None);
        return pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The second step: compile from the documents that were read, fetching nothing.</summary>
    private static void Build(Uri[] roots, Closure closure, List<SchemaDiagnostic> diagnostics)
    {
        var resolver = new LoadedOnlyResolver(closure);
        var set = new XmlSchemaSet { XmlResolver = resolver };
        set.ValidationEventHandler += (_, e) => diagnostics.Add(SchemaDiagnostic.From(e.Exception, e.Severity));
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            // The roots, where the catalog maps them to; then what the catalog gave for an import
            // that names only a namespace, which the schema compiler has no location to ask for.
            foreach (var root in roots.Select(closure.Map).Concat(closure.ImportedByNamespace).DistinctBy(u => u.AbsoluteUri))
            {
                using var content = closure.Content(root)!;
                using var xml = XmlReader.Create(content, settings, root.AbsoluteUri);
                set.Add(null, xml);
            }
            set.Compile();
        }
        catch (XmlSchemaException ex)
        {
            diagnostics.Add(SchemaDiagnostic.From(ex, XmlSeverityType.Error));
        }
        catch (XmlException ex)
        {
            diagnostics.Add(new SchemaDiagnostic(SchemaSeverity.Error, ex.Message,
                string.IsNullOrEmpty(ex.SourceUri) ? null : ex.SourceUri, ex.LineNumber, ex.LinePosition));
        }
        // What the compiler asked for and was not given goes first: the rest is mostly its consequence.
        diagnostics.InsertRange(0, resolver.NotLoaded);
        // Nothing reads through the resolver after compilation; leaving it would keep every
        // document's bytes alive for as long as the compiled schema is cached.
        set.XmlResolver = null;
        closure.Set = set;
    }

    /// <summary>The first step: the documents of one compilation, each read once.</summary>
    private sealed class Closure
    {
        private readonly ISchemaAccessGate _gate;
        private readonly SchemaCompileOptions _options;
        private readonly Dictionary<string, SchemaSource> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _content = new(StringComparer.Ordinal);
        private readonly List<SchemaDocumentVersion> _documents = [];
        private readonly Dictionary<string, SchemaDocumentRequest> _requests = new(StringComparer.Ordinal);
        private long _totalBytes;

        public Closure(ISchemaAccessGate gate, IEnumerable<SchemaSource>? sources, SchemaCompileOptions options)
        {
            _gate = gate;
            _options = options;
            foreach (var source in sources ?? [])
            {
                ArgumentNullException.ThrowIfNull(source);
                if (!_sources.TryAdd(source.Uri.AbsoluteUri, source))
                    throw new ArgumentException($"Two schema sources are named '{source.Uri.AbsoluteUri}'.", nameof(sources));
            }
        }

        public List<SchemaDiagnostic> Errors { get; } = [];

        public IReadOnlyList<SchemaDocumentVersion> Documents => _documents;

        public IReadOnlyDictionary<string, SchemaDocumentRequest> Requests => _requests;

        public XmlSchemaSet? Set { get; set; }

        public MemoryStream? Content(Uri uri)
            => _content.TryGetValue(uri.AbsoluteUri, out var bytes) ? new MemoryStream(bytes, writable: false) : null;

        /// <summary>Documents the catalog gave for an import that names a namespace and no location.</summary>
        public List<Uri> ImportedByNamespace { get; } = [];

        /// <summary>Where the catalog says the document is read from; the URI itself when it says nothing.</summary>
        public Uri Map(Uri uri) => _options.Catalog?.ResolveUri(uri.AbsoluteUri) ?? uri;

        /// <summary>
        /// The document a reference names: the catalog is asked about the location as it is
        /// written, then about the absolute URI it resolves to against <paramref name="baseUri"/>.
        /// Used for reading the closure and again by the schema compiler, so the two agree.
        /// </summary>
        public Uri? Resolve(Uri baseUri, string location)
        {
            if (_options.Catalog?.ResolveUri(location) is { } asWritten)
                return asWritten;
            return Uri.TryCreate(baseUri, location, out var target) ? Map(target) : null;
        }

        public async ValueTask ReadAsync(Uri[] roots, CancellationToken cancellationToken)
        {
            var pending = new Queue<SchemaDocumentRequest>();
            var asked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in roots)
            {
                var mapped = Map(root);
                if (asked.Add(mapped.AbsoluteUri))
                    pending.Enqueue(new SchemaDocumentRequest(mapped, root.OriginalString, null));
            }

            while (pending.TryDequeue(out var request))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_documents.Count >= _options.MaxDocuments)
                {
                    Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                        $"The schema is made of more than {_options.MaxDocuments} documents; it is not loaded."));
                    return;
                }
                var bytes = await ReadOneAsync(request, cancellationToken).ConfigureAwait(false);
                if (bytes is null)
                    continue;
                // Checked as it was read; then, where it is written for XSD 1.1, as a 1.0 processor
                // is to see it. The references are taken from that second form: an include that
                // the document marks as 1.1-only is not part of this schema and is not fetched.
                var references = Check(bytes, request);
                if (_options.Xsd11Compatibility && Xsd11Compatibility.Apply(bytes) is var rewritten && !ReferenceEquals(rewritten, bytes))
                {
                    bytes = rewritten;
                    references = Check(bytes, request);
                }
                _content[request.Uri.AbsoluteUri] = bytes;
                foreach (var (location, importedNamespace) in references)
                {
                    if (location is null)
                    {
                        // An import with a namespace and no location: the catalog may know one.
                        if (_options.Catalog?.ResolveUri(importedNamespace!) is { } byNamespace)
                        {
                            if (!ImportedByNamespace.Any(u => u.AbsoluteUri == byNamespace.AbsoluteUri))
                                ImportedByNamespace.Add(byNamespace);
                            if (asked.Add(byNamespace.AbsoluteUri))
                                pending.Enqueue(new SchemaDocumentRequest(byNamespace, importedNamespace!, request.Uri));
                        }
                        continue;
                    }
                    if (Resolve(request.Uri, location) is not { } target)
                    {
                        Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                            $"The schema location '{location}' is not a URI.", request.Uri.AbsoluteUri));
                        continue;
                    }
                    if (asked.Add(target.AbsoluteUri))
                        pending.Enqueue(new SchemaDocumentRequest(target, location, request.Uri));
                }
            }
        }

        private async ValueTask<byte[]?> ReadOneAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            Stream content;
            string version;
            if (_sources.TryGetValue(request.Uri.AbsoluteUri, out var source))
            {
                content = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                version = source.Version;
            }
            else if (await _gate.OpenAsync(request, cancellationToken).ConfigureAwait(false) is { } opened)
            {
                content = opened.Content;
                version = opened.Version;
            }
            else
            {
                // The location as it was written, and the document that wrote it: nothing a gate
                // resolved it to. Refused and absent read the same.
                Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                    $"The schema document '{request.Location}' is not available: no source supplies it and the access gate does not admit it.",
                    request.Referrer?.AbsoluteUri));
                return null;
            }

            await using (content.ConfigureAwait(false))
            {
                // Never more than the limit is taken from a stream, whatever it claims to hold.
                var limit = Math.Min(_options.MaxDocumentBytes, _options.MaxTotalBytes - _totalBytes);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                while (true)
                {
                    var read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    if (buffer.Length + read > limit)
                    {
                        Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                            buffer.Length + read > _options.MaxDocumentBytes
                                ? $"The schema document '{request.Location}' is larger than the limit of {_options.MaxDocumentBytes} bytes for one document."
                                : $"The schema's documents come to more than the limit of {_options.MaxTotalBytes} bytes.",
                            request.Referrer?.AbsoluteUri));
                        return null;
                    }
                    await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                _totalBytes += buffer.Length;
                _documents.Add(new SchemaDocumentVersion(request.Uri, version));
                _requests[request.Uri.AbsoluteUri] = request;
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// One linear pass over a document's markup, before any schema parser sees it: no document
        /// type declaration, no deeper than <see cref="MaxNestingDepth"/>, and the locations it
        /// refers to. A document that is not well-formed is left for the schema parser to report,
        /// with its own message and position.
        /// </summary>
        private List<(string? Location, string? Namespace)> Check(byte[] document, SchemaDocumentRequest request)
        {
            var locations = new List<(string?, string?)>();
            // Parse, not Ignore: Ignore skips the declaration without reporting it. Nothing is
            // resolved and no entity is expanded, since reading stops at the declaration itself.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
            try
            {
                using var xml = XmlReader.Create(new MemoryStream(document, writable: false), settings);
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.DocumentType)
                    {
                        Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                            $"The schema document '{request.Location}' has a document type declaration, which a schema document must not have.",
                            request.Uri.AbsoluteUri));
                        return [];
                    }
                    if (xml.NodeType != XmlNodeType.Element)
                        continue;
                    if (xml.Depth >= MaxNestingDepth)
                    {
                        Errors.Add(new SchemaDiagnostic(SchemaSeverity.Error,
                            $"The schema document '{request.Location}' nests elements more than {MaxNestingDepth} levels deep; it is not loaded.",
                            request.Uri.AbsoluteUri));
                        return [];
                    }
                    // References are children of xs:schema.
                    if (xml.Depth == 1 && xml.NamespaceURI == XsdNamespace
                        && xml.LocalName is "include" or "import" or "redefine")
                    {
                        if (xml.GetAttribute("schemaLocation") is { Length: > 0 } location)
                            locations.Add((location, null));
                        else if (xml.LocalName == "import" && xml.GetAttribute("namespace") is { Length: > 0 } importedNamespace)
                            locations.Add((null, importedNamespace));
                    }
                }
            }
            catch (XmlException)
            {
            }
            return locations;
        }
    }

    /// <summary>Gives the schema compiler the documents that were read, and nothing else.</summary>
    private sealed class LoadedOnlyResolver(Closure closure) : XmlResolver
    {
        public List<SchemaDiagnostic> NotLoaded { get; } = [];

        // The same resolution the closure was read by, catalog included, so that the schema
        // compiler asks for a document under the name it was read under, and takes that name as
        // the base for the references inside it.
        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
            => baseUri is not null && relativeUri is not null && closure.Resolve(baseUri, relativeUri) is { } resolved
                ? resolved
                : base.ResolveUri(baseUri, relativeUri);

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            ArgumentNullException.ThrowIfNull(absoluteUri);
            if (closure.Content(absoluteUri) is { } content)
                return content;
            var diagnostic = new SchemaDiagnostic(SchemaSeverity.Error,
                $"The schema refers to '{absoluteUri.AbsoluteUri}', which was not read with the rest of the schema; nothing is fetched while compiling.");
            NotLoaded.Add(diagnostic);
            throw new XmlException(diagnostic.Message);
        }
    }
}
