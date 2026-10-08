namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// The one route by which a schema document that the host did not supply is read: a root schema
/// named by URI, and every document reached from any schema by <c>xs:include</c>,
/// <c>xs:import</c> or <c>xs:redefine</c>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing in the layer opens a file or a connection itself. Whatever a host's rules are — a
/// resource policy, an allow list, a database's access policy, a server's settings — they are
/// enforced here, in one place, for the whole closure of a schema and not only for its root.
/// </para>
/// <para>
/// A <see cref="SchemaSource"/> registered for a URI is served without asking the gate: the host
/// supplied that content, so there is nothing to fetch.
/// </para>
/// <para>
/// For one compilation each document is opened at most once, so a gate may count what it opens
/// against a budget. An exception a gate throws reaches the caller of the compilation unchanged.
/// </para>
/// </remarks>
public interface ISchemaAccessGate
{
    /// <summary>
    /// Names the rules this gate applies, to keep the compiled schemas of differently configured
    /// gates apart in a cache. It is not what protects a document: a cached schema is given to a
    /// request only after that request's own gate has answered <see cref="GetVersionAsync"/> for
    /// every document in it.
    /// </summary>
    string Identity { get; }

    /// <summary>Opens the document, or refuses it.</summary>
    /// <returns>
    /// The document, or null when the gate does not admit it or it does not exist. The two are
    /// reported alike, so a refusal does not reveal whether the document is there.
    /// </returns>
    ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The current version token of the document, without its content: what
    /// <see cref="OpenAsync"/> would give as <see cref="SchemaDocumentContent.Version"/> now.
    /// </summary>
    /// <returns>The token, or null when the gate does not admit the document or it does not exist.</returns>
    ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken);
}

/// <summary>A schema document the layer needs to read.</summary>
/// <param name="Uri">The absolute URI of the document.</param>
/// <param name="Location">
/// The location as it was written: the <c>schemaLocation</c> of the reference, or for a root the
/// URI the caller gave.
/// </param>
/// <param name="Referrer">The document that refers to this one, or null for a root.</param>
public readonly record struct SchemaDocumentRequest(Uri Uri, string Location, Uri? Referrer)
{
    /// <summary>True for a schema the caller asked for; false for one that a schema refers to.</summary>
    public bool IsRoot => Referrer is null;
}

/// <summary>A schema document as a gate opened it.</summary>
public sealed class SchemaDocumentContent
{
    /// <param name="content">The document. The layer disposes it.</param>
    /// <param name="version">
    /// A token that changes when the content does: for a file its size and modification time,
    /// for an HTTP resource its ETag, for anything else a hash.
    /// </param>
    public SchemaDocumentContent(Stream content, string version)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(version);
        Content = content;
        Version = version;
    }

    /// <summary>The document.</summary>
    public Stream Content { get; }

    /// <summary>A token that changes when the content does.</summary>
    public string Version { get; }
}
