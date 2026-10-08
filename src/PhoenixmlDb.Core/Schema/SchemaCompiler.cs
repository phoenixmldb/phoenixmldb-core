using System.Xml;
using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// Compiles a schema from root documents, reading every document of it through one
/// <see cref="ISchemaAccessGate"/> or from the <see cref="SchemaSource"/> documents the host supplies.
/// </summary>
public static class SchemaCompiler
{
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
    /// <exception cref="SchemaCompilationException">
    /// A document is not admitted, is not well-formed, has a document type declaration or nests
    /// too deeply, or the schema does not compile. <see cref="SchemaCompilationException.Diagnostics"/>
    /// has what was reported.
    /// </exception>
    public static CompiledSchema Compile(IEnumerable<Uri> roots, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(gate);
        var rootList = roots.ToArray();
        if (rootList.Length == 0)
            throw new ArgumentException("At least one root schema is needed.", nameof(roots));
        foreach (var root in rootList)
            if (root is null || !root.IsAbsoluteUri)
                throw new ArgumentException($"A root schema needs an absolute URI; '{root}' is not one.", nameof(roots));

        var reader = new DocumentReader(gate, sources);
        var diagnostics = new List<SchemaDiagnostic>();
        var set = new XmlSchemaSet { XmlResolver = reader };
        set.ValidationEventHandler += (_, e) => diagnostics.Add(SchemaDiagnostic.From(e.Exception, e.Severity));

        // The root documents are parsed here; what they refer to is parsed by the set, through
        // the resolver. Both get the same checks in DocumentReader before a parser sees them.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            foreach (var root in rootList)
            {
                using var content = reader.Read(root, isRoot: true);
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
        catch (SchemaCompilationException ex)
        {
            diagnostics.AddRange(ex.Diagnostics);
        }

        // A referenced document the reader refused is reported by the set as a warning at most,
        // and the schema then compiles without it whenever nothing else needs what it declared.
        // A schema is the whole of what it refers to, so that is a failure here.
        // They go first: what else the compilation reports is mostly their consequence.
        diagnostics.InsertRange(0, reader.Refusals);

        var errors = diagnostics.Where(d => d.Severity == SchemaSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new SchemaCompilationException(
                "The schema could not be compiled: " + errors[0].Message
                + (errors.Length > 1 ? $" (and {errors.Length - 1} more)" : ""),
                diagnostics);

        // Nothing reads through the resolver after compilation; leaving it would keep the gate
        // and the sources alive for as long as the compiled schema is cached.
        set.XmlResolver = null;
        return new CompiledSchema(set, rootList, reader.Documents, diagnostics, gate.Identity);
    }

    private static SchemaCompilationException Refused(SchemaDiagnostic diagnostic)
        => new(diagnostic.Message, [diagnostic]);

    /// <summary>
    /// Reads schema documents for one compilation: from the supplied sources or through the gate,
    /// checked before any schema parser sees them, and recorded with their versions.
    /// </summary>
    private sealed class DocumentReader : XmlResolver
    {
        private readonly ISchemaAccessGate _gate;
        private readonly Dictionary<string, SchemaSource> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SchemaDocumentVersion> _documents = new(StringComparer.Ordinal);
        private readonly List<SchemaDiagnostic> _refusals = [];

        public DocumentReader(ISchemaAccessGate gate, IEnumerable<SchemaSource>? sources)
        {
            _gate = gate;
            foreach (var source in sources ?? [])
            {
                ArgumentNullException.ThrowIfNull(source);
                if (!_sources.TryAdd(source.Uri.AbsoluteUri, source))
                    throw new ArgumentException($"Two schema sources are named '{source.Uri.AbsoluteUri}'.", nameof(sources));
            }
        }

        public IReadOnlyList<SchemaDocumentVersion> Documents => [.. _documents.Values];

        public IReadOnlyList<SchemaDiagnostic> Refusals => _refusals;

        public MemoryStream Read(Uri uri, bool isRoot)
        {
            Stream content;
            string version;
            if (_sources.TryGetValue(uri.AbsoluteUri, out var source))
            {
                content = source.Open();
                version = source.Version;
            }
            else if (_gate.Open(new SchemaDocumentRequest(uri, isRoot)) is { } opened)
            {
                content = opened.Content;
                version = opened.Version;
            }
            else
            {
                throw Refused(new SchemaDiagnostic(SchemaSeverity.Error,
                    $"The schema document '{uri.AbsoluteUri}' is not available: no source supplies it and the access gate does not admit it.",
                    uri.AbsoluteUri));
            }

            var buffer = new MemoryStream();
            using (content)
                content.CopyTo(buffer);
            buffer.Position = 0;
            Check(buffer, uri);
            buffer.Position = 0;
            _documents[uri.AbsoluteUri] = new SchemaDocumentVersion(uri, version);
            return buffer;
        }

        /// <summary>
        /// One linear pass over the markup: no document type declaration, and no deeper than
        /// <see cref="MaxNestingDepth"/>. A document that is not well-formed is left for the
        /// schema parser to report, with its own message and position.
        /// </summary>
        private static void Check(MemoryStream document, Uri uri)
        {
            // Parse, not Ignore: Ignore skips the declaration without reporting it. Nothing is
            // resolved and no entity is expanded, since reading stops at the declaration itself.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, CloseInput = false };
            try
            {
                using var xml = XmlReader.Create(document, settings);
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.DocumentType)
                        throw Refused(new SchemaDiagnostic(SchemaSeverity.Error,
                            "A schema document must not have a document type declaration.", uri.AbsoluteUri));
                    if (xml.NodeType == XmlNodeType.Element && xml.Depth >= MaxNestingDepth)
                        throw Refused(new SchemaDiagnostic(SchemaSeverity.Error,
                            $"The schema document nests elements more than {MaxNestingDepth} levels deep; it is not loaded.",
                            uri.AbsoluteUri));
                }
            }
            catch (XmlException)
            {
            }
        }

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            ArgumentNullException.ThrowIfNull(absoluteUri);
            try
            {
                return Read(absoluteUri, isRoot: false);
            }
            catch (SchemaCompilationException ex)
            {
                _refusals.AddRange(ex.Diagnostics);
                throw new XmlException(ex.Message, ex);
            }
        }
    }
}
