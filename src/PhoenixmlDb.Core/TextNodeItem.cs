namespace PhoenixmlDb.Xdm;

/// <summary>
/// Lightweight text node marker for XSLT sequence accumulators, distinguishing text nodes
/// from atomic string values.
/// </summary>
/// <remarks>
/// <para>
/// During XSLT processing, sequences can contain both text nodes (from <c>xsl:text</c>,
/// <c>xsl:value-of</c>, literal text) and atomic string values (from <c>xsl:sequence</c>,
/// XPath expressions). These must be handled differently during simple content construction
/// per XSLT 3.0 §5.7.2: adjacent text nodes are merged without separators, while atomic
/// values are joined with spaces.
/// </para>
/// <para>
/// This type wraps a string value and acts as a type-level tag to distinguish text node
/// contributions from atomic value contributions in the sequence.
/// </para>
/// </remarks>
/// <param name="Value">The text content of the text node.</param>
public sealed record TextNodeItem(string Value)
{
    public override string ToString() => Value;
}
