using System;
using System.Xml;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// Host-injectable seam for resolving the resources referenced by an XInclude
/// <c>xi:include</c> element (<c>href</c>, optionally combined with <c>parse</c>).
/// </summary>
/// <remarks>
/// <para>
/// Implementations decide how (and whether) a given absolute <see cref="Uri"/> may be
/// dereferenced. The default engine-provided implementation is
/// <see cref="LocalFileResourceResolver"/>, which resolves <c>file:</c> URIs and blocks
/// remote (e.g. <c>http:</c>/<c>https:</c>) URIs unless explicitly allowed. Hosts may supply
/// their own implementation via <see cref="XIncludeOptions.Resolver"/> to add caching,
/// sandboxing, or support for custom URI schemes.
/// </para>
/// </remarks>
public interface IXmlResourceResolver
{
    /// <summary>
    /// Resolves <paramref name="absolute"/> for an <c>xi:include</c> with
    /// <c>parse="xml"</c> (the default), returning a reader positioned to parse the
    /// referenced resource as XML.
    /// </summary>
    /// <param name="absolute">The absolute URI to resolve.</param>
    /// <returns>An <see cref="XmlReader"/> over the resolved resource.</returns>
    XmlReader ResolveXml(Uri absolute);

    /// <summary>
    /// Resolves <paramref name="absolute"/> for an <c>xi:include</c> with
    /// <c>parse="text"</c>, returning the resource's content decoded as text.
    /// </summary>
    /// <param name="absolute">The absolute URI to resolve.</param>
    /// <param name="encoding">
    /// The character encoding name specified by the <c>encoding</c> attribute, or
    /// <see langword="null"/> to auto-detect / use the resource's declared encoding.
    /// </param>
    /// <returns>The resolved resource's content as text.</returns>
    string ResolveText(Uri absolute, string? encoding);
}
