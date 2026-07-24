using System;

namespace PhoenixmlDb.Core.Xml;

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

    public XIncludeException(bool isFatal, string message, Exception? inner = null)
        : base(message, inner)
    {
        IsFatal = isFatal;
    }

    public XIncludeException()
    {
    }

    public XIncludeException(string message) : base(message)
    {
    }

    public XIncludeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
