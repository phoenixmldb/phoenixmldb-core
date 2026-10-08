using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// A schema document the host supplies itself: an absolute URI that names it, and its content.
/// </summary>
/// <remarks>
/// A source is how schema text, an embedded resource or a schema held in a store reaches the
/// layer. Its <see cref="Uri"/> is what an <c>xs:include</c> or <c>xs:import</c> elsewhere refers
/// to it by, and the base its own relative references resolve against, so it is always absolute:
/// text with no base URI would resolve its references against the process's current directory,
/// which is not a place a schema was ever said to be. A schema on disk or on the network needs no
/// source; it is named by URI and read through the <see cref="ISchemaAccessGate"/>.
/// </remarks>
public sealed class SchemaSource
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _open;

    private SchemaSource(Uri uri, Func<CancellationToken, ValueTask<Stream>> open, string version)
    {
        Uri = uri;
        _open = open;
        Version = version;
    }

    /// <summary>The absolute URI that names this document.</summary>
    public Uri Uri { get; }

    /// <summary>
    /// A token that changes when the content does: a hash of the text, or the version id of a
    /// stored schema. A compiled schema is reused for as long as every document's token is unchanged.
    /// </summary>
    public string Version { get; }

    internal ValueTask<Stream> OpenAsync(CancellationToken cancellationToken) => _open(cancellationToken);

    /// <summary>Schema text, named by <paramref name="uri"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is not absolute.</exception>
    public static SchemaSource FromText(string schema, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(schema);
        RequireAbsolute(uri);
        var bytes = Encoding.UTF8.GetBytes(schema);
        return new SchemaSource(uri, _ => new ValueTask<Stream>(new MemoryStream(bytes, writable: false)),
            "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>
    /// A schema embedded in <paramref name="assembly"/> as the manifest resource
    /// <paramref name="resourceName"/>, named by <paramref name="uri"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="uri"/> is not absolute, or the assembly has no such resource.
    /// </exception>
    public static SchemaSource FromResource(Assembly assembly, string resourceName, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resourceName);
        RequireAbsolute(uri);
        using (var probe = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException(
                $"Assembly '{assembly.GetName().Name}' has no manifest resource '{resourceName}'.", nameof(resourceName)))
        {
            var hash = Convert.ToHexString(SHA256.HashData(probe));
            return new SchemaSource(uri, _ => new ValueTask<Stream>(assembly.GetManifestResourceStream(resourceName)!), "sha256:" + hash);
        }
    }

    /// <summary>
    /// A schema whose content the host reads on demand, such as one held in a database, named by
    /// <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The absolute URI that names the document.</param>
    /// <param name="open">
    /// Opens the content. Called once for each compilation that needs it, with that compilation's
    /// cancellation token. An exception it throws reaches the caller of the compilation unchanged.
    /// </param>
    /// <param name="version">
    /// A token that changes whenever the content does, such as the stored schema's version id, or
    /// a content hash for a store that keeps no versions.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="uri"/> is not absolute, or <paramref name="version"/> is empty.
    /// </exception>
    public static SchemaSource FromStore(Uri uri, Func<CancellationToken, ValueTask<Stream>> open, string version)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentException.ThrowIfNullOrEmpty(version);
        RequireAbsolute(uri);
        return new SchemaSource(uri, open, version);
    }

    private static void RequireAbsolute(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException(
                $"A schema source needs an absolute URI; '{uri}' is relative. The URI names the document and is the base for the references inside it.",
                nameof(uri));
    }
}
