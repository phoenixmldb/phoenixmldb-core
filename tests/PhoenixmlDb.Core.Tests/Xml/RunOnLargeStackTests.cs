using System;
using System.Threading;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

/// <summary>
/// Deterministic cover for the join-timeout that bounds xpath1() evaluation during XInclude
/// expansion. <see cref="XPointerEvaluatorTests.Xpath1_over_time_budget_throws_LimitExceeded"/>
/// exercises the same guarantee end to end, but does so by assuming a particular XPath is
/// slower than the budget — an assumption that failed in CI on 2026-08-23 for reasons that were
/// never explained. These tests make the work take a known amount of time instead of hoping it
/// does, so the timeout is proven rather than raced.
/// </summary>
public sealed class RunOnLargeStackTests
{
    [Fact]
    public void Work_that_outlives_the_budget_raises_LimitExceeded()
    {
        // The worker blocks until the test releases it, so it CANNOT finish inside the budget
        // regardless of machine speed, runtime version, or CI load.
        using var release = new ManualResetEventSlim(false);
        try
        {
            var act = () => XIncludeProcessor.RunOnLargeStack(
                () => { release.Wait(TimeSpan.FromSeconds(30)); return 1; },
                "test-timeout",
                joinTimeoutMs: 1,
                "budget exceeded");

            act.Should().Throw<XIncludeException>()
                .Which.Kind.Should().Be(XIncludeErrorKind.LimitExceeded);
        }
        finally
        {
            // Let the abandoned worker exit. It is a background thread so it would not hold the
            // process open, but leaving it parked for 30 s across a whole suite run is rude.
            release.Set();
        }
    }

    [Fact]
    public void Timeout_message_and_fatality_are_carried_through()
    {
        using var release = new ManualResetEventSlim(false);
        try
        {
            var act = () => XIncludeProcessor.RunOnLargeStack(
                () => { release.Wait(TimeSpan.FromSeconds(30)); return 1; },
                "test-timeout-message",
                joinTimeoutMs: 1,
                "xpath1() evaluation exceeded 1 ms.");

            var ex = act.Should().Throw<XIncludeException>().Which;
            ex.Message.Should().Contain("xpath1() evaluation exceeded 1 ms.");
            // Fatal matters: a non-fatal XInclude error is recoverable via xi:fallback, and a
            // resource-limit breach must never be silently fallen back to.
            ex.IsFatal.Should().BeTrue();
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public void Work_inside_the_budget_returns_its_value()
    {
        var result = XIncludeProcessor.RunOnLargeStack(
            () => 42, "test-fast", joinTimeoutMs: 30_000, "unused");

        result.Should().Be(42);
    }

    [Fact]
    public void Work_that_throws_rethrows_the_original_exception()
    {
        // The worker marshals failures back with ExceptionDispatchInfo rather than wrapping
        // them, which is what lets EvaluateXPath1 catch XPathException and classify a bad
        // expression as MalformedInclude. If this ever starts wrapping, that classification
        // silently stops working and every invalid xpath1() becomes an unhandled error.
        var act = () => XIncludeProcessor.RunOnLargeStack<int>(
            () => throw new InvalidOperationException("from the worker"),
            "test-throws", joinTimeoutMs: 30_000, "unused");

        act.Should().Throw<InvalidOperationException>().WithMessage("from the worker");
    }
}
