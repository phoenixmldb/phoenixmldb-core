# Release History

## 1.6.7 — 2026-08-24

### Changed

- **The reserved metadata namespace's conventional prefix is now `dbxml`, was `phxm`.** Prefix
  only — the URI is unchanged at `https://schemas.phoenixml.dev/2026/meta`, so the namespace's
  identity, every stored key and every `NamespaceId` are untouched. Metadata keys encode the
  numeric namespace id rather than the URI, so nothing on disk moves.

  Visible in `MetadataProperty.ToString()`, which now renders `dbxml:status` where it rendered
  `phxm:status`, and anywhere a caller declares the prefix in an XQuery prolog. `dbxml` is the
  prefix this project used before the namespace rework, and it reads as what it is.

## 1.6.6 — 2026-08-23

No library changes. Version alignment only, under the policy stated in 1.6.5 below.

The release exists because `PhoenixmlDb.XQuery` and `PhoenixmlDb.Xslt` ship real fixes at
this number, and the family carries one version so a developer can tell at a glance that a
set of packages belongs together. This package is republished unchanged, which is the
accepted cost of that guarantee rather than an oversight.

The one change in the repo since 1.6.5 is to CI, not to the library: the pack job now runs
the tests first. It could previously publish a package built from code whose tests had never
been run in that job.

## 1.6.5 — 2026-08-21

No code changes. Version alignment only.

From this release the engine libraries and the CLI tools that ship on top of them —
`PhoenixmlDb.Core`, `PhoenixmlDb.XQuery`, `PhoenixmlDb.Xslt`, the `xslt` tool and
`phxspec` — carry a single version, so a developer can tell at a glance that a set of
packages belongs together. The versions had drifted to 1.6.0 / 1.6.2 / 1.6.4 / 1.4.10
across the family, and a matching number is easier to reason about than a compatibility
matrix.

The trade is visible right here: this package is republished unchanged to keep the
number aligned. That is deliberate, and it is the accepted cost of the guarantee.

## 1.6.0 — 2026-08-06

Range queries over document metadata, and a typed way to declare the index that serves them.
Additive — nothing from 1.5.0 changes shape.

### `QueryMetadataRangeAsync`

Metadata could be queried for equality but not for order, which left the obvious questions —
everything received since a cutoff, everything with a retry count above a threshold — unable to
use an index even when one existed.

```csharp
// everything received on or after the cutoff
await foreach (var d in container.QueryMetadataRangeAsync(Received, lowerBound: cutoff, upperBound: null))
    Console.WriteLine(d.Name);
```

Either bound may be null for an open-ended range; both null matches every document carrying the
property at all. Ordering follows the property's XDM type rather than its .NET string form, so
dates order chronologically and numbers numerically.

As with equality, a declared index changes how the range is found and never what it finds. An
unindexed container answers the same query by scanning, comparing the same order-preserving
encoding the index walks.

**Two overloads, for a reason worth stating.** The typed overload constrains `T : struct`:

```csharp
IAsyncEnumerable<DocumentInfo> QueryMetadataRangeAsync<T>(
    MetadataProperty<T> descriptor, T? lowerBound, T? upperBound, ...) where T : struct;
```

On an unconstrained `T`, `T?` is a nullability annotation rather than `Nullable<T>`, so a
value-typed property could not express an open-ended range at all — `null` would not compile.
Using `default(T)` for "no bound" was the alternative, and it is a lie the caller cannot detect:
an unbounded range would be indistinguishable from one bounded at `DateTimeOffset.MinValue` or
`0`. The second overload takes an `XdmQName` with `XdmValue?` bounds and covers everything the
first cannot, including `MetadataProperty<string>`.

### `AddMetadataIndex<T>`

```csharp
opts.Indexes.AddMetadataIndex(Received, XdmValueType.DateTime);
```

Takes the qualified name from the property rather than restating it, so an index cannot drift
from the values it is meant to cover. The `XdmQName` overload remains for names with no
declared property.

## 1.5.0 — 2026-08-05

