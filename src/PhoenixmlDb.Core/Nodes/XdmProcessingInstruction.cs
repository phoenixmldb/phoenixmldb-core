using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML processing instruction (<c>&lt;?target data?&gt;</c>) in the XDM tree.
/// </summary>
/// <remarks>
/// <para>
/// Processing instructions provide a mechanism to pass information to applications.
/// The <see cref="Target"/> identifies the application, and the <see cref="Value"/>
/// contains the instruction data. Common examples include <c>&lt;?xml-stylesheet ... ?&gt;</c>.
/// </para>
/// <para>
/// PI nodes have a <see cref="XdmNode.NodeName"/> with no namespace — just the target as
/// the local name. Their <see cref="XdmNode.TypedValue"/> is <c>xs:string</c>, per the
/// XDM specification.
/// </para>
/// </remarks>
public sealed class XdmProcessingInstruction : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.ProcessingInstruction;

    /// <summary>
    /// The target (name) of the processing instruction, identifying the intended application.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// The content (data) of the processing instruction, following the target and whitespace.
    /// </summary>
    public required string Value { get; init; }

    public override XdmQName? NodeName => new XdmQName(NamespaceId.None, Target, null);

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.XsString(Value);
}
