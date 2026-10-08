using System.Xml.Schema;

namespace PhoenixmlDb.Core.Schema;

/// <summary>One document of a compiled schema, and the version of it that was compiled.</summary>
/// <param name="Uri">The absolute URI of the document.</param>
/// <param name="Version">The token its <see cref="SchemaSource"/> or the gate gave for its content.</param>
public readonly record struct SchemaDocumentVersion(Uri Uri, string Version);

/// <summary>
/// A schema compiled from one or more root documents and everything they include, import or
/// redefine. It does not change: a schema whose documents change is compiled again, into a new
/// instance.
/// </summary>
public sealed class CompiledSchema
{
    internal CompiledSchema(XmlSchemaSet schemaSet, IReadOnlyList<Uri> roots,
        IReadOnlyList<SchemaDocumentVersion> documents, IReadOnlyList<SchemaDiagnostic> warnings, string gateIdentity)
    {
        SchemaSet = schemaSet;
        Roots = roots;
        Documents = documents;
        Warnings = warnings;
        GateIdentity = gateIdentity;
    }

    /// <summary>
    /// The compiled set, for validating against. It is shared: many threads validate against it at
    /// once, which is safe only while nobody changes it. <b>Do not add to it, remove from it or
    /// compile it again.</b>
    /// </summary>
    public XmlSchemaSet SchemaSet { get; }

    /// <summary>The documents the schema was asked for by.</summary>
    public IReadOnlyList<Uri> Roots { get; }

    /// <summary>
    /// Every document that was read: the roots and the whole closure of what they refer to, each
    /// with the version that was compiled. A schema is out of date when any of them has changed,
    /// not only a root.
    /// </summary>
    public IReadOnlyList<SchemaDocumentVersion> Documents { get; }

    /// <summary>Warnings the compilation reported. A schema with errors is never constructed.</summary>
    public IReadOnlyList<SchemaDiagnostic> Warnings { get; }

    /// <summary>The <see cref="ISchemaAccessGate.Identity"/> of the gate its documents were read through.</summary>
    public string GateIdentity { get; }
}
