using System;
using System.Collections.Generic;
using System.Xml;

namespace PhoenixmlDb.Core.Xml;

/// <summary>
/// XInclude 1.0 processor: expands <c>xi:include</c> elements with <c>parse="xml"</c>
/// (structural inclusion) or <c>parse="text"</c> (textual inclusion, SP2) and an <c>href</c>,
/// resolving each reference against the in-scope base URI, recursing into included content,
/// and detecting cyclic / over-deep inclusion.
/// </summary>
/// <remarks>
/// <para>
/// <c>xpointer</c> / <c>fragid</c> sub-resource selection (SP3) is supported for both an external
/// target (<c>href</c> present, evaluated against the fetched fragment) and a same-document
/// reference (<c>xpointer</c> with no <c>href</c>, evaluated against the master document and
/// guarded against cyclic self-inclusion by containment and bounded by
/// <see cref="XIncludeOptions.MaxIncludeDepth"/>). The pointer is evaluated via
/// <see cref="XPointerEvaluator"/> and the selected node-set is spliced in place of the
/// <c>xi:include</c>, with per-element §4.5 <c>xml:base</c>/<c>xml:lang</c> fixup. An empty
/// selection is a fallback-eligible resource error; a selection containing a node that cannot be
/// element content (attribute, document, etc.) is a fatal <see cref="XIncludeException"/>.
/// </para>
/// <para>
/// <c>parse="text"</c> (SP2) reads the target via
/// <see cref="IXmlResourceResolver.ResolveText"/> — honoring the include's
/// <c>encoding</c>/<c>accept</c>/<c>accept-language</c> attributes — and splices the decoded
/// content in as a single text node in place of the <c>xi:include</c>. A resource error (fetch
/// or decode failure) is fallback-eligible, exactly as for <c>parse="xml"</c>; a fragment
/// identifier in <c>href</c> is still a fatal error (text resources have no fragments).
/// </para>
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
    /// Thrown on a malformed <c>xi:include</c> (including a malformed xpointer), a cyclic or
    /// over-deep inclusion, or a resource error with no usable <c>xi:fallback</c>. A resource
    /// error that is recovered by an <c>xi:fallback</c> does not throw.
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
        var limiter = new XIncludeLimiter(options);

        ExpandNode(doc, doc, baseUri, options, resolver, activeStack, limiter);
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
        List<Uri> activeStack,
        XIncludeLimiter limiter)
    {
        limiter.EnterExpansion();
        try
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
                        ProcessInclude(masterDoc, element, baseUri, options, resolver, activeStack, limiter);
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
                        ExpandNode(masterDoc, element, childBase, options, resolver, activeStack, limiter);
                    }
                }

                child = next;
            }
        }
        finally
        {
            limiter.ExitExpansion();
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
        List<Uri> activeStack,
        XIncludeLimiter limiter)
    {
        var href = include.HasAttribute("href") ? include.GetAttribute("href") : null;
        var parse = include.HasAttribute("parse") ? include.GetAttribute("parse") : "xml";
        var xpointer = include.HasAttribute("fragid") ? include.GetAttribute("fragid")
            : include.HasAttribute("xpointer") ? include.GetAttribute("xpointer")
            : null;

        if (string.Equals(parse, "text", StringComparison.Ordinal))
        {
            ProcessTextInclude(masterDoc, include, href, baseUri, options, resolver, activeStack, limiter);
            return;
        }

        if (!string.Equals(parse, "xml", StringComparison.Ordinal))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                $"xi:include has invalid parse='{parse}'.");
        }

        // Same-document inclusion: an xpointer with no href selects from the master document
        // itself.
        if (xpointer is not null && string.IsNullOrEmpty(href))
        {
            ProcessSameDocumentXPointer(masterDoc, include, xpointer, baseUri, options, resolver, activeStack, limiter);
            return;
        }

        // href is required here: an include with neither href nor xpointer is malformed.
        if (string.IsNullOrEmpty(href))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                "xi:include is missing required 'href'.");
        }

        // XInclude 1.0 §4.2: a fragment identifier in href is a fatal error (fragments select
        // into the parsed result, not the resource itself). Checked against the raw href string, per RFC 3986,
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
            RecoverWithFallback(masterDoc, include, fallback, baseUri, options, resolver, activeStack, ex, target, limiter);
            return;
        }

        // Recurse into the included fragment with the target as its base, tracking it on the
        // active stack for cycle/depth detection during the nested expansion.
        activeStack.Add(target);
        try
        {
            ExpandNode(fragment, fragment, target, options, resolver, activeStack, limiter);
        }
        finally
        {
            activeStack.RemoveAt(activeStack.Count - 1);
        }

        // Select what to include from the fetched fragment: the whole document element (no
        // xpointer) or the XPointer-selected node-set.
        //
        // NOTE (SP1 limitation, XInclude §3.2): per spec the whole-document replacement is
        // technically the target document node's *children* (which may include top-level
        // comments/PIs that sit outside the root element), not just the document element. This
        // build only splices fragment.DocumentElement in the no-xpointer case, so sibling
        // comments/PIs outside the root are dropped. Revisit if a fixture ever needs top-level
        // comment/PI preservation.
        IReadOnlyList<XmlNode> selected;
        if (xpointer is null)
        {
            var docEl = fragment.DocumentElement
                ?? throw new XIncludeException(
                    XIncludeErrorKind.MalformedInclude,
                    isFatal: true,
                    $"xi:include target '{target}' has no document element.");
            selected = new List<XmlNode> { docEl };
        }
        else
        {
            selected = XPointerEvaluator.Evaluate(fragment, xpointer);
            if (selected.Count == 0)
            {
                // XPointer selected nothing → resource error (fallback-eligible).
                RecoverWithFallback(masterDoc, include, fallback, baseUri, options, resolver, activeStack,
                    new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                        $"xpointer '{xpointer}' selected no nodes in '{target}'."),
                    target, limiter);
                return;
            }
        }

        SpliceNodeSet(masterDoc, include, selected, target);
    }

    /// <summary>
    /// Handles an <c>xi:include</c> with an <c>xpointer</c>/<c>fragid</c> and no <c>href</c>:
    /// the selection is evaluated against <paramref name="masterDoc"/> itself (same-document
    /// XPointer). A selection that is, or contains, the <c>xi:include</c> element is a fatal
    /// <see cref="XIncludeErrorKind.Cyclic"/> (containment-based cyclic guard — the URI-stack
    /// guard used for fetched targets doesn't apply here since there is no target URI). Any
    /// nested <c>xi:include</c> within the selected subtree is expanded (against the master's
    /// own base URI, tracked on the same active stack; <see cref="XIncludeOptions.MaxIncludeDepth"/>
    /// remains the runaway backstop) before the copies are spliced in.
    /// </summary>
    private static void ProcessSameDocumentXPointer(
        XmlDocument masterDoc, XmlElement include, string xpointer, Uri baseUri,
        XIncludeOptions options, IXmlResourceResolver resolver, List<Uri> activeStack,
        XIncludeLimiter limiter)
    {
        // Depth guard: the containment check below catches a *direct* self-inclusion, but a
        // same-document selection whose copy contains another same-document xi:include (self- or
        // mutually-recursive) would otherwise recurse forever — the copy is detached, so the
        // containment check can never match the original selection. Bound it by the shared active
        // stack: each level of same-document expansion pushes a sentinel below (so activeStack
        // grows with depth), and exceeding MaxIncludeDepth here is fatal.
        if (activeStack.Count >= options.MaxIncludeDepth)
        {
            throw new XIncludeException(
                XIncludeErrorKind.MaxDepthExceeded,
                isFatal: true,
                $"same-document xi:include nesting exceeds MaxIncludeDepth ({options.MaxIncludeDepth}).");
        }

        // Resolve any single xi:fallback up front (same rule as the fetched-target path).
        XmlElement? fallback = null;
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

        if (fallbacks.Count == 1)
        {
            fallback = fallbacks[0];
        }

        var selected = XPointerEvaluator.Evaluate(masterDoc, xpointer);
        if (selected.Count == 0)
        {
            RecoverWithFallback(masterDoc, include, fallback, baseUri, options, resolver, activeStack,
                new XIncludeException(XIncludeErrorKind.ResourceError, isFatal: false,
                    $"same-document xpointer '{xpointer}' selected no nodes."),
                baseUri, limiter);
            return;
        }

        // Cyclic guard by containment: including a node that is, or contains, the xi:include
        // would re-include the include itself.
        foreach (var node in selected)
        {
            if (node == include || IsAncestorOrSelf(node, include))
            {
                throw new XIncludeException(
                    XIncludeErrorKind.Cyclic,
                    isFatal: true,
                    $"same-document xpointer '{xpointer}' selects a node containing the xi:include (cyclic).");
            }
        }

        // Import copies, expand any nested xi:include within them (master base URI), then splice.
        // The imported copies live in masterDoc; expand them in place before they are inserted.
        // Push a sentinel (the master base URI) onto the active stack so nested same-document
        // expansion increments the shared depth counter the guard above reads — this is what makes
        // MaxIncludeDepth an actual backstop against self-/mutually-recursive same-document
        // inclusion. Popped in the finally so the stack is left exactly as found.
        var expanded = new List<XmlNode>(selected.Count);
        activeStack.Add(baseUri);
        try
        {
            foreach (var node in selected)
            {
                var imported = masterDoc.ImportNode(node, deep: true);
                if (imported is XmlElement)
                {
                    ExpandNode(masterDoc, imported, baseUri, options, resolver, activeStack, limiter);
                }

                expanded.Add(imported);
            }
        }
        finally
        {
            activeStack.RemoveAt(activeStack.Count - 1);
        }

        // Splice the (already-imported, already-expanded) copies; SpliceNodeSet skips
        // re-importing a node already owned by masterDoc, so the expanded state survives. Base
        // URI for same-document content is the master's own base.
        SpliceNodeSet(masterDoc, include, expanded, baseUri);
    }

    /// <summary>
    /// Returns whether <paramref name="candidateAncestor"/> is <paramref name="node"/> itself or
    /// one of its ancestors (walked via <see cref="XmlNode.ParentNode"/>).
    /// </summary>
    private static bool IsAncestorOrSelf(XmlNode candidateAncestor, XmlNode node)
    {
        for (var n = node; n is not null; n = n.ParentNode)
        {
            if (ReferenceEquals(n, candidateAncestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Imports each node in <paramref name="selected"/> into <paramref name="masterDoc"/> and
    /// inserts them, in document order, in place of <paramref name="include"/>. Applies XInclude
    /// §4.5 <c>xml:base</c>/<c>xml:lang</c> fixup to each top-level included <em>element</em>
    /// (<paramref name="target"/> = the origin URI). An attribute (or namespace) node cannot be
    /// spliced as content and is a fatal <see cref="XIncludeErrorKind.MalformedInclude"/>.
    /// </summary>
    private static void SpliceNodeSet(
        XmlDocument masterDoc, XmlElement include, IReadOnlyList<XmlNode> selected, Uri target)
    {
        // Reject any selected node that cannot be spliced as element content, before touching the
        // tree (so a bad node never causes a partial splice). System.Xml surfaces an xpath1(//@a)
        // selection as an XmlAttribute, and xpath1(/) as the Document node — neither can be a child
        // of an element (Document/DocumentType/etc. also make ImportNode throw), so all such are a
        // fatal malformed-include. Includable content is element/text/CDATA/comment/PI/entity-ref.
        foreach (var node in selected)
        {
            if (node.NodeType is XmlNodeType.Attribute
                or XmlNodeType.Document
                or XmlNodeType.DocumentType
                or XmlNodeType.DocumentFragment
                or XmlNodeType.XmlDeclaration
                or XmlNodeType.Notation
                or XmlNodeType.Entity)
            {
                throw new XIncludeException(
                    XIncludeErrorKind.MalformedInclude,
                    isFatal: true,
                    $"xpointer selection includes a node ({node.NodeType}) that cannot be included as content.");
            }
        }

        var parent = include.ParentNode!;
        var inScopeLang = GetInScopeLang(include);
        foreach (var node in selected)
        {
            // A same-document XPointer selection is already imported (and, for elements, already
            // expanded) into masterDoc by ProcessSameDocumentXPointer; re-importing it here would
            // needlessly clone it and lose that expanded state.
            var imported = ReferenceEquals(node.OwnerDocument, masterDoc)
                ? node
                : masterDoc.ImportNode(node, deep: true);

            // XInclude 1.0 §4.5 fixup: stamp xml:base/xml:lang on each top-level included
            // element only (descendants keep resolving through this + their own existing
            // xml:base/xml:lang chain). The in-scope xml:lang is computed from the xi:include's
            // own position in the (still-attached, pre-splice) master tree.
            if (imported is XmlElement el)
            {
                if (!el.HasAttribute("base", XmlNamespace))
                {
                    SetXmlAttribute(el, "base", target.AbsoluteUri);
                }

                if (!el.HasAttribute("lang", XmlNamespace) && !string.IsNullOrEmpty(inScopeLang))
                {
                    SetXmlAttribute(el, "lang", inScopeLang);
                }
            }

            parent.InsertBefore(imported, include);
        }

        parent.RemoveChild(include);
    }

    /// <summary>
    /// Handles an <c>xi:include parse="text"</c>: resolves <paramref name="href"/> to a text
    /// resource via <see cref="IXmlResourceResolver.ResolveText"/> (honoring the
    /// <c>encoding</c>/<c>accept</c>/<c>accept-language</c> attributes) and splices its content
    /// in as a single text node, replacing the <c>xi:include</c>. A resource error (fetch/decode
    /// failure) is fallback-eligible, exactly as for <c>parse="xml"</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The resolver's fetch of a parse=text target can fail with any " +
            "resolver-specific exception (I/O, network, ...); every such failure is a resource " +
            "error that must be routed through RecoverWithFallback, so catching Exception " +
            "broadly here is intentional, not a swallowed error.")]
    private static void ProcessTextInclude(
        XmlDocument masterDoc,
        XmlElement include,
        string? href,
        Uri baseUri,
        XIncludeOptions options,
        IXmlResourceResolver resolver,
        List<Uri> activeStack,
        XIncludeLimiter limiter)
    {
        // href required; fragment-in-href is still fatal (a text resource has no fragments).
        if (string.IsNullOrEmpty(href))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                "xi:include is missing required 'href'.");
        }

        if (href.Contains('#', StringComparison.Ordinal))
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                "fragment identifier in href is not allowed (XInclude 1.0 §4.2)");
        }

        var textFallbacks = new List<XmlElement>();
        foreach (XmlNode c in include.ChildNodes)
        {
            if (IsXIncludeFallback(c))
            {
                textFallbacks.Add((XmlElement)c);
            }
        }

        if (textFallbacks.Count > 1)
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedFallback,
                isFatal: true,
                "xi:include has more than one xi:fallback.");
        }

        var textFallback = textFallbacks.Count == 1 ? textFallbacks[0] : null;

        var effTextBase = AdjustBase(baseUri, include);
        Uri textTarget;
        try
        {
            textTarget = new Uri(effTextBase, href);
        }
        catch (UriFormatException ex)
        {
            throw new XIncludeException(
                XIncludeErrorKind.MalformedInclude,
                isFatal: true,
                $"xi:include href '{href}' is not a valid URI.", ex);
        }

        string text;
        try
        {
            text = resolver.ResolveText(
                textTarget,
                include.HasAttribute("encoding") ? include.GetAttribute("encoding") : null,
                include.HasAttribute("accept") ? include.GetAttribute("accept") : null,
                include.HasAttribute("accept-language") ? include.GetAttribute("accept-language") : null);
        }
        catch (XIncludeException xie) when (xie.IsFatal)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecoverWithFallback(masterDoc, include, textFallback, baseUri, options, resolver, activeStack, ex, textTarget, limiter);
            return;
        }

        var textNode = masterDoc.CreateTextNode(text);
        include.ParentNode!.ReplaceChild(textNode, include);
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
        Uri target,
        XIncludeLimiter limiter)
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
        // itself XInclude-processed first, in place, with the active stack unchanged (the failed
        // target was never entered, so it is not on the stack) — this lets the existing
        // ExpandNode walk handle any nested xi:include/xi:fallback within the fallback subtree
        // exactly as it would anywhere else. Only once that expansion is done are the (now fully
        // expanded) children moved out from under the include. An empty fallback simply removes
        // the include. `include` and `fallback` already live in masterDoc, so children can be
        // moved directly with InsertBefore — no ImportNode needed.
        //
        // Base URI for the fallback subtree (XML Base): fold any xml:base on the xi:include and
        // on the xi:fallback itself onto the in-scope base, so a relative href on a nested
        // xi:include inside the fallback resolves against the fallback's own base, not the
        // include's parent base.
        var fallbackBase = AdjustBase(AdjustBase(baseUri, include), fallback);
        ExpandNode(masterDoc, fallback, fallbackBase, options, resolver, activeStack, limiter);

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
    /// Stamps an attribute in the XML namespace (<c>xml:base</c>/<c>xml:lang</c>) on
    /// <paramref name="element"/> using the reserved <c>xml</c> prefix. Using
    /// <see cref="XmlElement.SetAttribute(string, string, string)"/> with the XML namespace but
    /// no prefix leaves the attribute prefix empty, so a later serialization (e.g. via
    /// <c>OuterXml</c>) invents a bogus prefix and emits an illegal
    /// <c>xmlns:…="http://www.w3.org/XML/1998/namespace"</c> redeclaration; binding the reserved
    /// <c>xml</c> prefix here keeps the stamped attribute well-formed on round-trip.
    /// </summary>
    private static void SetXmlAttribute(XmlElement element, string localName, string value)
    {
        var attr = element.OwnerDocument.CreateAttribute("xml", localName, XmlNamespace);
        attr.Value = value;
        element.SetAttributeNode(attr);
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
