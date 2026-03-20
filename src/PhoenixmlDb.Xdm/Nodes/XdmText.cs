using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents a text node containing character data content in the XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// Text nodes hold the character data content of XML elements. Adjacent text nodes are
/// merged during parsing (as required by the XDM specification), so a text node's
/// <see cref="Value"/> always represents a complete run of character data.
/// </para>
/// <para>
/// Empty text nodes are never stored — the parser discards them. CDATA sections are
/// merged into regular text nodes during parsing, as the XDM does not distinguish between
/// CDATA and regular text content.
/// </para>
/// </remarks>
public sealed class XdmText : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Text;

    /// <summary>
    /// The text content. This is never empty — empty text nodes are discarded during parsing.
    /// </summary>
    public required string Value { get; init; }

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(Value);
}
