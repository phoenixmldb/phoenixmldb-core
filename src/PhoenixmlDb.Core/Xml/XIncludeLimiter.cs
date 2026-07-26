namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Enforces XInclude expansion resource limits (descent depth, produced-node budget) and carries
/// the <c>xpath1()</c> evaluation timeout. One instance is created per <see cref="XIncludeProcessor.Expand"/>
/// call and threaded through every recursive path, so a bound cannot be silently skipped on one
/// path (the root cause of the resource-safety audit findings). Single-threaded; not reusable.
/// </summary>
internal sealed class XIncludeLimiter
{
    private readonly int _maxDepth;       // <= 0 → unlimited
    private readonly long _maxNodes;      // <= 0 → unlimited
    private long _nodesConsumed;
    private int _depth;

    public XIncludeLimiter(XIncludeOptions options)
    {
        _maxDepth = options.MaxExpansionDepth;
        _maxNodes = options.MaxExpandedNodes;
        XPathTimeoutMs = options.MaxXPathEvalMilliseconds;
    }

    /// <summary>The per-<c>xpath1()</c> wall-clock budget in ms (&lt;= 0 = unlimited).</summary>
    public int XPathTimeoutMs { get; }

    /// <summary>Increments the descent depth; throws fatal <see cref="XIncludeErrorKind.LimitExceeded"/> past the cap.</summary>
    public void EnterExpansion()
    {
        _depth++;
        if (_maxDepth > 0 && _depth > _maxDepth)
        {
            throw new XIncludeException(XIncludeErrorKind.LimitExceeded, isFatal: true,
                $"XInclude expansion depth exceeded MaxExpansionDepth ({_maxDepth}).");
        }
    }

    /// <summary>Decrements the descent depth. Call in a <c>finally</c> paired with <see cref="EnterExpansion"/>.</summary>
    public void ExitExpansion() => _depth--;

    /// <summary>Charges <paramref name="n"/> produced nodes against the budget; throws fatal <see cref="XIncludeErrorKind.LimitExceeded"/> on breach.</summary>
    public void ConsumeNodes(long n)
    {
        if (_maxNodes <= 0) return;
        _nodesConsumed += n;
        if (_nodesConsumed > _maxNodes)
        {
            throw new XIncludeException(XIncludeErrorKind.LimitExceeded, isFatal: true,
                $"XInclude produced more than MaxExpandedNodes ({_maxNodes}) nodes.");
        }
    }
}
