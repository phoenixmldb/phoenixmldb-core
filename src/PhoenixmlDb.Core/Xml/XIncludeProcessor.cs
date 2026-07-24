using System;
using System.Collections.Generic;
using System.Xml;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// XInclude 1.0 processor (SP1 scope): expands <c>xi:include</c> elements with
/// <c>parse="xml"</c> and an <c>href</c>, resolving each reference against the in-scope
/// base URI, recursing into included content, and detecting cyclic / over-deep inclusion.
/// </summary>
/// <remarks>
/// <para>
/// SP1 deliberately covers only structural inclusion. The following XInclude features are
/// out of scope for this build and raise a fatal <see cref="XIncludeException"/> when
/// encountered:
/// </para>
/// <list type="bullet">
///   <item><description><c>xpointer</c> / <c>fragid</c> sub-resource selection (SP2/SP3).</description></item>
///   <item><description><c>parse="text"</c> textual inclusion (SP2).</description></item>
/// </list>
/// <para>
/// <c>xi:fallback</c> recovery (SP2) IS implemented: a resource error on an <c>xi:include</c>
/// (fetch or parse failure) recovers via a single <c>xi:fallback</c> child's content when
/// present — that content is itself XInclude-processed and replaces the include (an empty
/// fallback simply removes it) — or, absent a fallback, rethrows fatally as before. A
/// misplaced <c>xi:fallback</c> (not a direct child of an <c>xi:include</c>) or more than one
/// on the same <c>xi:include</c> is always a fatal error.
/// </para>
/// <para>
/// <c>xml:base</c> / <c>xml:lang</c> fixup (XInclude 1.0 §4.5): when a top-level included
/// element is spliced into the master, it is stamped with <c>xml:base</c> = the resolved
/// target URI (unless it already carries its own <c>xml:base</c>) and, if it lacks its own
/// <c>xml:lang</c>, with the in-scope <c>xml:lang</c> from the include's ancestor chain (if
/// any). Only the top-level included element is stamped — descendants resolve their base/lang
/// through the added attribute plus their own existing <c>xml:base</c>/<c>xml:lang</c> chain.
/// </para>
/// </remarks>
public static class XIncludeProcessor
{
    /// <summary>The XInclude 1.0 namespace URI.</summary>
    public const string XIncludeNamespace = "http://www.w3.org/2001/XInclude";

    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>
    /// Expands every <c>xi:include</c> (<c>parse="xml"</c>, <c>href</c>) in
    /// <paramref name="doc"/>, in place, and returns the same mutated document.
    /// </summary>
    /// <param name="doc">The document to expand. Mutated in place.</param>
    /// <param name="baseUri">The absolute base URI of <paramref name="doc"/>, against which
    /// relative <c>href</c>s (as adjusted by any in-scope <c>xml:base</c>) are resolved.</param>
    /// <param name="options">XInclude processing options (resolver, remote policy, depth).</param>
    /// <returns>The same <paramref name="doc"/>, with its <c>xi:include</c>s expanded.</returns>
    /// <exception cref="XIncludeException">
    /// Thrown (always fatal in SP1) on a malformed <c>xi:include</c>, an unsupported feature
    /// (xpointer / <c>parse="text"</c>), a cyclic or over-deep inclusion, or a resource error.
    /// </exception>
    public static XmlDocument Expand(XmlDocument doc, Uri baseUri, XIncludeOptions options)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(options);

        var resolver = options.Resolver ?? new LocalFileResourceResolver { AllowRemote = options.AllowRemote };

        // Active-inclusion URI stack: the master document's own URI plus every ancestor
        // target currently being expanded. A target URI already on the stack is a cycle.
        var activeStack = new List<Uri> { baseUri };

