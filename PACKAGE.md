# PhoenixmlDb.Core

Core types, interfaces, and XDM (XQuery Data Model) implementation for [PhoenixmlDb](https://phoenixml.dev) — a modern embedded XML/JSON document database for .NET.

## What's in this package

- **Database interfaces** — `IDocumentDatabase`, `IContainer`, `IDocument` for storing and retrieving XML/JSON documents
- **XDM node types** — `XdmElement`, `XdmAttribute`, `XdmDocument`, etc. — the W3C XQuery Data Model
- **Transaction model** — `IReadTransaction`, `IWriteTransaction` with MVCC snapshot isolation
- **Index configuration** — path, value, full-text, and metadata indexes for fast queries
- **XML parser and serializer** — parse XML into XDM trees and serialize back
- **Atomic value types** — dates, times, durations, and other XSD types with correct semantics

## When to use this package

**Directly:** If you're building a storage provider, query engine integration, or tooling that works with XDM types.

**Indirectly:** This package is a dependency of `PhoenixmlDb.XQuery`, `PhoenixmlDb.Xslt`, and the CLI tools. You typically don't reference it alone unless you need the core types without query/transform capabilities.

## Quick example

```csharp
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Parsing;

// Parse XML into an XDM tree
var parser = new XmlDocumentParser();
var result = parser.Parse("<order><item>Widget</item></order>");
var doc = result.Document;

// Navigate the tree
var root = doc.Children[0]; // <order> element
Console.WriteLine(root.StringValue); // "Widget"
```

## Related packages

| Package | Description |
|---------|-------------|
| **PhoenixmlDb.XQuery** | XQuery 4.0 query engine |
| **PhoenixmlDb.Xslt** | XSLT 4.0 transformation engine |
| **PhoenixmlDb.XQuery.Cli** | `xquery` command-line tool |
| **PhoenixmlDb.Xslt.Cli** | `xslt` command-line tool |

## Documentation

Full documentation at [phoenixml.dev](https://phoenixml.dev)

## License

Apache 2.0