Metadata becomes namespace-qualified end to end. **Breaking**: the metadata surfaces on
`IContainer`, `IWriteTransaction` and `IDocument` change shape, as does `AddMetadataIndex` and the
persisted index-config format.

### Why this is breaking rather than additive

The old surface keyed metadata by a bare string and valued it as `object`. Two applications sharing
a database could not both use a name as ordinary as `status`, and — more seriously — the engine had
no single answer for what a metadata value *was*. A stored value was JSON; an indexed value was
typed XDM. Those are different comparisons, so adding an index could change which documents a query
returned, not merely how quickly it found them. That is not a defect an additive API can fix: the
two encodings had to stop existing, which means the surface that produced them had to go.

### The three-tier surface

Metadata names are now `XdmQName` and values `XdmValue`, exposed at three levels of explicitness:

```csharp
// local name, resolved against the container's default namespace
await container.SetMetadataAsync("invoice.xml", "status", "pending");

// typed descriptor — namespace and CLR type come from the property
await container.SetMetadataAsync("invoice.xml", DcTerms.Creator, "lucas");

// fully explicit
await container.SetMetadataAsync("invoice.xml", qname, XdmValue.From("application/xml"));
```

`ContainerOptions.DefaultMetadataNamespace` sets what unqualified names resolve to, defaulting to
`https://schemas.phoenixml.dev/2026/meta`. Set it to your own application namespace and a common
name such as `status` can no longer collide with another application's.

`GetAllMetadataAsync` returns a `MetadataCollection` rather than
`IReadOnlyDictionary<string, object>`, so a caller can finally separate a namespace from a local
name — the old shape handed back `"ns:name"` as one unsplittable string. `GetMetadataByNamespaceAsync`
is new, and is served by a cursor range over the namespace key prefix rather than by fetching all of
a document's metadata and filtering it.

`DocumentOptions.Metadata` is now `IReadOnlyDictionary<XdmQName, XdmValue>?`.

### Indexes are qualified too

`AddMetadataIndex` takes an `XdmQName`. An index that cannot say *which* `status` it covers is the
same collision in a different place, and — because the store keys by qualified name — a bare-named
index would answer for documents it does not describe. With one key model on both sides, declaring
an index is a pure performance decision.

The persisted index configuration records the namespace alongside the local name. A configuration
written by an earlier version is rejected with an explanatory error rather than reinterpreted:
defaulting the namespace would silently point an index at a different key than the store writes.

### Removed

`IContainer.SetIndexedValuesAsync` is deleted rather than ported. Its default interface
implementation returned a completed task for any implementation without indexing, so a caller could
not distinguish a successful write from a no-op; it also stored its values as a single JSON array
while indexing each element separately. Multi-valued metadata returns as a deliberate feature or
not at all.

### Notes

Two parameter names could not be used as intended: CA1716 rejects `property` and `namespace` on
virtual and interface members because they collide with reserved keywords in other .NET languages.
They are `descriptor` and `namespaceId`. Positional call sites are unaffected.

## 1.4.0 — 2026-08-04

Foundation for typed document metadata: a single conversion boundary between CLR values and XDM, a
registry for well-known namespaces, and typed metadata property descriptors. Additive — no public
API was removed.

### `XdmValue.From<T>` / `To<T>` — one CLR-to-XDM conversion

`XdmValue` gains a single, symmetric conversion pair, replacing ad-hoc per-call-site coercion.

**`To<T>` is strict**, deliberately. The stored `XdmType` must match the requested `T`, with numeric
widening the only implicit conversion; a mismatch throws `InvalidCastException` and an overflowing
narrowing throws `OverflowException`. It does *not* inherit `AsLong()`'s XPath-style coercion, which
parses strings into numbers — correct in an XPath expression, wrong at a typed storage boundary,
where it would let a value round-trip as a different type than it was written as.

**Empty-value semantics are explicit.** `From<T>(null)` produces `Empty`. `To<T>(Empty)` returns
`null` for reference types and `Nullable<V>`, preserving the round-trip, and throws
`InvalidCastException` for non-nullable value types — because `default(long)` is `0`, and silently
returning `0` for an absent value is a lie the caller cannot detect. Note `XdmQName` is a
`readonly record struct` and follows the value-type arm.

