# Release History

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
