# PhoenixmlDb Core

Core types, XDM (XQuery Data Model) implementation, and storage abstractions for the [PhoenixML](https://github.com/phoenixmldb) XML database platform.

## Packages

| Package | Description |
|---------|-------------|
| **PhoenixmlDb.Core** | Core identifiers, QName, NamespaceId, and storage abstraction interfaces |
| **PhoenixmlDb.Xdm** | XQuery Data Model — node types (Document, Element, Attribute, Text, etc.), serialization, and parsing |

## Installation

```bash
dotnet add package PhoenixmlDb.Core
dotnet add package PhoenixmlDb.Xdm
```

## Key Types

### PhoenixmlDb.Core
- `ContainerId`, `DocumentId`, `NodeId`, `NamespaceId` — strongly-typed identifiers
- `QName` — XML qualified name with namespace resolution
- `IStorageEngine`, `IDatabase`, `IStorageTransaction` — storage abstraction interfaces

### PhoenixmlDb.Xdm
- `XdmDocument`, `XdmElement`, `XdmAttribute`, `XdmText` — XDM node types
- `XdmNode` — abstract base for all node kinds
- Node serialization and parsing utilities

## License

Apache 2.0 — see [LICENSE](LICENSE)

## Related Projects

- [phoenixmldb-xquery](https://github.com/phoenixmldb/phoenixmldb-xquery) — XPath/XQuery 4.0 engine
- [phoenixmldb-xslt](https://github.com/phoenixmldb/phoenixmldb-xslt) — XSLT 4.0 engine
- [phoenixmldb-cli](https://github.com/phoenixmldb/phoenixmldb-cli) — CLI tools