### `NamespaceRegistry` — one source for well-known URIs

Well-known namespace URIs and their conventional prefixes now live in one table rather than being
restated at each use, so a URI typo cannot silently create a second namespace.

### Typed metadata: `MetadataProperty<T>`, `MetadataCollection`, vocabularies

`MetadataProperty<T>` is a typed, namespace-qualified property descriptor; `MetadataCollection`
holds resolved values. Ships with `PhxMeta` (engine-reserved: `ContentType`, `Size`) and `DcTerms`
(`Creator`, `Created`, `Title`).

`MetadataProperty<object>` is **rejected in the constructor**. `IsSupportedClrType` returns `true`
for `object` because runtime dispatch in `From` needs it, but a `MetadataProperty<object>` would
defeat the type safety the descriptor exists to provide.

There is no `TryGetValue` on `MetadataCollection` — the nullable `this[XdmQName]` indexer already
expresses absence, and typed access is the `Get<T>` extension method, because C# cannot declare a
generic indexer on a non-generic type.

### Packaging

- Exception stack traces from shipped assemblies no longer embed the absolute build-machine path (a `PathMap` maps the repo root to a repo-relative prefix, so a frame reads `phoenixmldb-core/src/…` instead of a local filesystem path). Line numbers are preserved. Release builds only.
- Release notes are delivered to `dotnet pack` from this file by CI at tag time rather than duplicated in `Directory.Build.props`, removing a source of drift between the two.

## 1.3.0 — 2026-07-27

### XInclude 1.0 (complete): `parse="xml"` + `parse="text"` + `xi:fallback` + XPointer

Adds `PhoenixmlDb.Core.Xml.XIncludeProcessor`, a tree-to-tree pass that expands `<xi:include>` elements (XInclude namespace `http://www.w3.org/2001/XInclude`) in a parsed `XmlDocument` before it is converted to XDM. References resolve through a host-injectable `IXmlResourceResolver`; the default `LocalFileResourceResolver` resolves `file:`/relative URIs only and refuses remote (`http:`/`https:`/UNC) fetches unless `AllowRemote` is set, matching the engine's cautious posture (DTD processing stays prohibited on every reader, including the remote and text paths). The processor honours intervening `xml:base`, recurses into included content, detects cyclic inclusion and a configurable maximum include depth (both fatal), and performs XInclude §4.5 `xml:base`/`xml:lang` fixup (stamped with the reserved `xml` prefix, so the expanded DOM serializes and round-trips cleanly) so included nodes report their origin base URI.

`parse="text"` reads the target and decodes it by the `encoding` attribute → BOM → UTF-8, with `accept`/`accept-language` sent as HTTP headers on the remote (`AllowRemote`) path, and splices it as a text node. `xi:fallback` recovery is implemented per §4.6: a resource error (target unfetchable/unparsable/undecodable) falls back to a single `xi:fallback` child's content — itself XInclude-processed, honouring `xml:base` on the include/fallback; an empty fallback removes the include; absent a fallback it is fatal. `XIncludeException` carries an `XIncludeErrorKind` (`Cyclic`/`MaxDepthExceeded`/`ResourceError`/`MalformedInclude`/`MalformedFallback`/`Unsupported`). Additive only; XInclude is opt-in per document load and no public API was removed (`IXmlResourceResolver.ResolveText` gained `accept`/`acceptLanguage` parameters — a change for custom resolver implementers, pre-release).

