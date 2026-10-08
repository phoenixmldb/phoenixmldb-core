using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

#pragma warning disable CA2007 // xUnit runs tests without a synchronization context
#pragma warning disable CA2000 // streams handed to the compiler, which disposes them

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// <see cref="SchemaCache"/>: a schema is compiled once and used until a document of it changes,
/// and a gate is only given a schema it admits.
/// </summary>
public sealed class SchemaCacheTests
{
    private static readonly Uri RootUri = new("mem://test/schemas/main.xsd");
    private static readonly Uri PartUri = new("mem://test/schemas/part.xsd");

    private const string Part = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:m" xmlns="urn:m">
          <xs:simpleType name="size"><xs:restriction base="xs:integer"><xs:maxInclusive value="9"/></xs:restriction></xs:simpleType>
        </xs:schema>
        """;

    private const string Main = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:m" xmlns="urn:m" elementFormDefault="qualified">
          <xs:include schemaLocation="part.xsd"/>
          <xs:element name="n" type="size"/>
        </xs:schema>
        """;

    /// <summary>A gate over a table the test changes, counting what it is asked.</summary>
    private sealed class TableGate(Dictionary<string, (string Text, string Version)> documents, string identity = "table") : ISchemaAccessGate
    {
        public int Opens;
        public int VersionQuestions;
        public Func<Task>? BeforeOpen { get; set; }
        public string Identity => identity;

