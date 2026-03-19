using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// Tests for ContainerId identifier type.
/// </summary>
public class ContainerIdTests
{
    [Fact]
    public void Constructor_SetsValueCorrectly()
    {
        var id = new ContainerId(42);
        id.Value.Should().Be(42);
    }

    [Fact]
    public void None_ReturnsZeroValue()
    {
        ContainerId.None.Value.Should().Be(0);
    }

    [Fact]
    public void Default_EqualsNone()
    {
        var defaultId = default(ContainerId);
        defaultId.Should().Be(ContainerId.None);
        defaultId.Value.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "C:0")]
    [InlineData(1, "C:1")]
    [InlineData(100, "C:100")]
    [InlineData(uint.MaxValue, "C:4294967295")]
    public void ToString_FormatsCorrectly(uint value, string expected)
    {
        var id = new ContainerId(value);
        id.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id1 = new ContainerId(42);
        var id2 = new ContainerId(42);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var id1 = new ContainerId(42);
        var id2 = new ContainerId(43);

        id1.Should().NotBe(id2);
        (id1 == id2).Should().BeFalse();
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_SameHashCode()
    {
        var id1 = new ContainerId(42);
        var id2 = new ContainerId(42);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_DifferentHashCodes()
    {
        var id1 = new ContainerId(42);
        var id2 = new ContainerId(43);

        // Not guaranteed by contract, but almost always true
        id1.GetHashCode().Should().NotBe(id2.GetHashCode());
    }

    [Theory]
    [InlineData(1, 2, -1)]
    [InlineData(2, 1, 1)]
    [InlineData(5, 5, 0)]
    public void CompareTo_ReturnsCorrectResult(uint left, uint right, int expectedSign)
    {
        var id1 = new ContainerId(left);
        var id2 = new ContainerId(right);

        var result = id1.CompareTo(id2);
        Math.Sign(result).Should().Be(expectedSign);
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, false)]
    [InlineData(5, 5, false)]
    public void LessThan_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new ContainerId(left);
        var id2 = new ContainerId(right);

        (id1 < id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, false)]
    [InlineData(5, 5, true)]
    public void LessThanOrEqual_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new ContainerId(left);
        var id2 = new ContainerId(right);

        (id1 <= id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(5, 5, false)]
    public void GreaterThan_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new ContainerId(left);
        var id2 = new ContainerId(right);

        (id1 > id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(5, 5, true)]
    public void GreaterThanOrEqual_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new ContainerId(left);
        var id2 = new ContainerId(right);

        (id1 >= id2).Should().Be(expected);
    }

    [Fact]
    public void Operators_WithBoundaryValues()
    {
        var min = new ContainerId(0);
        var minCopy = new ContainerId(0);
        var max = new ContainerId(uint.MaxValue);
        var maxCopy = new ContainerId(uint.MaxValue);

        (min < max).Should().BeTrue();
        (max > min).Should().BeTrue();
        (min <= minCopy).Should().BeTrue();
        (max >= maxCopy).Should().BeTrue();
    }
}

/// <summary>
/// Tests for DocumentId identifier type.
/// </summary>
public class DocumentIdTests
{
    [Fact]
    public void Constructor_SetsValueCorrectly()
    {
        var id = new DocumentId(42UL);
        id.Value.Should().Be(42UL);
    }

    [Fact]
    public void None_ReturnsZeroValue()
    {
        DocumentId.None.Value.Should().Be(0UL);
    }

    [Fact]
    public void Default_EqualsNone()
    {
        var defaultId = default(DocumentId);
        defaultId.Should().Be(DocumentId.None);
        defaultId.Value.Should().Be(0UL);
    }

    [Theory]
    [InlineData(0UL, "D:0")]
    [InlineData(1UL, "D:1")]
    [InlineData(100UL, "D:100")]
    [InlineData(ulong.MaxValue, "D:18446744073709551615")]
    public void ToString_FormatsCorrectly(ulong value, string expected)
    {
        var id = new DocumentId(value);
        id.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id1 = new DocumentId(42UL);
        var id2 = new DocumentId(42UL);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var id1 = new DocumentId(42UL);
        var id2 = new DocumentId(43UL);

        id1.Should().NotBe(id2);
        (id1 == id2).Should().BeFalse();
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_SameHashCode()
    {
        var id1 = new DocumentId(42UL);
        var id2 = new DocumentId(42UL);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Theory]
    [InlineData(1UL, 2UL, -1)]
    [InlineData(2UL, 1UL, 1)]
    [InlineData(5UL, 5UL, 0)]
    public void CompareTo_ReturnsCorrectResult(ulong left, ulong right, int expectedSign)
    {
        var id1 = new DocumentId(left);
        var id2 = new DocumentId(right);

        var result = id1.CompareTo(id2);
        Math.Sign(result).Should().Be(expectedSign);
    }

    [Theory]
    [InlineData(1UL, 2UL, true)]
    [InlineData(2UL, 1UL, false)]
    [InlineData(5UL, 5UL, false)]
    public void LessThan_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new DocumentId(left);
        var id2 = new DocumentId(right);

        (id1 < id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, true)]
    [InlineData(2UL, 1UL, false)]
    [InlineData(5UL, 5UL, true)]
    public void LessThanOrEqual_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new DocumentId(left);
        var id2 = new DocumentId(right);

