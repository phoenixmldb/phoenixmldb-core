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
    /// Admits <c>file:</c> documents under one of <paramref name="roots"/>, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The roots must be writable only by the operator.</b> Someone who can write inside a root
    /// can place there whatever a schema is then compiled from.
    /// </para>
    /// <para>
    /// A file is opened once, and admitted by the path of the open file, as the operating system
    /// reports it with every link resolved (Linux and Windows). On a platform where the layer
    /// cannot ask that, a path with a symbolic link in any component below its root is refused
    /// instead, looked at before the open and again after it.
    /// </para>
    /// </remarks>
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

        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
            => default;

        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
            => default;
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

        /// <summary>The root the path is under by name, with the path, or null.</summary>
        private (string Root, string Path)? UnderRootByName(SchemaDocumentRequest request)
        {
            if (!request.Uri.IsFile)
                return null;
            var path = Path.GetFullPath(request.Uri.LocalPath);
            foreach (var root in _roots)
                if (path.StartsWith(root, StringComparison.Ordinal))
                    return (root, path);
            return null;
        }

        /// <summary>
        /// Opens the file the request names if it is admitted. Both <see cref="OpenAsync"/> and
        /// <see cref="GetVersionAsync"/> go through here, so they decide alike.
        /// </summary>
        private FileStream? OpenAdmitted(SchemaDocumentRequest request)
        {
            if (UnderRootByName(request) is not var (root, path))
                return null;
            FileStream stream;
            try
            {
#pragma warning disable CA2000 // returned to the caller, or disposed below when not admitted
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
            // Decide on the file that is open, not on the name it was opened by. Where the
            // platform cannot say which file that is, refuse a link anywhere below the root.
            var admitted = OpenFilePath.TryGet(stream) is { } actual
                ? _roots.Any(r => actual.StartsWith(r, StringComparison.Ordinal))
                : !HasLinkBelow(root, path);
            if (admitted)
                return stream;
            stream.Dispose();
            return null;
        }

        /// <summary>Whether any component of <paramref name="path"/> below <paramref name="root"/> is a link.</summary>
        private static bool HasLinkBelow(string root, string path)
        {
            try
            {
                for (var current = path;
                     current is not null && current.Length > root.Length - 1;
                     current = Path.GetDirectoryName(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        return true;
                }
                return false;
            }
            catch (IOException)
            {
                return true; // not there, or not readable: nothing to admit
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static string VersionOf(long length, DateTime lastWriteUtc)
            => string.Create(CultureInfo.InvariantCulture, $"file:{length}:{lastWriteUtc.Ticks}");

        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CA2000 // the stream is handed to the caller inside the returned content
            var stream = OpenAdmitted(request);
#pragma warning restore CA2000
            return stream is null
                ? default
                : new(new SchemaDocumentContent(stream, VersionOf(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle))));
        }

#pragma warning disable CA1849 // closing a local file that was only looked at; nothing here waits on anything
        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = OpenAdmitted(request);
            return stream is null
                ? default
                : new(VersionOf(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle)));
        }
#pragma warning restore CA1849
    }
}
