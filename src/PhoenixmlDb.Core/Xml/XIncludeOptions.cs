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
    /// The resource resolver used to dereference <c>xi:include</c> targets. When
    /// <see langword="null"/>, a <see cref="LocalFileResourceResolver"/> configured with
    /// <see cref="AllowRemote"/> is used.
    /// </summary>
    public IXmlResourceResolver? Resolver { get; init; }
}
