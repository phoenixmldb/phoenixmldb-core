using System;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Pins <see cref="XdmNode.StrictStringValue"/> OFF for the duration of a test, and restores it.
/// </summary>
/// <remarks>
/// A test that asserts the empty-string fallback is documenting the DEFAULT behaviour, not
/// behaviour that holds under every setting. Saying so explicitly is what lets the suite run with
/// strict mode enabled globally — measured: with strict on, the only failures across all 516
/// tests were the ones asserting that fallback, and no production path broke. Without this the
/// flag would be a strict mode nobody could turn on, which catches nothing.
/// </remarks>
internal sealed class NonStrictStringValue : IDisposable
{
    private readonly bool _saved;

    public NonStrictStringValue()
    {
        _saved = XdmNode.StrictStringValue;
        XdmNode.StrictStringValue = false;
    }

    public void Dispose() => XdmNode.StrictStringValue = _saved;
}