        (id1 <= id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, false)]
    [InlineData(2UL, 1UL, true)]
    [InlineData(5UL, 5UL, false)]
    public void GreaterThan_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new DocumentId(left);
        var id2 = new DocumentId(right);

        (id1 > id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, false)]
    [InlineData(2UL, 1UL, true)]
    [InlineData(5UL, 5UL, true)]
    public void GreaterThanOrEqual_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new DocumentId(left);
        var id2 = new DocumentId(right);

        (id1 >= id2).Should().Be(expected);
    }

    [Fact]
    public void Operators_WithBoundaryValues()
    {
        var min = new DocumentId(0UL);
        var minCopy = new DocumentId(0UL);
        var max = new DocumentId(ulong.MaxValue);
        var maxCopy = new DocumentId(ulong.MaxValue);

        (min < max).Should().BeTrue();
        (max > min).Should().BeTrue();
        (min <= minCopy).Should().BeTrue();
        (max >= maxCopy).Should().BeTrue();
    }

    [Fact]
    public void LargeValues_HandledCorrectly()
    {
        var largeValue = ulong.MaxValue - 1;
        var id = new DocumentId(largeValue);

        id.Value.Should().Be(largeValue);
        id.ToString().Should().Be($"D:{largeValue}");
    }
}

/// <summary>
/// Tests for NodeId identifier type.
/// </summary>
public class NodeIdTests
{
    [Fact]
    public void Constructor_SetsValueCorrectly()
    {
        var id = new NodeId(42UL);
        id.Value.Should().Be(42UL);
    }

    [Fact]
    public void None_ReturnsZeroValue()
    {
        NodeId.None.Value.Should().Be(0UL);
    }

    [Fact]
    public void Default_EqualsNone()
    {
        var defaultId = default(NodeId);
        defaultId.Should().Be(NodeId.None);
        defaultId.Value.Should().Be(0UL);
    }

    [Theory]
    [InlineData(0UL, "N:0")]
    [InlineData(1UL, "N:1")]
    [InlineData(100UL, "N:100")]
    [InlineData(ulong.MaxValue, "N:18446744073709551615")]
    public void ToString_FormatsCorrectly(ulong value, string expected)
    {
        var id = new NodeId(value);
        id.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id1 = new NodeId(42UL);
        var id2 = new NodeId(42UL);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var id1 = new NodeId(42UL);
        var id2 = new NodeId(43UL);

        id1.Should().NotBe(id2);
        (id1 == id2).Should().BeFalse();
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_SameHashCode()
    {
        var id1 = new NodeId(42UL);
        var id2 = new NodeId(42UL);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Theory]
    [InlineData(1UL, 2UL, -1)]
    [InlineData(2UL, 1UL, 1)]
    [InlineData(5UL, 5UL, 0)]
    public void CompareTo_ReturnsCorrectResult(ulong left, ulong right, int expectedSign)
    {
        var id1 = new NodeId(left);
        var id2 = new NodeId(right);

        var result = id1.CompareTo(id2);
        Math.Sign(result).Should().Be(expectedSign);
    }

    [Theory]
    [InlineData(1UL, 2UL, true)]
    [InlineData(2UL, 1UL, false)]
    [InlineData(5UL, 5UL, false)]
    public void LessThan_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new NodeId(left);
        var id2 = new NodeId(right);

        (id1 < id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, true)]
    [InlineData(2UL, 1UL, false)]
    [InlineData(5UL, 5UL, true)]
    public void LessThanOrEqual_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new NodeId(left);
        var id2 = new NodeId(right);

        (id1 <= id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, false)]
    [InlineData(2UL, 1UL, true)]
    [InlineData(5UL, 5UL, false)]
    public void GreaterThan_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new NodeId(left);
        var id2 = new NodeId(right);

        (id1 > id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 2UL, false)]
    [InlineData(2UL, 1UL, true)]
    [InlineData(5UL, 5UL, true)]
    public void GreaterThanOrEqual_ReturnsCorrectResult(ulong left, ulong right, bool expected)
    {
        var id1 = new NodeId(left);
        var id2 = new NodeId(right);

        (id1 >= id2).Should().Be(expected);
    }

    [Fact]
    public void Operators_WithBoundaryValues()
    {
        var min = new NodeId(0UL);
        var minCopy = new NodeId(0UL);
        var max = new NodeId(ulong.MaxValue);
        var maxCopy = new NodeId(ulong.MaxValue);

        (min < max).Should().BeTrue();
        (max > min).Should().BeTrue();
        (min <= minCopy).Should().BeTrue();
        (max >= maxCopy).Should().BeTrue();
    }
}