**XPointer (completing XInclude 1.0):** an `xi:include`'s `xpointer`/`fragid` now selects a sub-resource via the full W3C XPointer Framework — shorthand (barename resolved via `xml:id`), `element()` (child-sequence), `xmlns()` (prefix binding), and `xpath1()` (XPath 1.0, evaluated by `System.Xml`'s built-in engine) — for both a fetched external target (`href` + `xpointer`) and same-document references (`xpointer`, no `href`, guarded against cyclic self-inclusion and bounded by `MaxIncludeDepth` against mutual recursion). The selected node-set replaces the `xi:include`, with per-element `xml:base`/`xml:lang` fixup; a pointer that selects nothing is a resource error (recoverable via `xi:fallback`), a malformed pointer or an attribute-node selection is fatal, and an unknown scheme part is skipped per the Framework. DTD-declared IDs remain unavailable (the resolver keeps DTD prohibited) — shorthand resolves via `xml:id`. Two documented limitations: for a *deep* selection (a pointer selecting a descendant of the fetched fragment) or a *same-document* selection, the §4.5 `xml:base` stamp uses the target's origin URI rather than folding an `xml:base` declared on an ancestor *above* the selected node within the fragment — this affects only relative-`href` resolution of a further nested include inside such a selection. This closes the last XInclude gap; XPointer is the feature that previously raised an "unsupported" error.

**Resource-safety hardening.** XInclude's threat model is untrusted input, so expansion is now bounded end-to-end against denial-of-service. `XIncludeOptions` gains four budget knobs, all default-on and treating `<= 0` as unlimited: `MaxExpansionDepth` (5000) bounds *every* recursive descent — the tree walk, the fallback-recovery chain, and same-document XPointer re-expansion — closing the gap where only the successful-fetch include stack was counted and any other recursion could `StackOverflow` the host; `MaxExpandedNodes` (10,000,000) caps total produced nodes so a `2ⁿ` same-document blow-up fails fatally instead of exhausting memory; `MaxResourceBytes` (64 MiB, on `LocalFileResourceResolver`) caps any single fetched resource, streaming-capped even when a remote response omits or misstates `Content-Length`; and `MaxXPathEvalMilliseconds` (5000) bounds each `xpath1()` evaluation. A depth/node/time breach raises the new fatal `XIncludeErrorKind.LimitExceeded` (not fallback-eligible); an oversized resource stays a fallback-eligible `ResourceError`. Because the generous default depth is far higher than a normal ~1 MiB thread stack survives on the frame-heavy fallback path, the whole expansion runs on a dedicated large-stack worker thread so the guard is genuinely reachable — the process throws a catchable exception rather than crashing. A fatal `XIncludeException` leaves the input document in an undefined state; callers must discard it. Additive only — no public API removed; the new options default to the bounds above.

### XML parser hardening & XDM correctness

The primary source-document parser (`XmlDocumentParser`) is hardened against untrusted input and corrected against the XDM data model:
- **Deep-nesting stack safety.** Element parsing recurses per nesting level; a deeply nested document (which `XmlReader` itself reads iteratively) could overflow the native thread stack — an uncatchable `StackOverflowException`. The recursion is now guarded with `RuntimeHelpers.EnsureSufficientExecutionStack()`, so a pathologically deep document raises a catchable exception before the stack is exhausted.
- **Internal-entity expansion cap.** All parse overloads now set `MaxCharactersFromEntities` (1,000,000), matching the sibling parsers, so a billion-laughs document cannot expand unboundedly into memory.
- **Adjacent character-data coalescing.** `XmlReader` splits character data at CDATA and entity boundaries; the parser now merges a run of consecutive Text/CDATA events into a single text node, honouring the XDM "no two consecutive text nodes" invariant (elements, comments, and PIs still break a run). Behaviour is preserved for every existing case.

## 1.2.2 — 2026-07-08

### Store-global tree ordinal for document order

Adds `XdmNode.TreeOrdinal` (a settable `ulong`, default 0) together with `XdmNode.CompareDocumentOrder(a, b)` and `XdmNode.DocumentOrderKey`, defining XDM document order as the pair `(TreeOrdinal, Id)`. `Id` (`NodeId`) is unique only within a single node store, so nodes drawn from independently-parsed sources could share an id; the tree ordinal groups nodes by their originating tree with a store-global counter, restoring a total, consistent order and a reliable identity key. The default of 0 leaves any node constructed without a store comparing purely by `Id`, so existing single-source ordering is unchanged. Additive only — no public API removed.

