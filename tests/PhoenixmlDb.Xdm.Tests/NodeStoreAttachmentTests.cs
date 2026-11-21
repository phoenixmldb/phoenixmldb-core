using AwesomeAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// <see cref="XdmNode.Store"/> and <see cref="XdmNode.AttachStore"/>: a node belongs to at most
/// one store, set once.
/// </summary>
public class NodeStoreAttachmentTests
{
    private sealed class Store : IXdmNodeStore
    {
        public XdmNode? GetNode(NodeId id) => null;
        public string? GetNamespaceUri(NamespaceId id) => null;
        public bool TryGetNamespaceId(string uri, out NamespaceId id) { id = NamespaceId.None; return false; }
    }

    private static XdmText NewNode() => new() { Id = new NodeId(1), Document = DocumentId.None, Value = "x" };

    [Fact]
    public void A_new_node_has_no_store() => NewNode().Store.Should().BeNull();

    [Fact]
    public void Attach_sets_the_store()
    {
        var node = NewNode();
        var store = new Store();
        node.AttachStore(store);
        node.Store.Should().BeSameAs(store);
    }

    [Fact]
    public void Attaching_to_the_same_store_again_does_nothing()
    {
        var node = NewNode();
        var store = new Store();
        node.AttachStore(store);
        node.AttachStore(store);
        node.Store.Should().BeSameAs(store);
    }

    [Fact]
    public void Attaching_to_a_second_store_throws_and_keeps_the_first()
    {
        var node = NewNode();
        var first = new Store();
        node.AttachStore(first);
        var act = () => node.AttachStore(new Store());
        act.Should().Throw<InvalidOperationException>();
        node.Store.Should().BeSameAs(first);
    }

    [Fact]
    public void Attaching_null_throws()
    {
        var act = () => NewNode().AttachStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Concurrent_attaches_to_different_stores_leave_exactly_one_winner()
    {
        for (var round = 0; round < 200; round++)
        {
            var node = NewNode();
            var stores = Enumerable.Range(0, 8).Select(_ => new Store()).ToArray();
            var succeeded = 0;
            var failed = 0;
            using var start = new Barrier(stores.Length);
            var threads = stores.Select(s => new Thread(() =>
            {
                start.SignalAndWait();
                try { node.AttachStore(s); Interlocked.Increment(ref succeeded); }
                catch (InvalidOperationException) { Interlocked.Increment(ref failed); }
            })).ToArray();
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();
            succeeded.Should().Be(1);
            failed.Should().Be(stores.Length - 1);
            stores.Should().Contain(s => ReferenceEquals(s, node.Store));
        }
    }
}
