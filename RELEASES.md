# Release History

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
