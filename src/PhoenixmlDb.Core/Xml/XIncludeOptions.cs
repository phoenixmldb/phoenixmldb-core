namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Configures XInclude 1.0 (<c>xi:include</c>) processing.
/// </summary>
public sealed class XIncludeOptions
{
    /// <summary>
    /// Whether XInclude processing is performed at all. Defaults to <see langword="false"/>
    /// (opt-in).
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether remote (non-local-file) resources may be included. Defaults to
    /// <see langword="false"/> — only local file resources are resolved unless explicitly
    /// enabled. Applies when <see cref="Resolver"/> is <see langword="null"/> and the default
    /// <see cref="LocalFileResourceResolver"/> is used; a custom <see cref="Resolver"/> is
    /// responsible for enforcing its own remote-access policy.
    /// </summary>
    public bool AllowRemote { get; init; }

    /// <summary>
    /// The maximum depth of nested <c>xi:include</c> resolution, guarding against runaway or
    /// circular includes. Defaults to 40.
    /// </summary>
    public int MaxIncludeDepth { get; init; } = 40;

    /// <summary>
    /// Maximum tree-descent recursion depth during expansion (stack-safety bound, distinct from
    /// <see cref="MaxIncludeDepth"/> which bounds nested includes). Guards against StackOverflow
    /// from deeply nested content, fallback chains, or same-document recursion. Default 5000;
    /// <c>&lt;= 0</c> = unlimited. Expansion runs on a worker thread with a large explicit stack
    /// sized to accommodate this default comfortably; raising this value far above the default (or
    /// setting it to unlimited on adversarial input) can exceed that stack and reintroduce an
    /// <em>uncatchable</em> <see cref="System.StackOverflowException"/> that crashes the process —
    /// keep it bounded and near the default range.
    /// </summary>
    public int MaxExpansionDepth { get; init; } = 5000;

    /// <summary>
    /// Maximum total number of nodes produced by expansion (size-safety bound). Guards against
    /// exponential (billion-laughs-style) blow-up. Default 10,000,000; <c>&lt;= 0</c> = unlimited.
    /// </summary>
    public long MaxExpandedNodes { get; init; } = 10_000_000;

    /// <summary>
    /// Maximum size, in characters/bytes, of a single fetched resource. Guards against one small
    /// include pulling in an enormous resource. Enforced by <see cref="LocalFileResourceResolver"/>;
    /// a custom <see cref="Resolver"/> enforces its own. Default 64 MiB; <c>&lt;= 0</c> = unlimited.
    /// </summary>
    public long MaxResourceBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Wall-clock budget, in milliseconds, for a single XPointer <c>xpath1()</c> evaluation.
    /// Guards against a pathological XPath over a large document. Default 5000; <c>&lt;= 0</c> =
    /// unlimited. Note: System.Xml's XPath engine cannot be cancelled mid-evaluation, so this
    /// bounds the caller's wall-clock and raises a fatal error at the deadline; an abandoned
    /// evaluation runs to completion on a dedicated background thread with a large stack (off the
    /// thread pool, so it starves neither pooled work nor the caller; XPath 1.0 always terminates).
    /// Because that thread keeps reading the document being expanded, a document that hits this
    /// deadline MUST be discarded (see <see cref="XIncludeProcessor.Expand"/>), not reused.
    /// </summary>
    public int MaxXPathEvalMilliseconds { get; init; } = 5000;

    /// <summary>
    /// The resource resolver used to dereference <c>xi:include</c> targets. When
    /// <see langword="null"/>, a <see cref="LocalFileResourceResolver"/> configured with
    /// <see cref="AllowRemote"/> is used.
    /// </summary>
    public IXmlResourceResolver? Resolver { get; init; }
}
