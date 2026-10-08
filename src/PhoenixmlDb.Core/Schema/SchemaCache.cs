using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PhoenixmlDb.Core.Schema;

/// <summary>How a <see cref="SchemaCache"/> keeps and re-examines what it holds.</summary>
public sealed record SchemaCacheOptions
{
    /// <summary>
    /// How long a cached schema is given out without asking whether its documents have changed.
    /// 30 seconds by default. <see cref="TimeSpan.Zero"/> asks on every use;
    /// <see cref="Timeout.InfiniteTimeSpan"/> never asks, and then only
    /// <see cref="SchemaCache.Invalidate"/> and <see cref="SchemaCache.Clear"/> make a schema new.
    /// </summary>
    /// <remarks>
    /// The interval is also how long a gate's admission of a cached schema is taken as still
    /// holding. A host that uses <b>one gate object for many requests</b> can therefore be given a
    /// cached schema for up to one interval after that gate would refuse one of its documents,
    /// for example after a permission is removed. Such a host sets <see cref="TimeSpan.Zero"/>,
    /// which asks the gate on every use, or uses a gate object for each request: a gate object
    /// the cache has not seen is always asked first.
    /// </remarks>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The most compiled schemas kept; the least recently used goes first. 256 by default.</summary>
    public int MaxEntries { get; init; } = 256;

    /// <summary>The clock the interval is measured by. The system clock when null.</summary>
    public TimeProvider? TimeProvider { get; init; }
}

/// <summary>
/// Keeps compiled schemas, so that a schema is compiled once and used until one of its documents
/// changes.
/// </summary>
/// <remarks>
/// <para>
/// A schema is found by its roots, the identity of the gate and the compile limits. It is out of
/// date when any document of its closure has another version than the one compiled, not only a
/// root: a supplied <see cref="SchemaSource"/> is compared by the version it carries, and any
/// other document by asking the gate (<see cref="ISchemaAccessGate.GetVersionAsync"/>).
/// </para>
/// <para>
/// <b>A gate is only given a schema it admits.</b> Before a cached schema goes to a request, that
/// request's own gate is asked about every document of it that no source of the request supplies.
/// If it refuses one, the request compiles for itself, through that gate, and so fails as it would
/// have with no cache. The answer holds for that gate object for one check interval. Two gates
/// that report the same <see cref="ISchemaAccessGate.Identity"/> therefore share compiled schemas
/// only as far as each of them admits the documents.
/// </para>
/// <para>
/// Requests that arrive together for the same schema compile it once. A compilation that fails
/// is not kept.
/// </para>
/// </remarks>
public sealed class SchemaCache
{
    /// <summary>The cache the engines share. A host that wants its own keeps its own instance.</summary>
    public static SchemaCache Default { get; } = new();

    private readonly SchemaCacheOptions _options;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> _entries = new(StringComparer.Ordinal);
    private long _clock;

