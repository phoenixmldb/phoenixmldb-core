using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML comment.
/// </summary>
public sealed class XdmComment : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Comment;

    /// <summary>
    /// The comment text (without delimiters).
    /// </summary>
    public required string Value { get; init; }

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.XsString(Value);
}