        ExpandNode(doc, doc, baseUri, options, resolver, activeStack);
        return doc;
    }

    /// <summary>
    /// Depth-first walks <paramref name="node"/>'s children in <paramref name="masterDoc"/>,
    /// expanding any <c>xi:include</c> elements. <paramref name="baseUri"/> is the in-scope
    /// base for <paramref name="node"/> itself (already adjusted for the ancestors above it).
    /// </summary>
    private static void ExpandNode(
        XmlDocument masterDoc,
        XmlNode node,
        Uri baseUri,
        XIncludeOptions options,
        IXmlResourceResolver resolver,
        List<Uri> activeStack)
    {
        var child = node.FirstChild;
        while (child is not null)
        {
            // Capture the next sibling now: `child` may be replaced/removed below.
            var next = child.NextSibling;

            if (child is XmlElement element)
            {
                if (IsXIncludeInclude(element))
                {
                    ProcessInclude(masterDoc, element, baseUri, options, resolver, activeStack);
                }
                else if (IsXIncludeFallback(element))
                {
                    // A fallback that IS a child of an xi:include is consumed inside
                    // ProcessInclude before the walk ever descends into that xi:include (the
                    // include is replaced/removed wholesale first), so reaching an xi:fallback
                    // here means it is not a child of an xi:include — genuinely misplaced.
                    throw new XIncludeException(
                        XIncludeErrorKind.MalformedFallback,
                        isFatal: true,
                        "xi:fallback must be a child of xi:include.");
                }
                else
                {
                    // Recurse into ordinary elements, carrying any xml:base they declare.
                    var childBase = AdjustBase(baseUri, element);
                    ExpandNode(masterDoc, element, childBase, options, resolver, activeStack);
                }
            }

            child = next;
        }
    }

    private static bool IsXIncludeInclude(XmlElement element) =>
        string.Equals(element.NamespaceURI, XIncludeNamespace, StringComparison.Ordinal)
        && string.Equals(element.LocalName, "include", StringComparison.Ordinal);

    private static bool IsXIncludeFallback(XmlNode node) =>
        node is XmlElement e
        && string.Equals(e.NamespaceURI, XIncludeNamespace, StringComparison.Ordinal)
        && string.Equals(e.LocalName, "fallback", StringComparison.Ordinal);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The fetch/parse of an xi:include target can fail with any resolver- " +
            "or XmlReader-specific exception (I/O, network, malformed XML, ...); every such " +
            "failure is a resource error that must be routed through RecoverWithFallback " +
            "(fallback recovery or a fatal rethrow), so catching Exception broadly here is " +
            "intentional, not a swallowed error.")]
    private static void ProcessInclude(
        XmlDocument masterDoc,
        XmlElement include,
        Uri baseUri,
        XIncludeOptions options,
        IXmlResourceResolver resolver,
        List<Uri> activeStack)
    {
        var href = include.HasAttribute("href") ? include.GetAttribute("href") : null;
        var parse = include.HasAttribute("parse") ? include.GetAttribute("parse") : "xml";
        var hasXPointer = include.HasAttribute("xpointer") || include.HasAttribute("fragid");

        // Unsupported SP2/SP3 features: xpointer/fragid sub-selection and parse="text".
        if (hasXPointer || string.Equals(parse, "text", StringComparison.Ordinal))
        {
            throw new XIncludeException(
                XIncludeErrorKind.Unsupported,
                isFatal: true,
                "xpointer/parse=text not supported in this build (SP2/SP3)");
        }

        if (!string.Equals(parse, "xml", StringComparison.Ordinal))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                $"xi:include has invalid parse='{parse}'.");
        }

        // With no xpointer, href is required (an xpointer-only include, referencing the
        // same document, is the only case where href may be absent — and that path is the
        // unsupported xpointer branch above). Missing href here is a fatal error.
        if (string.IsNullOrEmpty(href))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                "xi:include is missing required 'href'.");
        }

        // XInclude 1.0 §4.2: a fragment identifier in href is a fatal error (fragments select
        // into the parsed result, not the resource itself, and SP1 does not support xpointer
        // sub-resource selection at all). Checked against the raw href string, per RFC 3986,
        // rather than the resolved Uri's .Fragment: System.Uri's fragment parsing for combined
        // relative references is unreliable for "file" URIs whose base was constructed from a
        // bare path string (new Uri(path), as opposed to new Uri("file://...")) — the '#' gets
        // silently folded into the path (percent-encoded) instead of split off as a fragment,
        // even though the two Uri instances print identically. Scanning href up front sidesteps
        // that footgun entirely and matches the spec text (href's fragment, not the base's).
        if (href.Contains('#', StringComparison.Ordinal))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                "fragment identifier in href is not allowed (XInclude 1.0 §4.2)");
        }

        // Per XML Base, an xml:base attribute on the element carrying a URI-valued attribute
        // (here, href) applies to that attribute — so the xi:include element's OWN xml:base
        // (if present) must be folded in before resolving href, on top of the ancestor-derived
        // in-scope base.
        var effectiveBase = AdjustBase(baseUri, include);

        // Resolve href against the in-scope base (effectiveBase reflects both ancestor
        // xml:base and any xml:base on the xi:include element itself).
        Uri target;
        try
        {
            target = new Uri(effectiveBase, href);
        }
        catch (UriFormatException ex)
        {
            throw new XIncludeException(isFatal: true, $"xi:include href '{href}' is not a valid URI.", ex);
        }

        // Cyclic guard: a target already being expanded is a circular inclusion.
        foreach (var active in activeStack)
        {
            if (active == target)
            {
                throw new XIncludeException(XIncludeErrorKind.Cyclic, isFatal: true, "cyclic inclusion");
            }
        }

        // Depth guard: pushing this target would exceed the configured maximum.
        if (activeStack.Count >= options.MaxIncludeDepth)
        {
            throw new XIncludeException(
                XIncludeErrorKind.MaxDepthExceeded,
                isFatal: true,
                $"xi:include nesting exceeds MaxIncludeDepth ({options.MaxIncludeDepth}).");
        }

        // xi:fallback (SP2): at most one is allowed as a direct child of xi:include. Resolved
        // up front, before the fetch, so RecoverWithFallback has it ready if the fetch fails.
        var fallbacks = new List<XmlElement>();
        foreach (XmlNode c in include.ChildNodes)
        {
            if (IsXIncludeFallback(c))
            {
                fallbacks.Add((XmlElement)c);
            }
        }

        if (fallbacks.Count > 1)
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedFallback,
                isFatal: true,
                "xi:include has more than one xi:fallback.");
        }

        var fallback = fallbacks.Count == 1 ? fallbacks[0] : null;

        // Fetch + parse the target into a fragment document. A resource error (resolver
        // throws, or the content is not well-formed XML) is fallback-eligible (SP2): if the
        // xi:include has an xi:fallback child, its content replaces the include; otherwise the
        // error is fatal, exactly as in SP1. A resolver failure that is itself fatal (e.g. a
        // blocked remote/UNC fetch under AllowRemote=false) is never fallback-eligible and
        // rethrows unchanged.
        var fragment = new XmlDocument { PreserveWhitespace = true };
        try
        {
            using var reader = resolver.ResolveXml(target);
            fragment.Load(reader);
        }
        catch (XIncludeException xie) when (xie.IsFatal)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecoverWithFallback(masterDoc, include, fallback, baseUri, options, resolver, activeStack, ex, target);
            return;
        }

        // Recurse into the included fragment with the target as its base, tracking it on the
        // active stack for cycle/depth detection during the nested expansion.
        activeStack.Add(target);
        try
        {
            ExpandNode(fragment, fragment, target, options, resolver, activeStack);
        }
        finally
        {
            activeStack.RemoveAt(activeStack.Count - 1);
        }

        // Splice: for parse="xml" the included item is the fragment's document element.
        // Import it into the master document and replace the xi:include in place.
        //
        // NOTE (SP1 limitation, XInclude §3.2): per spec the replacement is technically the
        // target document node's *children* (which may include top-level comments/PIs that sit
        // outside the root element), not just the document element. This SP1 build only splices
        // fragment.DocumentElement, so sibling comments/PIs outside the root are dropped. Revisit
        // if a fixture ever needs top-level comment/PI preservation.
        var toInsert = fragment.DocumentElement
            ?? throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                $"xi:include target '{target}' has no document element.");

        var imported = masterDoc.ImportNode(toInsert, deep: true);

        // XInclude 1.0 §4.5 fixup: stamp xml:base/xml:lang on the top-level included element
        // only (descendants keep resolving through this + their own existing xml:base/xml:lang
        // chain). The in-scope xml:lang is computed from the xi:include's own position in the
        // (still-attached, pre-splice) master tree, so it must be captured before ReplaceChild.
        if (imported is XmlElement importedElement)
        {
            if (!importedElement.HasAttribute("base", XmlNamespace))
            {
                importedElement.SetAttribute("base", XmlNamespace, target.AbsoluteUri);
            }

            if (!importedElement.HasAttribute("lang", XmlNamespace))
            {
                var inScopeLang = GetInScopeLang(include);
                if (!string.IsNullOrEmpty(inScopeLang))
                {
                    importedElement.SetAttribute("lang", XmlNamespace, inScopeLang);
                }
            }
        }

        include.ParentNode!.ReplaceChild(imported, include);
    }

    /// <summary>
    /// Handles a resource error (fetch/parse failure) on an <c>xi:include</c> target: recovers
    /// via <paramref name="fallback"/>'s content when present, or rethrows fatally when it is
    /// not.
    /// </summary>
    private static void RecoverWithFallback(
        XmlDocument masterDoc,
        XmlElement include,
        XmlElement? fallback,
        Uri baseUri,
        XIncludeOptions options,
        IXmlResourceResolver resolver,
        List<Uri> activeStack,
        Exception resourceError,
        Uri target)
    {
        if (fallback is null)
        {
            throw new XIncludeException(
                XIncludeErrorKind.ResourceError,
                isFatal: true,
                $"xi:include could not resolve/parse '{target}' and has no xi:fallback: {resourceError.Message}",
                resourceError);
        }

        // The fallback's CONTENT (its children) replaces the xi:include. That content is
        // itself XInclude-processed first, in place, with the SAME in-scope base and active
        // stack (the failed target was never entered, so it is not on the stack) — this lets
        // the existing ExpandNode walk handle any nested xi:include/xi:fallback within the
        // fallback subtree exactly as it would anywhere else. Only once that expansion is done
        // are the (now fully expanded) children moved out from under the include. An empty
        // fallback simply removes the include. `include` and `fallback` already live in
        // masterDoc, so children can be moved directly with InsertBefore — no ImportNode needed.
        ExpandNode(masterDoc, fallback, baseUri, options, resolver, activeStack);

        var parent = include.ParentNode!;
        var next = fallback.FirstChild;
        while (next is not null)
        {
            var node = next;
            next = node.NextSibling;
            parent.InsertBefore(node, include);
        }

        parent.RemoveChild(include);
    }

    /// <summary>
    /// Returns the in-scope <c>xml:lang</c> for <paramref name="node"/>: the nearest
    /// <c>xml:lang</c> declared on <paramref name="node"/> itself or an ancestor, or
    /// <c>null</c> if none is in scope.
    /// </summary>
    private static string? GetInScopeLang(XmlNode node)
    {
        var current = node;
        while (current is XmlElement element)
        {
            var lang = element.GetAttribute("lang", XmlNamespace);
            if (!string.IsNullOrEmpty(lang))
            {
                return lang;
            }

            current = element.ParentNode!;
        }

        return null;
    }

    /// <summary>
    /// Returns the in-scope base URI for <paramref name="element"/>: <paramref name="baseUri"/>
    /// adjusted by an <c>xml:base</c> attribute on the element itself, if present.
    /// </summary>
    private static Uri AdjustBase(Uri baseUri, XmlElement element)
    {
        var xmlBase = element.GetAttribute("base", XmlNamespace);
        if (string.IsNullOrEmpty(xmlBase))
        {
            return baseUri;
        }

        return Uri.TryCreate(baseUri, xmlBase, out var adjusted) ? adjusted : baseUri;
    }
}
