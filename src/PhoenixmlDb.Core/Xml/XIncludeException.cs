using System;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Classifies an <see cref="XIncludeException"/> so callers (and the XSLT/XQuery engine
/// wirings) can map a failure to the appropriate engine error and tests can assert the cause.
/// </summary>
public enum XIncludeErrorKind
{
    /// <summary>A resource is included (directly or transitively) within itself.</summary>
    Cyclic,
    /// <summary>Nesting exceeded <see cref="XIncludeOptions.MaxIncludeDepth"/>.</summary>
    MaxDepthExceeded,
    /// <summary>The target could not be fetched, parsed, or decoded (fallback-eligible).</summary>
    ResourceError,
    /// <summary>The <c>xi:include</c> element itself is malformed (bad parse=, missing/fragment href, unsupported combo).</summary>
    MalformedInclude,
    /// <summary>An <c>xi:fallback</c> is misplaced or an <c>xi:include</c> has more than one.</summary>
    MalformedFallback,
    /// <summary>A feature not implemented in this build (XPointer — SP3).</summary>
    Unsupported,
}

/// <summary>
/// Thrown when XInclude processing (<c>xi:include</c> resolution) fails.
/// </summary>
/// <remarks>
/// <para>
/// XInclude 1.0 distinguishes fatal errors (e.g. an include that resolves to a resource
/// that cannot legally be included, or a security-policy violation such as a blocked remote
/// fetch) from recoverable errors that fall back to <c>xi:fallback</c> content when present.
/// <see cref="IsFatal"/> records which case applies.
/// </para>
/// </remarks>
public class XIncludeException : Exception
{
    /// <summary>
    /// <see langword="true"/> when this error must abort processing outright;
    /// <see langword="false"/> when it is recoverable via an <c>xi:fallback</c> element.
    /// </summary>
    public bool IsFatal { get; }

    /// <summary>The category of this XInclude error.</summary>
    public XIncludeErrorKind Kind { get; }

    public XIncludeException(XIncludeErrorKind kind, bool isFatal, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        IsFatal = isFatal;
    }

    public XIncludeException(bool isFatal, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = XIncludeErrorKind.MalformedInclude;
        IsFatal = isFatal;
    }

    public XIncludeException()
    {
        Kind = XIncludeErrorKind.MalformedInclude;
    }

    public XIncludeException(string message) : base(message)
    {
        Kind = XIncludeErrorKind.MalformedInclude;
    }

    public XIncludeException(string message, Exception innerException) : base(message, innerException)
    {
        Kind = XIncludeErrorKind.MalformedInclude;
    }
}