/// <summary>
/// Tests for NamespaceId identifier type.
/// </summary>
public class NamespaceIdTests
{
    [Fact]
    public void Constructor_SetsValueCorrectly()
    {
        var id = new NamespaceId(42);
        id.Value.Should().Be(42);
    }

    [Fact]
    public void None_ReturnsZeroValue()
    {
        NamespaceId.None.Value.Should().Be(0);
    }

    [Fact]
    public void Xml_ReturnsValue1()
    {
        NamespaceId.Xml.Value.Should().Be(1);
    }

    [Fact]
    public void Xmlns_ReturnsValue2()
    {
        NamespaceId.Xmlns.Value.Should().Be(2);
    }

    [Fact]
    public void Xsd_ReturnsValue3()
    {
        NamespaceId.Xsd.Value.Should().Be(3);
    }

    [Fact]
    public void Xsi_ReturnsValue4()
    {
        NamespaceId.Xsi.Value.Should().Be(4);
    }

    [Fact]
    public void Fn_ReturnsValue5()
    {
        NamespaceId.Fn.Value.Should().Be(5);
    }

    [Fact]
    public void FirstUserNamespaceId_IsCorrect()
    {
        NamespaceId.FirstUserNamespaceId.Should().Be(100);
    }

    [Fact]
    public void WellKnownNamespaces_AreOrdered()
    {
        NamespaceId.None.Value.Should().BeLessThan(NamespaceId.Xml.Value);
        NamespaceId.Xml.Value.Should().BeLessThan(NamespaceId.Xmlns.Value);
        NamespaceId.Xmlns.Value.Should().BeLessThan(NamespaceId.Xsd.Value);
        NamespaceId.Xsd.Value.Should().BeLessThan(NamespaceId.Xsi.Value);
        NamespaceId.Xsi.Value.Should().BeLessThan(NamespaceId.Fn.Value);
        NamespaceId.Fn.Value.Should().BeLessThan(NamespaceId.FirstUserNamespaceId);
    }

    [Fact]
    public void Default_EqualsNone()
    {
        var defaultId = default(NamespaceId);
        defaultId.Should().Be(NamespaceId.None);
        defaultId.Value.Should().Be(0);
    }

    [Theory]
    [InlineData(0u, "NS:0")]
    [InlineData(1u, "NS:1")]
    [InlineData(100u, "NS:100")]
    [InlineData(uint.MaxValue, "NS:4294967295")]
    public void ToString_FormatsCorrectly(uint value, string expected)
    {
        var id = new NamespaceId(value);
        id.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id1 = new NamespaceId(42);
        var id2 = new NamespaceId(42);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var id1 = new NamespaceId(42);
        var id2 = new NamespaceId(43);

        id1.Should().NotBe(id2);
        (id1 == id2).Should().BeFalse();
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_SameHashCode()
    {
        var id1 = new NamespaceId(42);
        var id2 = new NamespaceId(42);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Theory]
    [InlineData(1u, 2u, -1)]
    [InlineData(2u, 1u, 1)]
    [InlineData(5u, 5u, 0)]
    public void CompareTo_ReturnsCorrectResult(uint left, uint right, int expectedSign)
    {
        var id1 = new NamespaceId(left);
        var id2 = new NamespaceId(right);

        var result = id1.CompareTo(id2);
        Math.Sign(result).Should().Be(expectedSign);
    }

    [Theory]
    [InlineData(1u, 2u, true)]
    [InlineData(2u, 1u, false)]
    [InlineData(5u, 5u, false)]
    public void LessThan_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new NamespaceId(left);
        var id2 = new NamespaceId(right);

        (id1 < id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, 2u, true)]
    [InlineData(2u, 1u, false)]
    [InlineData(5u, 5u, true)]
    public void LessThanOrEqual_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new NamespaceId(left);
        var id2 = new NamespaceId(right);

        (id1 <= id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, 2u, false)]
    [InlineData(2u, 1u, true)]
    [InlineData(5u, 5u, false)]
    public void GreaterThan_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new NamespaceId(left);
        var id2 = new NamespaceId(right);

        (id1 > id2).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, 2u, false)]
    [InlineData(2u, 1u, true)]
    [InlineData(5u, 5u, true)]
    public void GreaterThanOrEqual_ReturnsCorrectResult(uint left, uint right, bool expected)
    {
        var id1 = new NamespaceId(left);
        var id2 = new NamespaceId(right);

        (id1 >= id2).Should().Be(expected);
    }

    [Fact]
    public void Operators_WithBoundaryValues()
    {
        var min = new NamespaceId(0);
        var minCopy = new NamespaceId(0);
        var max = new NamespaceId(uint.MaxValue);
        var maxCopy = new NamespaceId(uint.MaxValue);

        (min < max).Should().BeTrue();
        (max > min).Should().BeTrue();
        (min <= minCopy).Should().BeTrue();
        (max >= maxCopy).Should().BeTrue();
    }
}

