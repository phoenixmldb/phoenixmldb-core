using System.Globalization;

namespace PhoenixmlDb.Core.Schema;

/// <summary>The gates the layer provides.</summary>
public static class SchemaAccessGate
{
    /// <summary>
    /// Admits nothing: a schema can be made only of the <see cref="SchemaSource"/> documents the
    /// host supplies. A reference to anything else fails.
    /// </summary>
    public static ISchemaAccessGate SuppliedOnly { get; } = new SuppliedOnlyGate();

    /// <summary>
    /// Admits <c>file:</c> documents under one of <paramref name="roots"/>, and nothing else. A
    /// path is compared after it is made absolute and its <c>..</c> segments are resolved, so a
    /// reference cannot step out of a root; a link inside a root that points outside it is not
    /// followed back, and is the host's to rule out where that matters.
    /// </summary>
    /// <exception cref="ArgumentException">No root was given.</exception>
    public static ISchemaAccessGate LocalFiles(params string[] roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Length == 0)
            throw new ArgumentException("At least one root directory is needed.", nameof(roots));
        return new LocalFilesGate(roots);
    }

    private sealed class SuppliedOnlyGate : ISchemaAccessGate
    {
        public string Identity => "supplied-only";
        public SchemaDocumentContent? Open(SchemaDocumentRequest request) => null;
    }

    private sealed class LocalFilesGate : ISchemaAccessGate
    {
        private readonly string[] _roots;

        public LocalFilesGate(string[] roots)
        {
            _roots = roots
                .Select(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)) + Path.DirectorySeparatorChar)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToArray();
            Identity = "local-files:" + string.Join("|", _roots);
        }

        public string Identity { get; }

        public SchemaDocumentContent? Open(SchemaDocumentRequest request)
        {
            if (!request.Uri.IsFile)
                return null;
            var path = Path.GetFullPath(request.Uri.LocalPath);
            if (!_roots.Any(root => path.StartsWith(root, StringComparison.Ordinal)))
                return null;
            FileStream stream;
            try
            {
#pragma warning disable CA2000 // the stream is handed to the caller inside the returned content
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
#pragma warning restore CA2000
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            // Size and time of the file that was opened, not of the path: the two can differ if
            // the file is replaced between the open and the query.
            var info = new FileInfo(stream.Name);
            var version = string.Create(CultureInfo.InvariantCulture,
                $"file:{stream.Length}:{info.LastWriteTimeUtc.Ticks}");
            return new SchemaDocumentContent(stream, version);
        }
    }
}
