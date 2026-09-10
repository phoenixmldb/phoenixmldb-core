using System.Runtime.CompilerServices;
using PhoenixmlDb.Xdm.Nodes;

/// <summary>
/// Runs this test assembly with <see cref="XdmNode.StrictStringValue"/> ENABLED, so that reading
/// the string value of a node which has neither a computed value nor a resolver fails loudly
/// instead of returning the empty string.
/// </summary>
/// <remarks>
/// <para>
/// The flag ships OFF, so no consumer of the package changes behaviour on upgrade. That default
/// is also the defect it exists for: "not computed yet" and "genuinely empty" are the same
/// observable value, which is how storage-backed aggregates came to return wrong answers with no
/// error raised (phoenixmldb/phoenixmldb-core#4).
/// </para>
/// <para>
/// A strict mode nobody enables catches nothing, so enabling it here is the point rather than an
/// option — these suites are what convert that class of defect into a failing test. Measured
/// before switching it on: with strict enabled globally the ONLY failures across all 516 Xdm
/// tests were the four that assert the empty-string fallback, and no production path broke. Those
/// tests now pin the flag off explicitly via <c>NonStrictStringValue</c>, because a test
/// documenting the default should say so rather than depend on the ambient setting.
/// </para>
/// </remarks>
internal static class StrictStringValueEnabler
{
    [ModuleInitializer]
    internal static void Enable() => XdmNode.StrictStringValue = true;
}