/// <summary>
/// Tests for QName (qualified name) type.
/// </summary>
public class QNameTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var ns = new NamespaceId(42);
        var qname = new QName(ns, "element", "prefix");

        qname.Namespace.Should().Be(ns);
        qname.LocalName.Should().Be("element");
        qname.Prefix.Should().Be("prefix");
    }

    [Fact]
    public void Constructor_WithNullPrefix_SetsNullPrefix()
    {
        var ns = new NamespaceId(42);
        var qname = new QName(ns, "element");

        qname.Prefix.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullLocalName_ThrowsArgumentNullException()
    {
        var ns = new NamespaceId(42);

        var act = () => new QName(ns, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("localName");
    }

    [Theory]
    [InlineData("element", null, "element")]
    [InlineData("element", "xs", "xs:element")]
    [InlineData("attr", "xml", "xml:attr")]
    public void PrefixedName_FormatsCorrectly(string localName, string? prefix, string expected)
    {
        var qname = new QName(NamespaceId.None, localName, prefix);
        qname.PrefixedName.Should().Be(expected);
    }

    [Theory]
    [InlineData("element", null, "element")]
    [InlineData("element", "xs", "xs:element")]
    public void ToString_ReturnsPrefixedName(string localName, string? prefix, string expected)
    {
        var qname = new QName(NamespaceId.None, localName, prefix);
        qname.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var ns = new NamespaceId(42);
        var qname1 = new QName(ns, "element", "prefix");
        var qname2 = new QName(ns, "element", "prefix");

        qname1.Should().Be(qname2);
        (qname1 == qname2).Should().BeTrue();
        (qname1 != qname2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentNamespace_AreNotEqual()
    {
        var qname1 = new QName(new NamespaceId(1), "element", "prefix");
        var qname2 = new QName(new NamespaceId(2), "element", "prefix");

        qname1.Should().NotBe(qname2);
        (qname1 == qname2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentLocalName_AreNotEqual()
    {
        var ns = new NamespaceId(42);
        var qname1 = new QName(ns, "element1", "prefix");
        var qname2 = new QName(ns, "element2", "prefix");

        qname1.Should().NotBe(qname2);
        (qname1 == qname2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentPrefix_AreEqual()
    {
        // Per XML Namespaces §6.1, QName equality is based on namespace + local name only
        var ns = new NamespaceId(42);
        var qname1 = new QName(ns, "element", "prefix1");
        var qname2 = new QName(ns, "element", "prefix2");

        qname1.Should().Be(qname2);
        (qname1 == qname2).Should().BeTrue();
    }

    [Fact]
    public void Equality_NullPrefixVsEmptyPrefix_AreEqual()
    {
        // Prefix is irrelevant for QName equality
        var ns = new NamespaceId(42);
        var qname1 = new QName(ns, "element", null);
        var qname2 = new QName(ns, "element", "");

        qname1.Should().Be(qname2);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHashCode()
    {
        var ns = new NamespaceId(42);
        var qname1 = new QName(ns, "element", "prefix");
        var qname2 = new QName(ns, "element", "prefix");

        qname1.GetHashCode().Should().Be(qname2.GetHashCode());
    }

    [Fact]
    public void Default_HasDefaultValues()
    {
        var defaultQName = default(QName);

        defaultQName.Namespace.Should().Be(NamespaceId.None);
        defaultQName.LocalName.Should().BeNull();
        defaultQName.Prefix.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("longLocalNameThatIsStillValid")]
    [InlineData("name-with-hyphen")]
    [InlineData("name_with_underscore")]
    [InlineData("name.with.dots")]
    public void Constructor_AcceptsVariousLocalNames(string localName)
    {
        var ns = new NamespaceId(1);
        var qname = new QName(ns, localName);

        qname.LocalName.Should().Be(localName);
    }

    [Fact]
    public void QName_WithWellKnownNamespaces()
    {
        var xmlQName = new QName(NamespaceId.Xml, "lang", "xml");
        var xsdQName = new QName(NamespaceId.Xsd, "string", "xs");
        var fnQName = new QName(NamespaceId.Fn, "concat", "fn");

        xmlQName.ToString().Should().Be("xml:lang");
        xsdQName.ToString().Should().Be("xs:string");
        fnQName.ToString().Should().Be("fn:concat");
    }
}