        public async ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Opens);
            if (BeforeOpen is { } wait)
                await wait().WaitAsync(cancellationToken);
            return documents.TryGetValue(request.Uri.AbsoluteUri, out var d)
                ? new SchemaDocumentContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(d.Text)), d.Version)
                : null;
        }

        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref VersionQuestions);
            return new(documents.TryGetValue(request.Uri.AbsoluteUri, out var d) ? d.Version : null);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private static Dictionary<string, (string, string)> Table() => new()
    {
        [RootUri.AbsoluteUri] = (Main, "1"),
        [PartUri.AbsoluteUri] = (Part, "1"),
    };

    private static SchemaCache Cache(TimeSpan interval, ManualTime? time = null, int max = 256)
        => new(new SchemaCacheOptions { CheckInterval = interval, TimeProvider = time, MaxEntries = max });

    [Fact]
    public async Task A_schema_is_compiled_once_and_given_again()
    {
        var gate = new TableGate(Table());
        var cache = Cache(TimeSpan.FromSeconds(30), new ManualTime());

        var first = await cache.GetAsync([RootUri], gate);
        var second = await cache.GetAsync([RootUri], gate);

        second.Should().BeSameAs(first);
        gate.Opens.Should().Be(2);            // the root and its include, once
        gate.VersionQuestions.Should().Be(0); // within the interval nothing is asked
        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task A_changed_include_is_seen_at_the_next_check_and_not_before()
    {
        var table = Table();
        var gate = new TableGate(table);
        var time = new ManualTime();
        var cache = Cache(TimeSpan.FromSeconds(30), time);
        var first = await cache.GetAsync([RootUri], gate);

        table[PartUri.AbsoluteUri] = (Part.Replace("9", "5", StringComparison.Ordinal), "2");
        time.Advance(TimeSpan.FromSeconds(29));
        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(first);

        time.Advance(TimeSpan.FromSeconds(1));
        var later = await cache.GetAsync([RootUri], gate);
        later.Should().NotBeSameAs(first);
        later.Documents.Should().Contain(new SchemaDocumentVersion(PartUri, "2"));
        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(later);
    }

    [Fact]
    public async Task An_unchanged_schema_is_kept_at_a_check()
    {
        var gate = new TableGate(Table());
        var time = new ManualTime();
        var cache = Cache(TimeSpan.FromSeconds(30), time);
        var first = await cache.GetAsync([RootUri], gate);
        time.Advance(TimeSpan.FromMinutes(5));

        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(first);

        gate.VersionQuestions.Should().Be(2); // both documents asked about, neither read again
        gate.Opens.Should().Be(2);
    }

    [Fact]
    public async Task With_a_zero_interval_every_use_asks()
    {
        var table = Table();
        var gate = new TableGate(table);
        var cache = Cache(TimeSpan.Zero);
        var first = await cache.GetAsync([RootUri], gate);
        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(first);
        table[PartUri.AbsoluteUri] = (Part, "2");
        (await cache.GetAsync([RootUri], gate)).Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task With_an_infinite_interval_only_invalidation_makes_a_schema_new()
    {
        var table = Table();
        var gate = new TableGate(table);
        var time = new ManualTime();
        var cache = Cache(Timeout.InfiniteTimeSpan, time);
        var first = await cache.GetAsync([RootUri], gate);
        table[PartUri.AbsoluteUri] = (Part, "2");
        time.Advance(TimeSpan.FromDays(365));
        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(first);

        // Named by a document of the closure that is not a root.
        cache.Invalidate(PartUri);

        cache.Count.Should().Be(0);
        (await cache.GetAsync([RootUri], gate)).Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task Invalidating_another_document_leaves_a_schema_alone()
    {
        var gate = new TableGate(Table());
        var cache = Cache(Timeout.InfiniteTimeSpan);
        var first = await cache.GetAsync([RootUri], gate);
        cache.Invalidate(new Uri("mem://test/schemas/unrelated.xsd"));
        (await cache.GetAsync([RootUri], gate)).Should().BeSameAs(first);
        cache.Clear();
        (await cache.GetAsync([RootUri], gate)).Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task A_supplied_source_with_other_content_is_seen_at_once()
    {
        var gate = new TableGate(new() { [PartUri.AbsoluteUri] = (Part, "1") });
        var cache = Cache(Timeout.InfiniteTimeSpan);
        var first = await cache.GetAsync([RootUri], gate, [SchemaSource.FromText(Main, RootUri)]);
        (await cache.GetAsync([RootUri], gate, [SchemaSource.FromText(Main, RootUri)])).Should().BeSameAs(first);

        var changed = await cache.GetAsync([RootUri], gate, [SchemaSource.FromText(Main + "<!-- edited -->", RootUri)]);

        changed.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Two gates that call themselves the same. One does not admit the included document. It gets
    /// the failure it would get with no cache, and the cached schema stays for the other.
    /// </summary>
    [Fact]
    public async Task A_gate_is_not_given_a_schema_with_a_document_it_refuses()
    {
        var open = new TableGate(Table(), identity: "same");
        var narrow = new TableGate(new() { [RootUri.AbsoluteUri] = (Main, "1") }, identity: "same");
        var cache = Cache(TimeSpan.FromSeconds(30), new ManualTime());
        var cached = await cache.GetAsync([RootUri], open);

        var act = async () => await cache.GetAsync([RootUri], narrow);

        (await act.Should().ThrowAsync<SchemaCompilationException>()).WithMessage("*'part.xsd' is not available*");
        narrow.VersionQuestions.Should().BeGreaterThan(0);
        (await cache.GetAsync([RootUri], open)).Should().BeSameAs(cached);
        // And again: the refusal was not remembered as an admission.
        await act.Should().ThrowAsync<SchemaCompilationException>();
    }

    [Fact]
    public async Task A_second_gate_that_admits_everything_shares_the_schema_after_being_asked()
    {
        var table = Table();
        var one = new TableGate(table, identity: "same");
        var two = new TableGate(table, identity: "same");
        var cache = Cache(TimeSpan.FromSeconds(30), new ManualTime());
        var cached = await cache.GetAsync([RootUri], one);

        (await cache.GetAsync([RootUri], two)).Should().BeSameAs(cached);

        two.Opens.Should().Be(0);
        two.VersionQuestions.Should().Be(2);
        // Within the interval it is not asked a second time.
        (await cache.GetAsync([RootUri], two)).Should().BeSameAs(cached);
        two.VersionQuestions.Should().Be(2);
    }

    [Fact]
    public async Task Gates_with_other_identities_do_not_share()
    {
        var table = Table();
        var cache = Cache(Timeout.InfiniteTimeSpan);
        var a = await cache.GetAsync([RootUri], new TableGate(table, "a"));
        var b = await cache.GetAsync([RootUri], new TableGate(table, "b"));
        b.Should().NotBeSameAs(a);
        cache.Count.Should().Be(2);
    }

    [Fact]
    public async Task Other_limits_are_another_entry()
    {
        var gate = new TableGate(Table());
        var cache = Cache(Timeout.InfiniteTimeSpan);
        var a = await cache.GetAsync([RootUri], gate);
        var b = await cache.GetAsync([RootUri], gate, options: new SchemaCompileOptions { MaxDocuments = 5 });
        b.Should().NotBeSameAs(a);
    }

    [Fact]
    public async Task Requests_that_arrive_together_compile_once()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TableGate(Table()) { BeforeOpen = () => release.Task };
        var cache = Cache(TimeSpan.FromSeconds(30), new ManualTime());

        var requests = Enumerable.Range(0, 24).Select(_ => Task.Run(async () => await cache.GetAsync([RootUri], gate))).ToArray();
        // All of them are waiting on the one compilation, which is waiting on the gate.
        while (Volatile.Read(ref gate.Opens) == 0)
            await Task.Yield();
        release.SetResult();
        var results = await Task.WhenAll(requests);

        results.Should().OnlyContain(r => ReferenceEquals(r, results[0]));
        gate.Opens.Should().Be(2);
    }

    [Fact]
    public async Task A_failed_compilation_is_not_kept()
    {
        var table = new Dictionary<string, (string, string)> { [RootUri.AbsoluteUri] = (Main, "1") };
        var gate = new TableGate(table);
        var cache = Cache(Timeout.InfiniteTimeSpan);
        var act = async () => await cache.GetAsync([RootUri], gate);
        await act.Should().ThrowAsync<SchemaCompilationException>();
        cache.Count.Should().Be(0);

        table[PartUri.AbsoluteUri] = (Part, "1");

        (await cache.GetAsync([RootUri], gate)).Documents.Should().HaveCount(2);
        cache.Count.Should().Be(1);
    }

    /// <summary>
    /// One request starts the compilation and is cancelled while it waits on the gate. A second
    /// request that was sharing it is not cancelled with it.
    /// </summary>
    [Fact]
    public async Task A_request_outlives_the_cancellation_of_the_one_it_shared_a_compilation_with()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TableGate(Table()) { BeforeOpen = () => release.Task };
        var cache = Cache(TimeSpan.FromSeconds(30), new ManualTime());
        using var firstToken = new CancellationTokenSource();

        var first = Task.Run(async () => await cache.GetAsync([RootUri], gate, cancellationToken: firstToken.Token));
        while (Volatile.Read(ref gate.Opens) == 0)
            await Task.Yield();
        var second = Task.Run(async () => await cache.GetAsync([RootUri], gate));
        // Let the second reach the shared compilation before the first is cancelled.
        await Task.Delay(50);
        await firstToken.CancelAsync();
        var firstAct = async () => await first;
        await firstAct.Should().ThrowAsync<OperationCanceledException>();
        release.SetResult();

        (await second).Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_least_recently_used_schema_goes_first()
    {
        var table = new Dictionary<string, (string, string)>();
        Uri Add(string name)
        {
            var uri = new Uri($"mem://test/lru/{name}.xsd");
            table[uri.AbsoluteUri] = ($"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:{name}'/>", "1");
            return uri;
        }
        var (a, b, c) = (Add("a"), Add("b"), Add("c"));
        var gate = new TableGate(table);
        var cache = Cache(Timeout.InfiniteTimeSpan, max: 2);

        var first = await cache.GetAsync([a], gate);
        await cache.GetAsync([b], gate);
        (await cache.GetAsync([a], gate)).Should().BeSameAs(first); // a is now the more recent
        await cache.GetAsync([c], gate);                              // b goes

        cache.Count.Should().Be(2);
        (await cache.GetAsync([a], gate)).Should().BeSameAs(first);
        var opensBefore = gate.Opens;
        await cache.GetAsync([b], gate);
        gate.Opens.Should().Be(opensBefore + 1);
    }

    [Fact]
    public void The_synchronous_form_gives_the_same_cached_schema()
    {
        var gate = new TableGate(Table());
        var cache = Cache(Timeout.InfiniteTimeSpan);
        cache.Get([RootUri], gate).Should().BeSameAs(cache.Get([RootUri, RootUri], gate));
    }

    [Fact]
    public void Bad_options_are_refused()
    {
        FluentActions.Invoking(() => new SchemaCache(new SchemaCacheOptions { MaxEntries = 0 })).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new SchemaCache(new SchemaCacheOptions { CheckInterval = TimeSpan.FromSeconds(-2) })).Should().Throw<ArgumentOutOfRangeException>();
    }
}
