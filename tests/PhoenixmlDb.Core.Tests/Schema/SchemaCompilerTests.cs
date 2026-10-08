using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
using PhoenixmlDb.Core.Schema;
using Xunit;

#pragma warning disable CA2007 // xUnit runs tests without a synchronization context
#pragma warning disable CA2000 // streams handed to the compiler, which disposes them

namespace PhoenixmlDb.Core.Tests.Schema;

/// <summary>
/// <see cref="SchemaCompiler"/>: a schema is the whole of what its roots refer to, and every
/// document of it is read from a supplied <see cref="SchemaSource"/> or through the gate.
/// </summary>
public sealed class SchemaCompilerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "phx-sc-" + Guid.NewGuid().ToString("N"));

    public SchemaCompilerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static readonly Uri MainUri = new("urn:test:schemas/main.xsd");
    private static readonly Uri PartUri = new("mem://test/schemas/part.xsd");
    private static readonly Uri RootUri = new("mem://test/schemas/main.xsd");

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

    private const string Alone = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:a">
          <xs:element name="a" type="xs:string"/>
        </xs:schema>
        """;

    /// <summary>A gate that admits what it is told to and records every request.</summary>
    private sealed class RecordingGate(Dictionary<string, string> admitted) : ISchemaAccessGate
    {
        public List<SchemaDocumentRequest> Requests { get; } = [];
        public string Identity => "recording";

        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return new(admitted.TryGetValue(request.Uri.AbsoluteUri, out var text)
                ? new SchemaDocumentContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), "v:" + text.Length)
                : null);
        }

        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken)
            => new(admitted.TryGetValue(request.Uri.AbsoluteUri, out var text) ? "v:" + text.Length : null);
    }

    private static bool IsValid(CompiledSchema schema, string instance)
    {
        var valid = true;
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = schema.SchemaSet };
        settings.ValidationEventHandler += (_, _) => valid = false;
        using var reader = XmlReader.Create(new StringReader(instance), settings);
        while (reader.Read()) { }
        return valid;
    }

    [Fact]
    public void A_schema_made_of_supplied_sources_compiles_without_asking_the_gate()
    {
        var gate = new RecordingGate([]);
        var schema = SchemaCompiler.Compile([RootUri], gate,
            [SchemaSource.FromText(Main, RootUri), SchemaSource.FromText(Part, PartUri)]);

        gate.Requests.Should().BeEmpty();
        IsValid(schema, "<n xmlns='urn:m'>8</n>").Should().BeTrue();
        IsValid(schema, "<n xmlns='urn:m'>12</n>").Should().BeFalse();
        schema.Roots.Should().Equal(RootUri);
        schema.Documents.Select(d => d.Uri).Should().BeEquivalentTo([RootUri, PartUri]);
        schema.Documents.Should().OnlyContain(d => d.Version.StartsWith("sha256:"));
        schema.GateIdentity.Should().Be("recording");
    }

    [Fact]
    public void An_included_document_is_read_through_the_gate_and_recorded()
    {
        var gate = new RecordingGate(new() { [PartUri.AbsoluteUri] = Part });
        var schema = SchemaCompiler.Compile([RootUri], gate, [SchemaSource.FromText(Main, RootUri)]);

        gate.Requests.Should().Equal(new SchemaDocumentRequest(PartUri, "part.xsd", RootUri));
        gate.Requests[0].IsRoot.Should().BeFalse();
        IsValid(schema, "<n xmlns='urn:m'>12</n>").Should().BeFalse();
        schema.Documents.Should().Contain(new SchemaDocumentVersion(PartUri, "v:" + Part.Length));
    }

    [Fact]
    public void A_root_that_no_source_supplies_is_asked_of_the_gate_as_a_root()
    {
        var gate = new RecordingGate(new() { [RootUri.AbsoluteUri] = Main, [PartUri.AbsoluteUri] = Part });
        SchemaCompiler.Compile([RootUri], gate);
        gate.Requests.Should().Equal(
            new SchemaDocumentRequest(RootUri, RootUri.OriginalString, null),
            new SchemaDocumentRequest(PartUri, "part.xsd", RootUri));
        gate.Requests[0].IsRoot.Should().BeTrue();
    }

    /// <summary>
    /// System.Xml reports an include it cannot read as a warning and compiles what is left. Here
    /// the element's type is in the refused document, so that would fail anyway; the next test is
    /// the case where it would not.
    /// </summary>
    [Fact]
    public void A_refused_include_fails_the_compilation()
    {
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(Main, RootUri)]);
        act.Should().Throw<SchemaCompilationException>()
            .Which.Diagnostics.Should().Contain(d => d.Severity == SchemaSeverity.Error
                && d.SourceUri == RootUri.AbsoluteUri && d.Message.Contains("'part.xsd' is not available"));
    }

    [Fact]
    public void A_refused_import_fails_the_compilation_even_when_nothing_uses_it()
    {
        const string importer = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:i">
              <xs:import namespace="urn:a" schemaLocation="alone.xsd"/>
              <xs:element name="i" type="xs:string"/>
            </xs:schema>
            """;
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(importer, RootUri)]);
        act.Should().Throw<SchemaCompilationException>()
            .Which.Diagnostics.Should().ContainSingle()
            .Which.Message.Should().Contain("'alone.xsd' is not available").And.NotContain("mem://");
    }

    [Fact]
    public void A_refused_root_fails_the_compilation()
    {
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly);
        act.Should().Throw<SchemaCompilationException>().WithMessage("*not available*");
    }

    [Fact]
    public void A_schema_that_does_not_compile_reports_where()
    {
        const string broken = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:b" xmlns="urn:b">
              <xs:element name="b" type="nosuch"/>
            </xs:schema>
            """;
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(broken, RootUri)]);
        var error = act.Should().Throw<SchemaCompilationException>().Which.Diagnostics.Should().ContainSingle().Which;
        error.Severity.Should().Be(SchemaSeverity.Error);
        error.SourceUri.Should().Be(RootUri.AbsoluteUri);
        error.Line.Should().Be(2);
        error.Message.Should().Contain("nosuch");
    }

    [Fact]
    public void A_document_that_is_not_well_formed_is_reported_not_thrown_raw()
    {
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText("<xs:schema", RootUri)]);
        act.Should().Throw<SchemaCompilationException>().Which.Diagnostics.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_document_type_declaration_is_refused_in_a_root_and_in_an_include(bool inRoot)
    {
        const string doctype = "<!DOCTYPE xs:schema [<!ENTITY e 'x'>]>\n";
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText((inRoot ? doctype : "") + Main, RootUri), SchemaSource.FromText((inRoot ? "" : doctype) + Part, PartUri)]);
        act.Should().Throw<SchemaCompilationException>()
            .Which.Diagnostics.Should().Contain(d => d.Message.Contains("document type declaration")
                && d.SourceUri == (inRoot ? RootUri : PartUri).AbsoluteUri);
    }

    [Theory]
    [InlineData(SchemaCompiler.MaxNestingDepth - 4, true)]
    [InlineData(SchemaCompiler.MaxNestingDepth + 50, false)]
    public void A_document_nested_too_deeply_is_refused_before_it_is_parsed(int depth, bool accepted)
    {
        // Anything is allowed inside xs:appinfo, so depth costs nothing to write.
        var deep = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:d'><xs:annotation><xs:appinfo>"
            + string.Concat(Enumerable.Repeat("<d>", depth)) + string.Concat(Enumerable.Repeat("</d>", depth))
            + "</xs:appinfo></xs:annotation></xs:schema>";
        var act = () => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(deep, RootUri)]);
        if (accepted)
            act.Should().NotThrow();
        else
            act.Should().Throw<SchemaCompilationException>().WithMessage("*levels deep*");
    }

    [Fact]
    public void Several_roots_compile_into_one_schema()
    {
        var aloneUri = new Uri("mem://test/other/alone.xsd");
        var schema = SchemaCompiler.Compile([RootUri, aloneUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromText(Main, RootUri), SchemaSource.FromText(Part, PartUri), SchemaSource.FromText(Alone, aloneUri)]);
        IsValid(schema, "<n xmlns='urn:m'>8</n>").Should().BeTrue();
        IsValid(schema, "<a xmlns='urn:a'>x</a>").Should().BeTrue();
        schema.Documents.Should().HaveCount(3);
    }

    [Fact]
    public void A_compiled_schema_reports_itself_compiled()
        => SchemaCompiler.Compile([MainUri], SchemaAccessGate.SuppliedOnly, [SchemaSource.FromText(Alone, MainUri)])
            .SchemaSet.Should().Match<XmlSchemaSet>(s => s.IsCompiled);

    [Fact]
    public void A_source_needs_an_absolute_uri()
    {
        FluentActions.Invoking(() => SchemaSource.FromText(Alone, new Uri("relative.xsd", UriKind.Relative)))
            .Should().Throw<ArgumentException>().WithMessage("*absolute*");
        FluentActions.Invoking(() => SchemaCompiler.Compile([new Uri("relative.xsd", UriKind.Relative)], SchemaAccessGate.SuppliedOnly))
            .Should().Throw<ArgumentException>().WithMessage("*absolute*");
    }

    [Fact]
    public void Two_sources_with_one_name_are_an_error()
        => FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly,
                [SchemaSource.FromText(Main, RootUri), SchemaSource.FromText(Alone, RootUri)]))
            .Should().Throw<ArgumentException>().WithMessage("*Two schema sources*");

    [Fact]
    public void A_source_version_follows_its_content()
    {
        SchemaSource.FromText(Alone, RootUri).Version.Should().Be(SchemaSource.FromText(Alone, PartUri).Version);
        SchemaSource.FromText(Alone, RootUri).Version.Should().NotBe(SchemaSource.FromText(Main, RootUri).Version);
        SchemaSource.FromStore(RootUri, _ => new ValueTask<Stream>(new MemoryStream()), "7").Version.Should().Be("7");
    }

    private string Write(string relativePath, string text)
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void The_local_files_gate_reads_a_schema_and_its_include_under_a_root()
    {
        var main = Write("in/main.xsd", Main);
        Write("in/part.xsd", Part);
        var schema = SchemaCompiler.Compile([new Uri(main)], SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in")));
        IsValid(schema, "<n xmlns='urn:m'>12</n>").Should().BeFalse();
        schema.Documents.Should().HaveCount(2).And.OnlyContain(d => d.Version.StartsWith("file:"));
    }

    [Fact]
    public void The_local_files_gate_refuses_an_include_outside_its_roots()
    {
        var main = Write("in/main.xsd", Main.Replace("part.xsd", "../out/part.xsd", StringComparison.Ordinal));
        Write("out/part.xsd", Part);
        var act = () => SchemaCompiler.Compile([new Uri(main)], SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in")));
        act.Should().Throw<SchemaCompilationException>().WithMessage("*not available*");
        // The same schema compiles once the other directory is a root too: it was the gate.
        FluentActions.Invoking(() => SchemaCompiler.Compile([new Uri(main)],
            SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in"), Path.Combine(_dir, "out")))).Should().NotThrow();
    }

    [Fact]
    public void The_local_files_gate_refuses_a_directory_whose_name_only_starts_like_a_root()
    {
        var main = Write("in-other/main.xsd", Alone);
        var act = () => SchemaCompiler.Compile([new Uri(main)], SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in")));
        act.Should().Throw<SchemaCompilationException>();
    }

    [Fact]
    public async Task The_local_files_gate_refuses_what_is_not_a_file()
        => (await SchemaAccessGate.LocalFiles(_dir).OpenAsync(new SchemaDocumentRequest(new Uri("http://example.invalid/a.xsd"), "a.xsd", null), default))
            .Should().BeNull();

    [Fact]
    public async Task A_file_version_changes_when_the_file_does()
    {
        var path = Write("in/alone.xsd", Alone);
        var gate = SchemaAccessGate.LocalFiles(_dir);
        var request = new SchemaDocumentRequest(new Uri(path), path, null);
        async Task<string> VersionAsync()
        {
            var content = (await gate.OpenAsync(request, default))!;
            await content.Content.DisposeAsync();
            // The token without the content is the token with it.
            (await gate.GetVersionAsync(request, default)).Should().Be(content.Version);
            return content.Version;
        }
        var before = await VersionAsync();
        (await VersionAsync()).Should().Be(before);
        await File.WriteAllTextAsync(path, Alone + "<!-- changed -->");
        (await VersionAsync()).Should().NotBe(before);
    }

    [Fact]
    public void Gates_with_the_same_roots_have_the_same_identity()
    {
        var a = Path.Combine(_dir, "a");
        var b = Path.Combine(_dir, "b");
        SchemaAccessGate.LocalFiles(a, b).Identity.Should().Be(SchemaAccessGate.LocalFiles(b, a + Path.DirectorySeparatorChar).Identity);
        SchemaAccessGate.LocalFiles(a).Identity.Should().NotBe(SchemaAccessGate.LocalFiles(b).Identity);
    }

    /// <summary>
    /// Two documents include the same third one, and one of them names it two ways. It is opened
    /// once: a gate may count what it opens against a budget.
    /// </summary>
    [Fact]
    public void Each_document_is_opened_once_for_one_compilation()
    {
        var other = new Uri("mem://test/schemas/other.xsd");
        const string otherText = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:m" xmlns="urn:m">
              <xs:include schemaLocation="part.xsd"/>
              <xs:element name="o" type="size"/>
            </xs:schema>
            """;
        var main = Main.Replace("""<xs:include schemaLocation="part.xsd"/>""",
            """<xs:include schemaLocation="part.xsd"/><xs:include schemaLocation="other.xsd"/><xs:include schemaLocation="./part.xsd"/>""",
            StringComparison.Ordinal);
        var gate = new RecordingGate(new() { [RootUri.AbsoluteUri] = main, [PartUri.AbsoluteUri] = Part, [other.AbsoluteUri] = otherText });

        var schema = SchemaCompiler.Compile([RootUri, RootUri], gate);

        gate.Requests.Select(r => r.Uri).Should().BeEquivalentTo([RootUri, PartUri, other]);
        schema.Documents.Should().HaveCount(3);
        IsValid(schema, "<o xmlns='urn:m'>12</o>").Should().BeFalse();
    }

    [Fact]
    public void A_redefined_document_is_part_of_the_closure()
    {
        const string redefining = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:m" xmlns="urn:m" elementFormDefault="qualified">
              <xs:redefine schemaLocation="part.xsd">
                <xs:simpleType name="size"><xs:restriction base="size"><xs:maxInclusive value="5"/></xs:restriction></xs:simpleType>
              </xs:redefine>
              <xs:element name="n" type="size"/>
            </xs:schema>
            """;
        var gate = new RecordingGate(new() { [PartUri.AbsoluteUri] = Part });
        var schema = SchemaCompiler.Compile([RootUri], gate, [SchemaSource.FromText(redefining, RootUri)]);
        gate.Requests.Should().ContainSingle().Which.Uri.Should().Be(PartUri);
        IsValid(schema, "<n xmlns='urn:m'>5</n>").Should().BeTrue();
        IsValid(schema, "<n xmlns='urn:m'>8</n>").Should().BeFalse();
    }

    private sealed class ThrowingGate(Exception error) : ISchemaAccessGate
    {
        public string Identity => "throwing";
        public ValueTask<SchemaDocumentContent?> OpenAsync(SchemaDocumentRequest request, CancellationToken cancellationToken) => throw error;
        public ValueTask<string?> GetVersionAsync(SchemaDocumentRequest request, CancellationToken cancellationToken) => throw error;
    }

    [Fact]
    public async Task What_a_gate_throws_reaches_the_caller_unchanged()
    {
        var thrown = new InvalidOperationException("the host's own refusal");
        var act = async () => await SchemaCompiler.CompileAsync([RootUri], new ThrowingGate(thrown), [SchemaSource.FromText(Main, RootUri)]);
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(thrown);
        FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], new ThrowingGate(thrown)))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(thrown);
    }

    [Fact]
    public async Task What_a_source_throws_reaches_the_caller_unchanged()
    {
        var thrown = new IOException("the store is away");
        var act = async () => await SchemaCompiler.CompileAsync([RootUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromStore(RootUri, _ => throw thrown, "1")]);
        (await act.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(thrown);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_compilation_and_is_given_to_the_source()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        var source = SchemaSource.FromStore(RootUri, async token =>
        {
            seen = token;
            await cts.CancelAsync();
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Main));
        }, "1");
        var gate = new RecordingGate(new() { [PartUri.AbsoluteUri] = Part });

        var act = async () => await SchemaCompiler.CompileAsync([RootUri], gate, [source], cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        seen.Should().Be(cts.Token);
        // Cancelled while the root was being read: the include was never asked for.
        gate.Requests.Should().BeEmpty();
    }

    /// <summary>A stream that says it holds far more than any limit, and counts what is taken from it.</summary>
    private sealed class EndlessStream : Stream
    {
        public long Taken { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => long.MaxValue;
        public override long Position { get => Taken; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)' ', offset, count);
            Taken += count;
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task No_more_than_the_document_limit_is_taken_from_a_stream()
    {
        var endless = new EndlessStream();
        var options = new SchemaCompileOptions { MaxDocumentBytes = 100_000 };
        var act = async () => await SchemaCompiler.CompileAsync([RootUri], SchemaAccessGate.SuppliedOnly,
            [SchemaSource.FromStore(RootUri, _ => new ValueTask<Stream>(endless), "1")], options);
        (await act.Should().ThrowAsync<SchemaCompilationException>()).WithMessage("*larger than the limit of 100000 bytes*");
        // One read past the limit at most, never the stream's claimed length.
        endless.Taken.Should().BeLessThan(100_000 + 2 * 81920);
    }

    [Fact]
    public void The_total_limit_counts_every_document()
    {
        var sources = new[] { SchemaSource.FromText(Main, RootUri), SchemaSource.FromText(Part, PartUri) };
        var size = System.Text.Encoding.UTF8.GetByteCount(Main) + System.Text.Encoding.UTF8.GetByteCount(Part);
        FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, sources,
            new SchemaCompileOptions { MaxTotalBytes = size })).Should().NotThrow();
        FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, sources,
            new SchemaCompileOptions { MaxTotalBytes = size - 1 }))
            .Should().Throw<SchemaCompilationException>().WithMessage("*come to more than the limit*");
    }

    [Fact]
    public void The_document_count_is_limited()
    {
        var sources = new[] { SchemaSource.FromText(Main, RootUri), SchemaSource.FromText(Part, PartUri) };
        FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, sources,
            new SchemaCompileOptions { MaxDocuments = 1 })).Should().Throw<SchemaCompilationException>().WithMessage("*more than 1 documents*");
        FluentActions.Invoking(() => SchemaCompiler.Compile([RootUri], SchemaAccessGate.SuppliedOnly, sources,
            new SchemaCompileOptions { MaxDocuments = 2 })).Should().NotThrow();
    }

    [Fact]
    public void The_local_files_gate_refuses_a_link_that_leads_out_of_its_root()
    {
        var outside = Write("out/part.xsd", Part);
        var main = Write("in/main.xsd", Main);
        try
        {
            File.CreateSymbolicLink(Path.Combine(_dir, "in", "part.xsd"), outside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return; // this account may not create links (Windows without the privilege)
        }
        var inside = SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in"));
        FluentActions.Invoking(() => SchemaCompiler.Compile([new Uri(main)], inside))
            .Should().Throw<SchemaCompilationException>().WithMessage("*'part.xsd' is not available*");
        // The same link is admitted when where it leads is a root as well: the decision is made
        // on the file that was opened.
        FluentActions.Invoking(() => SchemaCompiler.Compile([new Uri(main)],
            SchemaAccessGate.LocalFiles(Path.Combine(_dir, "in"), Path.Combine(_dir, "out")))).Should().NotThrow();
    }
}
