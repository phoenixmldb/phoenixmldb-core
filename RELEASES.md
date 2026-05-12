# Release History

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