    /// <summary>Makes a cache.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="SchemaCacheOptions.MaxEntries"/> is less than 1, or
    /// <see cref="SchemaCacheOptions.CheckInterval"/> is negative and not infinite.
    /// </exception>
    public SchemaCache(SchemaCacheOptions? options = null)
    {
        _options = options ?? new SchemaCacheOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.MaxEntries, 1, nameof(options));
        if (_options.CheckInterval < TimeSpan.Zero && _options.CheckInterval != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options), "The check interval is negative.");
        _time = _options.TimeProvider ?? TimeProvider.System;
    }

    /// <summary>How many compiled schemas the cache holds.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// The compiled schema for <paramref name="roots"/>: the cached one when it is still current
    /// and <paramref name="gate"/> admits it, otherwise a new compilation.
    /// </summary>
    /// <inheritdoc cref="SchemaCompiler.CompileAsync" path="/param"/>
    /// <inheritdoc cref="SchemaCompiler.CompileAsync" path="/exception"/>
    public async ValueTask<CompiledSchema> GetAsync(IEnumerable<Uri> roots, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(gate);
        var rootList = roots.ToArray();
        var sourceList = sources?.ToArray() ?? [];
        var compileOptions = options ?? SchemaCompileOptions.Default;
        var key = KeyOf(rootList, gate, compileOptions);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = false;
            var lazy = _entries.GetOrAdd(key, _ => new Lazy<Task<Entry>>(() =>
            {
                started = true;
                return CompileEntryAsync(rootList, gate, sourceList, compileOptions, cancellationToken);
            }));

            Entry entry;
            try
            {
                entry = await lazy.Value.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!started && !cancellationToken.IsCancellationRequested)
            {
                // Another request's compilation, cancelled by that request. Ours goes on.
                Remove(key, lazy);
                continue;
            }
            catch
            {
                // Not kept: the next request compiles again and sees for itself.
                Remove(key, lazy);
                throw;
            }

            if (started)
            {
                Trim();
                return entry.Schema;
            }
            switch (await ExamineAsync(entry, gate, sourceList, cancellationToken).ConfigureAwait(false))
            {
                case Examined.Current:
                    entry.LastUsed = Interlocked.Increment(ref _clock);
                    return entry.Schema;
                case Examined.Changed:
                    // Out of date for everyone: compile again and keep the result.
                    Remove(key, lazy);
                    continue;
                default:
                    // This gate does not admit a document of the cached schema. Compile through
                    // it, which reports the refusal; the cached schema stays for those it serves.
                    return await SchemaCompiler.CompileAsync(rootList, gate, sourceList, compileOptions, cancellationToken)
                        .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// <see cref="GetAsync"/> for a caller that cannot wait asynchronously; it blocks the calling
    /// thread, so it is for hosts whose gate and sources answer at once or that may block a thread.
    /// </summary>
    public CompiledSchema Get(IEnumerable<Uri> roots, ISchemaAccessGate gate,
        IEnumerable<SchemaSource>? sources = null, SchemaCompileOptions? options = null)
    {
        var pending = GetAsync(roots, gate, sources, options, CancellationToken.None);
        return pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Forgets every compiled schema that has the document <paramref name="uri"/> anywhere in its
    /// closure. A host calls this when it knows a document has changed, been replaced or been
    /// removed, and does not want to wait for the next check.
    /// </summary>
    public void Invalidate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        foreach (var (key, lazy) in _entries)
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully
                && lazy.Value.Result.Schema.Documents.Any(d => d.Uri.AbsoluteUri == uri.AbsoluteUri))
                Remove(key, lazy);
        }
    }

    /// <summary>Forgets every compiled schema.</summary>
    public void Clear() => _entries.Clear();

    private void Remove(string key, Lazy<Task<Entry>> lazy)
        => _entries.TryRemove(new KeyValuePair<string, Lazy<Task<Entry>>>(key, lazy));

    private static string KeyOf(Uri[] roots, ISchemaAccessGate gate, SchemaCompileOptions options)
    {
        foreach (var root in roots)
            if (root is null || !root.IsAbsoluteUri)
                throw new ArgumentException($"A root schema needs an absolute URI; '{root}' is not one.", nameof(roots));
        var names = roots.Select(r => r.AbsoluteUri).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return string.Join("\n", names) + "\n\n" + gate.Identity + "\n"
            + string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{options.MaxDocumentBytes}:{options.MaxTotalBytes}:{options.MaxDocuments}");
    }

    private async Task<Entry> CompileEntryAsync(Uri[] roots, ISchemaAccessGate gate, SchemaSource[] sources,
        SchemaCompileOptions options, CancellationToken cancellationToken)
    {
        var schema = await SchemaCompiler.CompileAsync(roots, gate, sources, options, cancellationToken).ConfigureAwait(false);
        var entry = new Entry(schema) { LastUsed = Interlocked.Increment(ref _clock) };
        // The gate that compiled it has just admitted every document.
        entry.Examinations.AddOrUpdate(gate, new Examination { At = _time.GetTimestamp() });
        return entry;
    }

    private enum Examined
    {
        Current,
        Changed,
        NotAdmitted,
    }

    /// <summary>Whether the entry is current, and whether this gate admits it.</summary>
    private async ValueTask<Examined> ExamineAsync(Entry entry, ISchemaAccessGate gate, SchemaSource[] sources,
        CancellationToken cancellationToken)
    {
        // What the request supplies is compared every time: it is in hand, and it may differ from
        // one request to the next where everything else is the same.
        Dictionary<string, SchemaSource>? supplied = null;
        foreach (var source in sources)
            (supplied ??= new(StringComparer.Ordinal))[source.Uri.AbsoluteUri] = source;
        foreach (var document in entry.Schema.Documents)
        {
            if (supplied is not null && supplied.TryGetValue(document.Uri.AbsoluteUri, out var source)
                && source.Version != document.Version)
                return Examined.Changed;
        }

        // What the gate reads is asked about once per interval for each gate object.
        if (entry.Examinations.TryGetValue(gate, out var last) && !Due(last.At))
            return Examined.Current;

        foreach (var document in entry.Schema.Documents)
        {
            if (supplied is not null && supplied.ContainsKey(document.Uri.AbsoluteUri))
                continue;
            var request = entry.Requests[document.Uri.AbsoluteUri];
            var version = await gate.GetVersionAsync(request, cancellationToken).ConfigureAwait(false);
            if (version is null)
                return Examined.NotAdmitted;
            if (version != document.Version)
                return Examined.Changed;
        }
        entry.Examinations.AddOrUpdate(gate, new Examination { At = _time.GetTimestamp() });
        return Examined.Current;
    }

    private bool Due(long since)
    {
        if (_options.CheckInterval == Timeout.InfiniteTimeSpan)
            return false;
        return _options.CheckInterval == TimeSpan.Zero || _time.GetElapsedTime(since) >= _options.CheckInterval;
    }

    /// <summary>Drops the least recently used schemas over the limit.</summary>
    private void Trim()
    {
        while (_entries.Count > _options.MaxEntries)
        {
            string? oldestKey = null;
            Lazy<Task<Entry>>? oldest = null;
            var oldestUse = long.MaxValue;
            foreach (var (key, lazy) in _entries)
            {
                if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully)
                    continue;
                var used = lazy.Value.Result.LastUsed;
                if (used < oldestUse)
                    (oldestKey, oldest, oldestUse) = (key, lazy, used);
            }
            if (oldestKey is null)
                return;
            Remove(oldestKey, oldest!);
        }
    }

    private sealed class Examination
    {
        public long At { get; init; }
    }

    private sealed class Entry(CompiledSchema schema)
    {
        public CompiledSchema Schema { get; } = schema;

        /// <summary>When each gate object last admitted every document. Gates are not kept alive by it.</summary>
        public ConditionalWeakTable<ISchemaAccessGate, Examination> Examinations { get; } = new();

        /// <summary>The request each document was read by, to ask about it again in the same words.</summary>
        public IReadOnlyDictionary<string, SchemaDocumentRequest> Requests { get; } = schema.Requests;

        public long LastUsed;
    }
}
