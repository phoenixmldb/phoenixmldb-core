namespace PhoenixmlDb.Xdm;

/// <summary>
/// Lightweight text node marker for sequence accumulators.
/// Distinguishes text nodes (from xsl:text, xsl:value-of, literal text)
/// from atomic string values (from xsl:sequence, XPath expressions).
/// Used for correct XSLT 3.0 §5.7.2 simple content construction.
/// </summary>
public sealed record TextNodeItem(string Value)
{
    public override string ToString() => Value;
}
