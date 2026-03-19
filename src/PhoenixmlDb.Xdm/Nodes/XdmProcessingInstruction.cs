using PhoenixmlDb.Core;

namespace PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Represents an XML processing instruction.
/// </summary>
public sealed class XdmProcessingInstruction : XdmNode
{
    public override XdmNodeKind NodeKind => XdmNodeKind.ProcessingInstruction;

    /// <summary>
    /// The target (name) of the PI.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// The content of the PI.
    /// </summary>
    public required string Value { get; init; }

    public override XdmQName? NodeName => new XdmQName(NamespaceId.None, Target, null);

    public override string StringValue => Value;

    public override XdmValue TypedValue => XdmValue.XsString(Value);
}
