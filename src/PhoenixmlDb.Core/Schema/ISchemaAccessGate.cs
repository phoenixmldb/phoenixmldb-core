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
/// </remarks>
public interface ISchemaAccessGate
{
    /// <summary>
    /// Identifies the rules this gate applies. Two gates with the same identity must admit the
    /// same documents: a compiled schema is shared between requests whose gates have the same
    /// identity, and not otherwise.
    /// </summary>
    string Identity { get; }

    /// <summary>Opens the document, or refuses it.</summary>
    /// <returns>The document, or null when the gate does not admit it.</returns>
    SchemaDocumentContent? Open(SchemaDocumentRequest request);
}

/// <summary>A schema document the layer needs to read.</summary>
/// <param name="Uri">The absolute URI of the document.</param>
/// <param name="IsRoot">
/// True for a schema the caller asked for; false for one that a schema refers to.
/// </param>
public readonly record struct SchemaDocumentRequest(Uri Uri, bool IsRoot);

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