## 1.2.1 — 2026-07-07

### Linear string-value computation for large elements

String-value computation of large elements is now linear. The XML parser maintains an internal id-to-node index alongside its flat node list, so resolving each child while building an element's string value is a dictionary lookup instead of a per-child linear scan of every parsed node. Building the string value of an element with many children (and setting document-level child parents) no longer scales quadratically. Behavior is unchanged and no public API was removed.

## 1.2.0 — 2026-06-27

### Proleptic-Gregorian date arithmetic on `XsDate` / `XsDateTime`

Adds `AddMonths`, `AddDays`, and `Add(TimeSpan)` to `XsDate` and `XsDateTime`. These compute on the proleptic Gregorian calendar — correct across the year 1 / year 0 / negative-year boundary — using the existing `ExtendedYear` carrier rather than the underlying `DateOnly`/`DateTimeOffset`, which floor at year 1 and would overflow. Month arithmetic clamps the day to the target month's length; day and time arithmetic convert through a continuous day number (Howard Hinnant's `days_from_civil`/`civil_from_days`) so results below year 1 land on the right calendar date. A result year outside .NET's 1–9999 range is preserved in `ExtendedYear`. Additive only — no existing behavior changes and no public API removed.

## 1.1.9 — 2026-06-16

### Shared character escaper for output serialization

Adds `PhoenixmlDb.Xdm.Serialization.CharacterEscaper`, the single source of serialized-output character escaping (XML text, XML attribute, and the JSON per-character escape rules) shared by the XSLT and XQuery engines. It is `internal`, reaching both engines via the existing `InternalsVisibleTo`. The XSLT engine had grown one canonical copy internally; this lifts it to Core so the XQuery engine escapes identically, ending the divergence where attribute-value whitespace was escaped on some paths but not others. No public API change.

## 1.1.8 — 2026-06-13

### Fix: `XsTypedInteger` implements `IConvertible`

Derived integer types (`xs:long`, `xs:int`, `xs:short`, …) are carried as `XsTypedInteger`, which previously did not implement `IConvertible`. Any consumer routing such a value through `Convert.ToDouble` / `Convert.ToDecimal` / `Convert.ToInt64` got an `InvalidCastException`. The wrapper now delegates `IConvertible` to its underlying `long` (`ToDecimal`/`ToInt64` exact), so numeric operators and aggregate functions in PhoenixmlDb.XQuery 1.4.4 work over derived integer types. No API surface change.

## 1.1.7 — 2026-06-09

### Fix: restore binary compatibility for `XdmElement.Empty*` accessors

1.1.6 changed `EmptyAttributes`, `EmptyChildren`, and `EmptyNamespaceDeclarations`
from expression-bodied properties to `public static readonly` fields. The cache
behavior was correct, but **this was a binary-breaking change**: existing
consumers compiled against 1.1.5 (most notably `PhoenixmlDb.XQuery 1.4.x`)
invoke the `get_EmptyAttributes()` method, which no longer exists. Calls
throw `MissingMethodException` at runtime.

1.1.7 keeps the perf win — `ImmutableArray<T>.Empty` is boxed exactly once at
type init into a `private static readonly` field — and restores the original
property surface that consumers expect:

```csharp
private static readonly IReadOnlyList<NodeId> s_emptyAttributes = ImmutableArray<NodeId>.Empty;
public static IReadOnlyList<NodeId> EmptyAttributes => s_emptyAttributes;
```

**Anyone who already pinned 1.1.6 should upgrade.**

## 1.1.6 — 2026-06-09 (binary-breaking, do not use — see 1.1.7)

### Perf: `XdmElement.Empty*` accessors cached as static fields

`XdmElement.EmptyAttributes`, `EmptyChildren`, and `EmptyNamespaceDeclarations`
were expression-bodied properties returning `ImmutableArray<T>.Empty`. Because
the property return type is `IReadOnlyList<T>` and `ImmutableArray<T>` is a
struct, every read boxed the struct into a fresh heap object that wraps the
empty array. Switched to `public static readonly IReadOnlyList<T> = ImmutableArray<T>.Empty`
so the box happens exactly once at type init and all subsequent reads return
the same reference.

