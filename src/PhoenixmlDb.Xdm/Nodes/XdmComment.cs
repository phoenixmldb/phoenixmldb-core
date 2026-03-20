using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML comment node (<c>&lt;!-- ... --&gt;</c>) in the XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// Comment nodes store the text content between the <c>&lt;!--</c> and <c>--&gt;</c>
/// delimiters. The delimiters themselves are not included in <see cref="Value"/>.
/// </para>
/// <para>
/// Comments can appear as children of document or element nodes. Their
/// <see cref="XdmNode.TypedValue"/> is <c>xs:string</c> (not <c>xs:untypedAtomic</c>),
/// per the XDM specification.
/// </para>
/// </remarks>
public sealed class XdmComment : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.Comment;

    /// <summary>
    /// The comment text, excluding the <c>&lt;!--</c> and <c>--&gt;</c> delimiters.
    /// </summary>
    public required string Value { get; init; }

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.XsString(Value);
}
