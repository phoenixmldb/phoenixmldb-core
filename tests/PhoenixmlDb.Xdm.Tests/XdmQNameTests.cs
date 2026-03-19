using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for XdmQName and XdmTypeName.
/// </summary>
public class XdmQNameTests
{
    #region XdmQName Construction

    [Fact]
    public void Constructor_WithNamespaceAndLocalName_SetsPropertiesCorrectly()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "string", null);

        qname.Namespace.Should().Be(NamespaceId.Xsd);
        qname.LocalName.Should().Be("string");
        qname.Prefix.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithPrefix_SetsAllProperties()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "string", "xs");

        qname.Namespace.Should().Be(NamespaceId.Xsd);
        qname.LocalName.Should().Be("string");
        qname.Prefix.Should().Be("xs");
    }

    [Fact]
    public void Constructor_WithNullLocalName_ThrowsArgumentNullException()
    {
        var act = () => new XdmQName(NamespaceId.None, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("localName");
    }

    [Theory]
    [InlineData("element")]
    [InlineData("attribute")]
    [InlineData("my-element")]
    [InlineData("element123")]
    public void Constructor_WithValidLocalName_StoresValue(string localName)
    {
        var qname = new XdmQName(NamespaceId.None, localName);

        qname.LocalName.Should().Be(localName);
    }

    #endregion

    #region PrefixedName Property

    [Fact]
    public void PrefixedName_WithPrefix_ReturnsPrefixedForm()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "string", "xs");

        qname.PrefixedName.Should().Be("xs:string");
    }

    [Fact]
    public void PrefixedName_WithoutPrefix_ReturnsLocalNameOnly()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "string", null);

        qname.PrefixedName.Should().Be("string");
    }

    [Fact]
    public void PrefixedName_WithEmptyPrefix_ReturnsLocalNameOnly()
    {
        var qname = new XdmQName(NamespaceId.None, "element", null);

        qname.PrefixedName.Should().Be("element");
    }

    [Theory]
    [InlineData("html", "div", "html:div")]
    [InlineData("svg", "path", "svg:path")]
    [InlineData("xlink", "href", "xlink:href")]
    public void PrefixedName_VariousPrefixes_FormatsCorrectly(string prefix, string localName, string expected)
    {
        var qname = new XdmQName(new NamespaceId(100), localName, prefix);

        qname.PrefixedName.Should().Be(expected);
    }

    #endregion

    #region IsUnqualified Property

    [Fact]
    public void IsUnqualified_WithNoNamespace_ReturnsTrue()
    {
        var qname = new XdmQName(NamespaceId.None, "element");

        qname.IsUnqualified.Should().BeTrue();
    }

    [Fact]
    public void IsUnqualified_WithNamespace_ReturnsFalse()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "string");

        qname.IsUnqualified.Should().BeFalse();
    }

    [Theory]
    [InlineData(1u, false)]  // Xml namespace
    [InlineData(2u, false)]  // Xmlns namespace
    [InlineData(3u, false)]  // Xsd namespace
    [InlineData(0u, true)]   // None
    public void IsUnqualified_ForWellKnownNamespaces_ReturnsExpected(uint nsValue, bool expected)
    {
        var qname = new XdmQName(new NamespaceId(nsValue), "test");

        qname.IsUnqualified.Should().Be(expected);
    }

    #endregion

    #region Local Factory Method

    [Fact]
    public void Local_CreatesUnqualifiedQName()
    {
        var qname = XdmQName.Local("element");

        qname.Namespace.Should().Be(NamespaceId.None);
        qname.LocalName.Should().Be("element");
        qname.Prefix.Should().BeNull();
        qname.IsUnqualified.Should().BeTrue();
    }

    [Theory]
    [InlineData("div")]
    [InlineData("span")]
    [InlineData("custom-element")]
    public void Local_WithDifferentNames_CreatesCorrectQNames(string localName)
    {
        var qname = XdmQName.Local(localName);

        qname.LocalName.Should().Be(localName);
        qname.IsUnqualified.Should().BeTrue();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_WithPrefix_ReturnsPrefixedName()
    {
        var qname = new XdmQName(NamespaceId.Xsd, "integer", "xs");

        qname.ToString().Should().Be("xs:integer");
    }

    [Fact]
    public void ToString_WithoutPrefix_ReturnsLocalName()
    {
        var qname = new XdmQName(NamespaceId.None, "element");

        qname.ToString().Should().Be("element");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string", "xs");
        var qname2 = new XdmQName(NamespaceId.Xsd, "string", "xs");

        qname1.Should().Be(qname2);
        (qname1 == qname2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentNamespace_AreNotEqual()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string");
        var qname2 = new XdmQName(NamespaceId.Fn, "string");

        qname1.Should().NotBe(qname2);
        (qname1 != qname2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentLocalName_AreNotEqual()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string");
        var qname2 = new XdmQName(NamespaceId.Xsd, "integer");

        qname1.Should().NotBe(qname2);
    }

    [Fact]
    public void Equality_DifferentPrefix_AreNotEqual()
    {
        // Note: In XQuery, prefixes are typically ignored for equality
        // but this implementation includes them in the record equality
        var qname1 = new XdmQName(NamespaceId.Xsd, "string", "xs");
        var qname2 = new XdmQName(NamespaceId.Xsd, "string", "xsd");

        // Since XdmQName is a record struct, different prefixes make them unequal
        qname1.Should().NotBe(qname2);
    }

    [Fact]
    public void Equality_SameNamespaceAndLocal_DifferentPrefix_Comparison()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string", "xs");
        var qname2 = new XdmQName(NamespaceId.Xsd, "string", null);

        // With prefix vs without prefix are considered different
        qname1.Should().NotBe(qname2);
    }

    #endregion

    #region GetHashCode

    [Fact]
    public void GetHashCode_SameValues_HaveSameHash()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string", "xs");
        var qname2 = new XdmQName(NamespaceId.Xsd, "string", "xs");

        qname1.GetHashCode().Should().Be(qname2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_LikelyDifferentHash()
    {
        var qname1 = new XdmQName(NamespaceId.Xsd, "string");
        var qname2 = new XdmQName(NamespaceId.Xsd, "integer");

        qname1.GetHashCode().Should().NotBe(qname2.GetHashCode());
    }

    #endregion
}

/// <summary>
/// Tests for XdmTypeName.
/// </summary>
public class XdmTypeNameTests
{
    #region Construction

    [Fact]
    public void Constructor_WithNamespaceAndLocalName_SetsProperties()
    {
        var typeName = new XdmTypeName(NamespaceId.Xsd, "string");

        typeName.Namespace.Should().Be(NamespaceId.Xsd);
        typeName.LocalName.Should().Be("string");
    }

    [Fact]
    public void Constructor_WithNullLocalName_ThrowsArgumentNullException()
    {
        var act = () => new XdmTypeName(NamespaceId.Xsd, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("localName");
    }

    #endregion

    #region Pre-defined Type Constants

    [Fact]
    public void Untyped_HasCorrectValues()
    {
        XdmTypeName.Untyped.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Untyped.LocalName.Should().Be("untyped");
    }

    [Fact]
    public void UntypedAtomic_HasCorrectValues()
    {
        XdmTypeName.UntypedAtomic.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.UntypedAtomic.LocalName.Should().Be("untypedAtomic");
    }

    [Fact]
    public void AnyType_HasCorrectValues()
    {
        XdmTypeName.AnyType.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.AnyType.LocalName.Should().Be("anyType");
    }

    [Fact]
    public void AnySimpleType_HasCorrectValues()
    {
        XdmTypeName.AnySimpleType.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.AnySimpleType.LocalName.Should().Be("anySimpleType");
    }

    [Fact]
    public void AnyAtomicType_HasCorrectValues()
    {
        XdmTypeName.AnyAtomicType.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.AnyAtomicType.LocalName.Should().Be("anyAtomicType");
    }

    [Fact]
    public void String_HasCorrectValues()
    {
        XdmTypeName.XsString.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.XsString.LocalName.Should().Be("string");
    }

    [Fact]
    public void Boolean_HasCorrectValues()
    {
        XdmTypeName.Boolean.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Boolean.LocalName.Should().Be("boolean");
    }

    [Fact]
    public void Decimal_HasCorrectValues()
    {
        XdmTypeName.XsDecimal.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.XsDecimal.LocalName.Should().Be("decimal");
    }

    [Fact]
    public void Integer_HasCorrectValues()
    {
        XdmTypeName.XsInteger.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.XsInteger.LocalName.Should().Be("integer");
    }

    [Fact]
    public void Double_HasCorrectValues()
    {
        XdmTypeName.XsDouble.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.XsDouble.LocalName.Should().Be("double");
    }

    [Fact]
    public void Float_HasCorrectValues()
    {
        XdmTypeName.XsFloat.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.XsFloat.LocalName.Should().Be("float");
    }

    [Fact]
    public void Date_HasCorrectValues()
    {
        XdmTypeName.Date.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Date.LocalName.Should().Be("date");
    }

    [Fact]
    public void DateTime_HasCorrectValues()
    {
        XdmTypeName.DateTime.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.DateTime.LocalName.Should().Be("dateTime");
    }

    [Fact]
    public void Time_HasCorrectValues()
    {
        XdmTypeName.Time.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Time.LocalName.Should().Be("time");
    }

    [Fact]
    public void Duration_HasCorrectValues()
    {
        XdmTypeName.Duration.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Duration.LocalName.Should().Be("duration");
    }

    [Fact]
    public void QName_HasCorrectValues()
    {
        XdmTypeName.QName.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.QName.LocalName.Should().Be("QName");
    }

    [Fact]
    public void AnyUri_HasCorrectValues()
    {
        XdmTypeName.AnyUri.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.AnyUri.LocalName.Should().Be("anyURI");
    }

    [Fact]
    public void Base64Binary_HasCorrectValues()
    {
        XdmTypeName.Base64Binary.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.Base64Binary.LocalName.Should().Be("base64Binary");
    }

    [Fact]
    public void HexBinary_HasCorrectValues()
    {
        XdmTypeName.HexBinary.Namespace.Should().Be(NamespaceId.Xsd);
        XdmTypeName.HexBinary.LocalName.Should().Be("hexBinary");
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ReturnsXsPrefixedName()
    {
        var typeName = new XdmTypeName(NamespaceId.Xsd, "string");

        typeName.ToString().Should().Be("xs:string");
    }

    [Fact]
    public void ToString_ForPredefinedTypes_FormatsCorrectly()
    {
        XdmTypeName.XsString.ToString().Should().Be("xs:string");
        XdmTypeName.XsInteger.ToString().Should().Be("xs:integer");
        XdmTypeName.Boolean.ToString().Should().Be("xs:boolean");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var type1 = new XdmTypeName(NamespaceId.Xsd, "string");
        var type2 = new XdmTypeName(NamespaceId.Xsd, "string");

        type1.Should().Be(type2);
    }

    [Fact]
    public void Equality_DifferentNamespace_AreNotEqual()
    {
        var type1 = new XdmTypeName(NamespaceId.Xsd, "string");
        var type2 = new XdmTypeName(NamespaceId.Fn, "string");

        type1.Should().NotBe(type2);
    }

    [Fact]
    public void Equality_DifferentLocalName_AreNotEqual()
    {
        var type1 = new XdmTypeName(NamespaceId.Xsd, "string");
        var type2 = new XdmTypeName(NamespaceId.Xsd, "integer");

        type1.Should().NotBe(type2);
    }

    [Fact]
    public void Equality_PredefinedTypes_AreEqual()
    {
        XdmTypeName.XsString.Should().Be(XdmTypeName.XsString);
        XdmTypeName.XsInteger.Should().Be(XdmTypeName.XsInteger);
    }

    #endregion

    #region GetHashCode

    [Fact]
    public void GetHashCode_SameValues_HaveSameHash()
    {
        var type1 = new XdmTypeName(NamespaceId.Xsd, "string");
        var type2 = new XdmTypeName(NamespaceId.Xsd, "string");

        type1.GetHashCode().Should().Be(type2.GetHashCode());
    }

    #endregion
}

/// <summary>
/// Tests for NamespaceBinding.
/// </summary>
public class NamespaceBindingTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var binding = new NamespaceBinding("xs", NamespaceId.Xsd);

        binding.Prefix.Should().Be("xs");
        binding.Namespace.Should().Be(NamespaceId.Xsd);
    }

    [Fact]
    public void DefaultNamespace_HasEmptyPrefix()
    {
        var binding = new NamespaceBinding("", NamespaceId.Xsd);

        binding.Prefix.Should().BeEmpty();
        binding.Namespace.Should().Be(NamespaceId.Xsd);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var binding1 = new NamespaceBinding("xs", NamespaceId.Xsd);
        var binding2 = new NamespaceBinding("xs", NamespaceId.Xsd);

        binding1.Should().Be(binding2);
    }

    [Fact]
    public void Equality_DifferentPrefix_AreNotEqual()
    {
        var binding1 = new NamespaceBinding("xs", NamespaceId.Xsd);
        var binding2 = new NamespaceBinding("xsd", NamespaceId.Xsd);

        binding1.Should().NotBe(binding2);
    }

    [Fact]
    public void Equality_DifferentNamespace_AreNotEqual()
    {
        var binding1 = new NamespaceBinding("ns", NamespaceId.Xsd);
        var binding2 = new NamespaceBinding("ns", NamespaceId.Fn);

        binding1.Should().NotBe(binding2);
    }
}
