using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for the store-global tree-ordinal document order on <see cref="XdmNode"/>
/// (<c>(TreeOrdinal, Id)</c>). Covers issue #188 — cross-store document-order identity.
/// </summary>
public class DocumentOrderTests
{
    private static readonly DocumentId Doc = new(1);

    private static XdmText Node(ulong treeOrdinal, ulong id) => new()
    {
        Id = new NodeId(id),
        Document = Doc,
        TreeOrdinal = treeOrdinal,
        Value = "x"
    };

    [Fact]
    public void TreeOrdinal_DefaultsToZero()
    {
        var node = new XdmText { Id = new NodeId(1), Document = Doc, Value = "x" };

        node.TreeOrdinal.Should().Be(0UL);
    }

    [Fact]
    public void EqualTreeOrdinal_SortsById()
    {
        var a = Node(5, 30);
        var b = Node(5, 10);

        XdmNode.CompareDocumentOrder(a, b).Should().BePositive();
        XdmNode.CompareDocumentOrder(b, a).Should().BeNegative();
    }

    [Fact]
    public void DifferingTreeOrdinal_SortsByTreeOrdinal_RegardlessOfId()
    {
        // Higher tree ordinal must sort last even when its NodeId is smaller.
        var earlierTree = Node(1, 9999);
        var laterTree = Node(2, 1);

        XdmNode.CompareDocumentOrder(earlierTree, laterTree).Should().BeNegative();
        XdmNode.CompareDocumentOrder(laterTree, earlierTree).Should().BePositive();
    }

    [Fact]
    public void CompareDocumentOrder_IsTotalOrder_OnThreeNodeSample()
    {
        var x = Node(1, 5);
        var y = Node(1, 5); // same position
        var z = Node(2, 1);

        // Antisymmetry / reflexivity
        XdmNode.CompareDocumentOrder(x, y).Should().Be(0);
        XdmNode.CompareDocumentOrder(x, x).Should().Be(0);

        // Transitivity: x < z and consistency across the sample
        var nodes = new[] { Node(2, 1), Node(1, 5), Node(1, 2) };
        var sorted = nodes.OrderBy(n => n, Comparer<XdmNode>.Create(XdmNode.CompareDocumentOrder)).ToArray();

        sorted[0].DocumentOrderKey.Should().Be((1UL, new NodeId(2)));
        sorted[1].DocumentOrderKey.Should().Be((1UL, new NodeId(5)));
        sorted[2].DocumentOrderKey.Should().Be((2UL, new NodeId(1)));
    }

    [Fact]
    public void DocumentOrderKey_DistinguishesCrossStoreNodesSharingNodeId()
    {
        // Two distinct nodes from independent stores collide on NodeId but differ on TreeOrdinal.
        var fromStoreA = Node(1, 7);
        var fromStoreB = Node(2, 7);

        fromStoreA.DocumentOrderKey.Should().NotBe(fromStoreB.DocumentOrderKey);

        var set = new HashSet<(ulong, NodeId)>
        {
            fromStoreA.DocumentOrderKey,
            fromStoreB.DocumentOrderKey
        };
        set.Should().HaveCount(2);
    }
}
