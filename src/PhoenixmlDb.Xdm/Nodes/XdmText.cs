using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents character data content.
/// </summary>
public sealed class XdmText : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Text;

    /// <summary>
    /// The text content. Never empty (empty text nodes are not stored).
    /// </summary>
    public required string Value { get; init; }

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.UntypedAtomic(Value);
}
