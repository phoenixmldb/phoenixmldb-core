using System.Collections.Immutable;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using PhoenixmlDb.Xdm.Nodes;
using System.Linq;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for <see cref="XdmNode.StringValueResolver"/> (phoenixmldb-core#4).
///
/// A node reconstructed from storage cannot have its string value computed at construction —
/// its children resolve lazily, so there is no subtree to walk yet. Before this hook existed,
/// such a node reported <see cref="string.Empty"/>, which is also what a genuinely empty
/// element reports, so nothing downstream could tell them apart: paths that walk children
/// (<c>fn:string</c>, explicit casts) saw the text while paths reading the cached value
/// (implicit atomization) saw <c>""</c>, and the same query answered differently depending on
/// what wrapped it.
///
/// The backing field is internal and the storage layer is deliberately absent from
/// <c>InternalsVisibleTo</c>, so a supported hook is the only way for it to supply the value.
/// </summary>
public class StringValueResolverTests
{
    private static readonly NodeId ElemId = new(1);
    private static readonly NodeId DocId = new(2);

    private static XdmElement StorageBackedElement(XdmNode.XdmStringValueResolver? resolver) =>
        new()
        {
            Id = ElemId,
            Document = DocumentId.None,
            Namespace = NamespaceId.None,
            LocalName = "price",
            Attributes = XdmElement.EmptyAttributes,
            // Children are NodeIds only — the text is not reachable without the store,
            // which is exactly the condition the resolver exists for.
            Children = ImmutableArray.Create(new NodeId(99)),
            NamespaceDeclarations = ImmutableArray<NamespaceBinding>.Empty,
            StringValueResolver = resolver,
        };

    [Fact]
    public void Element_WithResolver_ReportsResolvedStringValue()
    {
        var element = StorageBackedElement(_ => "42");

        element.StringValue.Should().Be("42");
    }

    [Fact]
    public void Element_WithResolver_TypedValueAgreesWithStringValue()
    {
        var element = StorageBackedElement(_ => "42");

        // TypedValue is derived from StringValue, so the implicit-atomization paths that read
        // it — the ones that silently saw "" — must now see the same text fn:string() sees.
        element.TypedValue.Should().Be(XdmValue.UntypedAtomic("42"));
        element.TypedValue.ToString().Should().Be(element.StringValue);
    }

    [Fact]
    public void Element_Resolver_RunsAtMostOnce()
    {
        var calls = 0;
        var element = StorageBackedElement(_ => { calls++; return "42"; });

        _ = element.StringValue;
        _ = element.StringValue;
        _ = element.StringValue;

        calls.Should().Be(1, "the resolved value is cached; a subtree walk per read would be a "
            + "performance trap on the spanning path this exists to serve");
    }

    [Fact]
    public void Element_Resolver_ReceivesTheNodeItResolves()
    {
        XdmNode? seen = null;
        var element = StorageBackedElement(n => { seen = n; return ""; });

        _ = element.StringValue;

        seen.Should().BeSameAs(element, "the resolver is handed the node so one shared "
            + "implementation can serve every node a store hands out");
    }

    [Fact]
    public void Element_Resolver_MayResolveToEmptyString()
    {
        var element = StorageBackedElement(_ => "");

        element.StringValue.Should().BeEmpty("a genuinely empty element is a legitimate result, "
            + "and must not be retried as though it were 'not yet computed'");
    }

    [Fact]
    public void Element_WithoutResolver_StillReportsEmpty()
    {
        using var _ = new NonStrictStringValue();
        // Documents the CURRENT behaviour, which is the ambiguity itself: no computed value and
        // no resolver is indistinguishable from an empty element. Raising here instead is
        // tracked on core#4 and changes behaviour every consumer can observe, so it is a
        // deliberate decision rather than a side effect of adding the hook.
        var element = StorageBackedElement(null);

        element.StringValue.Should().BeEmpty();
    }

    [Fact]
    public void Document_WithResolver_ReportsResolvedStringValue()
    {
        var doc = new XdmDocument
        {
            Id = DocId,
            Document = DocumentId.None,
            Children = ImmutableArray.Create(new NodeId(98)),
            StringValueResolver = _ => "doc text",
        };

        doc.StringValue.Should().Be("doc text");
        doc.TypedValue.Should().Be(XdmValue.UntypedAtomic("doc text"));
    }

    // ---- the path that actually matters: nodes coming back through NodeReader ----
    //
    // Storage never constructs XdmElement/XdmDocument itself — every deserialization site goes
    // through NodeReader — so an init-only property on the node is unreachable from outside this
    // assembly. NodeReader therefore takes the resolver and stamps it on every node it builds.

    [Fact]
    public void NodeReader_StampsResolverOnDeserializedElement()
    {
        var source = new XdmElement
        {
            Id = ElemId,
            Document = DocumentId.None,
            Namespace = NamespaceId.None,
            LocalName = "price",
            Attributes = XdmElement.EmptyAttributes,
            Children = ImmutableArray<NodeId>.Empty,
            NamespaceDeclarations = ImmutableArray<NamespaceBinding>.Empty,
        };
        var buffer = new byte[1024];
        var written = PhoenixmlDb.Xdm.Serialization.NodeSerializer.Serialize(source, buffer);

        var reader = new PhoenixmlDb.Xdm.Serialization.NodeReader(
            buffer.AsSpan(0, written), _ => "42");
        var result = reader.Read(source.Id, source.Document) as XdmElement;

        result.Should().NotBeNull();
        result!.StringValue.Should().Be("42",
            "a deserialized node carries child NodeIds, not children, so only a caller holding "
            + "the store can compute its string value");
    }

    [Fact]
    public void NodeReader_WithoutResolver_DeserializedElementReportsEmpty()
    {
        using var _ = new NonStrictStringValue();
        var source = new XdmElement
        {
            Id = ElemId,
            Document = DocumentId.None,
            Namespace = NamespaceId.None,
            LocalName = "price",
            Attributes = XdmElement.EmptyAttributes,
            Children = ImmutableArray<NodeId>.Empty,
            NamespaceDeclarations = ImmutableArray<NamespaceBinding>.Empty,
        };
        var buffer = new byte[1024];
        var written = PhoenixmlDb.Xdm.Serialization.NodeSerializer.Serialize(source, buffer);

        // The single-argument constructor must keep behaving exactly as before, so every
        // existing caller is unaffected by the new overload.
        var reader = new PhoenixmlDb.Xdm.Serialization.NodeReader(buffer.AsSpan(0, written));
        var result = reader.Read(source.Id, source.Document) as XdmElement;

        result!.StringValue.Should().BeEmpty();
    }

    [Fact]
    public void NodeReader_StampsResolverOnDeserializedDocument()
    {
        var source = new XdmDocument
        {
            Id = DocId,
            Document = DocumentId.None,
            Children = ImmutableArray<NodeId>.Empty,
        };
        var buffer = new byte[1024];
        var written = PhoenixmlDb.Xdm.Serialization.NodeSerializer.Serialize(source, buffer);

        var reader = new PhoenixmlDb.Xdm.Serialization.NodeReader(
            buffer.AsSpan(0, written), _ => "doc text");
        var result = reader.Read(source.Id, source.Document) as XdmDocument;

        result!.StringValue.Should().Be("doc text");
    }

    [Fact]
    public void NodeReader_OneResolverServesEveryNodeItBuilds()
    {
        // The resolver is supplied per READER, not per node, so a caller closes over its read
        // transaction once. Each node still resolves independently and caches its own value.
        var calls = 0;
        PhoenixmlDb.Xdm.Nodes.XdmNode.XdmStringValueResolver shared = n =>
        {
            calls++;
            return ((XdmElement)n).LocalName + "-value";
        };

        var results = new List<XdmElement>();
        foreach (var name in new[] { "a", "b" })
        {
            var source = new XdmElement
            {
                Id = ElemId,
                Document = DocumentId.None,
                Namespace = NamespaceId.None,
                LocalName = name,
                Attributes = XdmElement.EmptyAttributes,
                Children = ImmutableArray<NodeId>.Empty,
                NamespaceDeclarations = ImmutableArray<NamespaceBinding>.Empty,
            };
            var buffer = new byte[1024];
            var written = PhoenixmlDb.Xdm.Serialization.NodeSerializer.Serialize(source, buffer);
            var reader = new PhoenixmlDb.Xdm.Serialization.NodeReader(
                buffer.AsSpan(0, written), shared);
            results.Add((XdmElement)reader.Read(source.Id, source.Document)!);
        }

        results[0].StringValue.Should().Be("a-value");
        results[1].StringValue.Should().Be("b-value");
        calls.Should().Be(2, "once per node, not once per read");
    }

    // ---- StrictStringValue: opt-in loudness ----
    //
    // Default off, so no consumer of the package changes behaviour on upgrade. On, so that the
    // suites which run it turn this class of defect from a silent wrong number into a failing
    // test. The switch is global mutable state, so these tests serialize and always restore.

    [Fact]
    public void Strict_TogglesAndRestores()
    {
        // The ambient value cannot be asserted — a suite may enable strict mode globally, which
        // is exactly what this flag is for. What must hold is that the property is honoured in
        // both directions and restores cleanly, so tests can pin whichever mode they document.
        var saved = XdmNode.StrictStringValue;
        try
        {
            XdmNode.StrictStringValue = false;
            XdmNode.StrictStringValue.Should().BeFalse();
            XdmNode.StrictStringValue = true;
            XdmNode.StrictStringValue.Should().BeTrue();
        }
        finally { XdmNode.StrictStringValue = saved; }

        XdmNode.StrictStringValue.Should().Be(saved);
    }

    [Fact]
    public void Strict_On_UnresolvedElement_Throws()
    {
        var element = StorageBackedElement(null);
        var saved = XdmNode.StrictStringValue;
        try
        {
            XdmNode.StrictStringValue = true;

            var act = () => element.StringValue;

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*never computed*")
                .WithMessage("*StringValueResolver*",
                    "the message must name the thing the caller has to supply");
        }
        finally { XdmNode.StrictStringValue = saved; }
    }

    [Fact]
    public void Strict_On_ResolvedElement_DoesNotThrow()
    {
        var element = StorageBackedElement(_ => "42");
        var saved = XdmNode.StrictStringValue;
        try
        {
            XdmNode.StrictStringValue = true;

            element.StringValue.Should().Be("42",
                "strict mode targets nodes with no way to know their value, not nodes that have one");
        }
        finally { XdmNode.StrictStringValue = saved; }
    }

    [Fact]
    public void Strict_On_ResolverReturningEmpty_DoesNotThrow()
    {
        var element = StorageBackedElement(_ => "");
        var saved = XdmNode.StrictStringValue;
        try
        {
            XdmNode.StrictStringValue = true;

            element.StringValue.Should().BeEmpty(
                "a resolver that answers 'empty' HAS determined the value; that is the very "
                + "distinction strict mode exists to make");
        }
        finally { XdmNode.StrictStringValue = saved; }
    }

    [Fact]
    public void Strict_On_ParsedElement_DoesNotThrow()
    {
        // A node built by the parser has its value computed up front and carries no resolver.
        // Strict mode must not fire on it, or it would break every ordinary in-memory document.
        var parsed = new PhoenixmlDb.Xdm.Parsing.XmlDocumentParser(
            DocumentId.None, new NodeId(1), _ => NamespaceId.None).Parse("<a><b>text</b></a>");
        var saved = XdmNode.StrictStringValue;
        try
        {
            XdmNode.StrictStringValue = true;

            var act = () => parsed.Nodes.OfType<XdmElement>().Select(e => e.StringValue).ToList();

            act.Should().NotThrow();
            parsed.Nodes.OfType<XdmElement>().Should().Contain(e => e.StringValue == "text");
        }
        finally { XdmNode.StrictStringValue = saved; }
    }
}