Surfaced while profiling PhoenixmlDb.Xslt streaming-identity (1M items): the
two `ImmutableArray<>` boxes accounted for ~20% of remaining streaming
allocation after the per-element pool work. No behavior change; existing
callers continue to work because the type and value of the static remain
`IReadOnlyList<NodeId>` / `IReadOnlyList<NamespaceBinding>`.

## 1.1.5 — 2026-05-23

### Fix: `XdmAttribute.TypeAnnotation` populated from schema validation

Element-level `TypeAnnotation` was wired in 1.1.4 (PR #59 — "populate
XdmElement/XdmAttribute.TypeAnnotation from XSD SchemaInfo") but the
attribute path actually only set the element annotation. Schema-validated
attributes arrived downstream still tagged `xs:untypedAtomic` regardless
of their XSD declaration.

`XmlDocumentParser.Parse(reader, documentUri, schemas)` now reads
`reader.SchemaInfo.MemberType` / `SchemaInfo.SchemaType` while positioned
on each attribute and resolves the result through the existing
`ResolveSchemaTypeAnnotation` helper. The non-schema-aware code path is
unaffected (SchemaInfo is null for non-validating readers, so the
fallback to `UntypedAtomic` kicks in).

Unblocks the schema-aware XQuery construction work
(`Constr-cont-constrmod-9/10` and related QT3 tests).

## 1.1.4 — 2026-05-22

### New: `XdmElement.TypeAnnotation` and `XdmAttribute.TypeAnnotation` from XSD SchemaInfo (PR #59)

Schema-validated documents loaded via `XdmDocumentStore.LoadFromStringWithSchema`
now carry XSD type annotations on element nodes. `XmlDocumentParser` reads
`reader.SchemaInfo.MemberType` / `SchemaInfo.SchemaType` while positioned on
each element and resolves through a `ResolveSchemaTypeAnnotation` helper.

Also multi-targets `net8.0;net10.0`.

## 1.1.3 — 2026-05-15

### New: `XsTypedInteger` wrapper for XSD integer subtype identity

Adds `XsTypedInteger` to carry XSD `integer` and its subtypes (`long`, `int`,
`short`, `byte`, `nonNegativeInteger`, etc.) through the XDM type system with
their original declared type preserved, enabling correct `instance of xs:long`
and similar type-identity checks.

## 1.1.2 — 2026-05-12

### New: `XdmSequence` — public wire-format for chaining transformations

A typed, ordered, store-carrying sequence wrapper. Engines (XSLT, XQuery)
return their typed results wrapped in an `XdmSequence`, and accept one as
input to chain transformations without serializing through XML markup.

For pure-atomic sequences `Store` is null and the value is fully
self-contained. For sequences containing `XdmNode` items, `Store` carries
the backing `INodeStore` so the receiving engine can navigate the source's
children. The store is typed as `object?` to keep `PhoenixmlDb.Core`
independent of engine-specific node-store implementations; engines
downcast to their concrete type (e.g. `XdmInMemoryStore` from
`PhoenixmlDb.Xslt`).

API surface:

- `XdmSequence.Empty` — the empty sequence singleton.
- `XdmSequence.Of(item)` — single-atomic wrapper.
- `XdmSequence.OfAtomics(items…)` — atomic-only sequence; rejects nodes.
- `XdmSequence.OfNode(node, store)` — single node + matching store.
- `XdmSequence.OfNodes(store, nodes…)` — multiple nodes sharing a store.
- `XdmSequence.FromEngineResult(items, store)` — for engine implementers.
- `IReadOnlyList<object?>` interface (Count, indexer, enumeration).
- Saxon-style accessors: `Head`, `Tail`, `IsEmpty`, `IsSingleNode`, `AsSingleNode()`.

Required by `PhoenixmlDb.Xslt 1.3.7` for the new
`TransformAsync(XdmSequence)` / `TransformToSequenceAsync` overloads.

## 1.1.1 — 2026-05-09

### Added
- `IContainer.QueryAsync(string, IReadOnlyDictionary<string,object>?, Predicate<string>?, CancellationToken)` overload accepts a document-name predicate to scope the query to a subset of the container. The 3-argument overload remains; the new one is a default interface method (DIM) that delegates to the unfiltered version when the implementer doesn't override.

### Why
Embedded hosts that store sidecar / system documents alongside user documents (notably the EPS server's `_eps_meta.json` metadata sidecar) need a way to hide them from user-issued XQuery without filtering result items after the fact. Per-document query iteration would otherwise visit the sidecar, evaluate the user's query against it, and return spurious extra items (e.g. `0` from `sum(())` aggregates). The new overload lets adapters pass `name => name != "<sidecar>"` and the iteration skips the sidecar before parse/load.

### Compatibility
Additive. Existing 3-argument callers compile and link unchanged. Existing implementers (only `PhoenixmlDb.Storage.Container` ships against this interface today) inherit the default implementation, which preserves the legacy unfiltered behavior.

## 1.1.0 — 2026-05-08

### Added
- `XdmNode.SourceLine` and `XdmNode.SourceColumn` (1-based; 0 = no source position).
- `XmlDocumentParser` populates source positions from `IXmlLineInfo` for elements, attributes, text, comments, and processing instructions.

### Compatibility
- Additive change — existing consumers (PhoenixmlDb.XQuery, PhoenixmlDb.Xslt, downstream NuGet packages) compile and run unchanged. The new fields default to 0; consumers that don't read them are unaffected.
- XQuery and XSLT packages do **not** need rebuilds for 1.1.0; they remain compatible with 1.0.30 binaries. New consumers that want source positions take 1.1.0.

### Known limitations
- Source positions are parse-time only. Round-trip via `Save` does not refresh them; the in-memory positions reflect the original parsed layout.
- Editor mutation helpers (`Rename`, `SetValue`, `Insert`) copy original positions to mutated nodes via C# object initializers. Logically correct until save+reparse.

---

## 1.0.29 (2026-04-30)

> Note: 1.0.26, 1.0.27, and 1.0.28 are content-equivalent placeholder versions on NuGet —
> they were each published from this same source ahead of the formal release. Resume
> shipping from 1.0.29.

### Features
- **DTD/XSD ID and IDREF type detection**: `XmlDocumentParser` now parses with DTD validation enabled (instead of `DtdProcessing.Ignore`) to detect `ID`, `IDREF`, and `IDREFS` attribute types. For XSD validation, a new `Parse(TextReader, string?, XmlSchemaSet)` overload populates type information from schema. DTD type info is read via reflection on the internal `SchemaType` property (the public `SchemaInfo` returns null for DTD validation).
- **`XdmAttribute.IsIdRef` property**: Indicates whether an attribute is declared as `IDREF` or `IDREFS` in the DTD or XSD schema. Enables `fn:idref()` XPath function support.
- **`XdmElement.IsIdContent` property**: Indicates whether an element's simple-content type is `xs:ID` or derived from `xs:ID` by restriction. Populated during XSD schema validation. Enables `fn:id()` and `fn:element-with-id()` to locate elements by typed content.

### Fixes
- **`QName.PrefixedName` empty prefix handling**: `PrefixedName` now checks `string.IsNullOrEmpty(Prefix)` instead of `Prefix is null`, preventing `:localName` output when prefix is empty string.

### Validation
- **Duration validation**: `xs:duration`, `xs:dayTimeDuration`, and `xs:yearMonthDuration` now reject invalid lexical forms (empty `P`, bare `T`, `H`/`S` without `T`).
- **Date/time validation**: `xs:time` and `xs:date` parsing validates per XML Schema spec.
- **Duration arithmetic**: `yearMonthDuration` multiply now rounds half toward positive infinity per spec.

## 1.0.0 (2026-03-20)

Initial release: Core types, XDM data model, and interfaces for PhoenixmlDb document database.
