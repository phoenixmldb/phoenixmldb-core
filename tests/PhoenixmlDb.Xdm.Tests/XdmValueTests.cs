using FluentAssertions;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm;
using Xunit;

namespace PhoenixmlDb.Xdm.Tests;

/// <summary>
/// Tests for XdmValue factory methods, conversions, and equality.
/// </summary>
public class XdmValueTests
{
    #region Factory Methods - Empty

    [Fact]
    public void Empty_ReturnsEmptyValue()
    {
        var value = XdmValue.Empty;

        value.IsEmpty.Should().BeTrue();
        value.Type.Should().Be(XdmType.UntypedAtomic);
        value.RawValue.Should().BeNull();
    }

    [Fact]
    public void Empty_AsString_ReturnsEmptyString()
    {
        var value = XdmValue.Empty;

        value.AsString().Should().BeEmpty();
    }

    [Fact]
    public void Empty_AsBoolean_ReturnsFalse()
    {
        var value = XdmValue.Empty;

        value.AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void Empty_AsLong_ReturnsZero()
    {
        var value = XdmValue.Empty;

        value.AsLong().Should().Be(0);
    }

    [Fact]
    public void Empty_AsDouble_ReturnsZero()
    {
        var value = XdmValue.Empty;

        value.AsDouble().Should().Be(0.0);
    }

    #endregion

    #region Factory Methods - String

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("unicode: \u00E9\u00E8\u00EA")]
    [InlineData("special chars: <>&\"'")]
    public void String_StoresValueCorrectly(string input)
    {
        var value = XdmValue.XsString(input);

        value.Type.Should().Be(XdmType.XsString);
        value.IsEmpty.Should().BeFalse();
        value.AsString().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    public void String_AsBoolean_ConvertsCorrectly(string input, bool expected)
    {
        var value = XdmValue.XsString(input);

        value.AsBoolean().Should().Be(expected);
    }

    #endregion

    #region Factory Methods - Boolean

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Boolean_StoresValueCorrectly(bool input)
    {
        var value = XdmValue.Boolean(input);

        value.Type.Should().Be(XdmType.Boolean);
        value.IsEmpty.Should().BeFalse();
        value.AsBoolean().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Boolean_AsString_ReturnsXmlForm(bool input, string expected)
    {
        var value = XdmValue.Boolean(input);

        value.AsString().Should().Be(expected);
    }

    #endregion

    #region Factory Methods - Integer

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(42L)]
    public void Integer_StoresValueCorrectly(long input)
    {
        var value = XdmValue.XsInteger(input);

        value.Type.Should().Be(XdmType.XsInteger);
        value.IsEmpty.Should().BeFalse();
        value.AsLong().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    [InlineData(-1L, true)]
    [InlineData(42L, true)]
    public void Integer_AsBoolean_ConvertsCorrectly(long input, bool expected)
    {
        var value = XdmValue.XsInteger(input);

        value.AsBoolean().Should().Be(expected);
    }

    [Fact]
    public void Integer_AsDouble_ConvertsCorrectly()
    {
        var value = XdmValue.XsInteger(42);

        value.AsDouble().Should().Be(42.0);
    }

    [Fact]
    public void Integer_AsDecimal_ConvertsCorrectly()
    {
        var value = XdmValue.XsInteger(42);

        value.AsDecimal().Should().Be(42m);
    }

    [Fact]
    public void Integer_AsInt_ConvertsCorrectly()
    {
        var value = XdmValue.XsInteger(42);

        value.AsInt().Should().Be(42);
    }

    #endregion

    #region Factory Methods - Decimal

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(-1.5)]
    [InlineData(123.456)]
    public void Decimal_StoresValueCorrectly(double inputDouble)
    {
        var input = (decimal)inputDouble;
        var value = XdmValue.XsDecimal(input);

        value.Type.Should().Be(XdmType.XsDecimal);
        value.IsEmpty.Should().BeFalse();
        value.AsDecimal().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void Decimal_AsLong_Truncates()
    {
        var value = XdmValue.XsDecimal(42.9m);

        value.AsLong().Should().Be(42);
    }

    [Fact]
    public void Decimal_AsDouble_ConvertsCorrectly()
    {
        var value = XdmValue.XsDecimal(42.5m);

        value.AsDouble().Should().Be(42.5);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.0, true)]
    [InlineData(-1.0, true)]
    public void Decimal_AsBoolean_ConvertsCorrectly(double inputDouble, bool expected)
    {
        var input = (decimal)inputDouble;
        var value = XdmValue.XsDecimal(input);

        value.AsBoolean().Should().Be(expected);
    }

    #endregion

    #region Factory Methods - Double

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void Double_StoresValueCorrectly(double input)
    {
        var value = XdmValue.XsDouble(input);

        value.Type.Should().Be(XdmType.XsDouble);
        value.IsEmpty.Should().BeFalse();

        if (double.IsNaN(input))
            double.IsNaN(value.AsDouble()).Should().BeTrue();
        else
            value.AsDouble().Should().Be(input);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.0, true)]
    [InlineData(-1.0, true)]
    [InlineData(double.NaN, false)]
    public void Double_AsBoolean_ConvertsCorrectly(double input, bool expected)
    {
        var value = XdmValue.XsDouble(input);

        value.AsBoolean().Should().Be(expected);
    }

    [Fact]
    public void Double_AsLong_Truncates()
    {
        var value = XdmValue.XsDouble(42.9);

        value.AsLong().Should().Be(42);
    }

    #endregion

    #region Factory Methods - Float

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    public void Float_StoresValueCorrectly(float input)
    {
        var value = XdmValue.XsFloat(input);

        value.Type.Should().Be(XdmType.XsFloat);
        value.IsEmpty.Should().BeFalse();

        if (float.IsNaN(input))
            float.IsNaN(value.AsFloat()).Should().BeTrue();
        else
            value.AsFloat().Should().Be(input);
    }

    [Theory]
    [InlineData(0.0f, false)]
    [InlineData(1.0f, true)]
    [InlineData(-1.0f, true)]
    [InlineData(float.NaN, false)]
    public void Float_AsBoolean_ConvertsCorrectly(float input, bool expected)
    {
        var value = XdmValue.XsFloat(input);

        value.AsBoolean().Should().Be(expected);
    }

    #endregion

    #region Factory Methods - DateTime

    [Fact]
    public void DateTime_StoresValueCorrectly()
    {
        var input = new DateTimeOffset(2024, 6, 15, 10, 30, 45, TimeSpan.FromHours(-5));
        var value = XdmValue.DateTime(input);

        value.Type.Should().Be(XdmType.DateTime);
        value.IsEmpty.Should().BeFalse();
        value.AsDateTime().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void DateTime_AsString_ReturnsIso8601Format()
    {
        var input = new DateTimeOffset(2024, 6, 15, 10, 30, 45, TimeSpan.Zero);
        var value = XdmValue.DateTime(input);

        value.AsString().Should().Contain("2024-06-15");
        value.AsString().Should().Contain("10:30:45");
    }

    [Fact]
    public void DateTime_AsDate_ReturnsDatePart()
    {
        var input = new DateTimeOffset(2024, 6, 15, 10, 30, 45, TimeSpan.Zero);
        var value = XdmValue.DateTime(input);

        value.AsDate().Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void DateTime_AsTime_ReturnsTimePart()
    {
        var input = new DateTimeOffset(2024, 6, 15, 10, 30, 45, TimeSpan.Zero);
        var value = XdmValue.DateTime(input);

        value.AsTime().Should().Be(new TimeOnly(10, 30, 45));
    }

    #endregion

    #region Factory Methods - Date

    [Fact]
    public void Date_StoresValueCorrectly()
    {
        var input = new DateOnly(2024, 6, 15);
        var value = XdmValue.Date(input);

        value.Type.Should().Be(XdmType.Date);
        value.IsEmpty.Should().BeFalse();
        value.AsDate().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void Date_AsString_ReturnsIso8601Format()
    {
        var input = new DateOnly(2024, 6, 15);
        var value = XdmValue.Date(input);

        value.AsString().Should().Be("2024-06-15");
    }

    [Fact]
    public void Date_AsDateTime_ReturnsDateAtMidnight()
    {
        var input = new DateOnly(2024, 6, 15);
        var value = XdmValue.Date(input);

        var dateTime = value.AsDateTime();
        dateTime.Year.Should().Be(2024);
        dateTime.Month.Should().Be(6);
        dateTime.Day.Should().Be(15);
    }

    #endregion

    #region Factory Methods - Time

    [Fact]
    public void Time_StoresValueCorrectly()
    {
        var input = new TimeOnly(10, 30, 45, 123);
        var value = XdmValue.Time(input);

        value.Type.Should().Be(XdmType.Time);
        value.IsEmpty.Should().BeFalse();
        value.AsTime().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void Time_AsString_ReturnsCorrectFormat()
    {
        var input = new TimeOnly(10, 30, 45);
        var value = XdmValue.Time(input);

        value.AsString().Should().Be("10:30:45");
    }

    #endregion

    #region Factory Methods - Duration

    [Fact]
    public void Duration_StoresValueCorrectly()
    {
        var input = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30);
        var value = XdmValue.Duration(input);

        value.Type.Should().Be(XdmType.Duration);
        value.IsEmpty.Should().BeFalse();
        value.AsDuration().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void Duration_Negative_StoresCorrectly()
    {
        var input = -TimeSpan.FromHours(1);
        var value = XdmValue.Duration(input);

        value.AsDuration().Should().Be(input);
    }

    #endregion

    #region Factory Methods - QName

    [Fact]
    public void QName_StoresValueCorrectly()
    {
        var input = new XdmQName(NamespaceId.Xsd, "string", "xs");
        var value = XdmValue.QName(input);

        value.Type.Should().Be(XdmType.QName);
        value.IsEmpty.Should().BeFalse();
        value.AsQName().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void QName_WithoutPrefix_StoresCorrectly()
    {
        var input = XdmQName.Local("localName");
        var value = XdmValue.QName(input);

        value.AsQName().LocalName.Should().Be("localName");
        value.AsQName().IsUnqualified.Should().BeTrue();
    }

    #endregion

    #region Factory Methods - AnyUri

    [Fact]
    public void AnyUri_FromUri_StoresValueCorrectly()
    {
        var input = new Uri("https://example.com/path?query=1");
        var value = XdmValue.AnyUri(input);

        value.Type.Should().Be(XdmType.AnyUri);
        value.IsEmpty.Should().BeFalse();
        value.AsUri().Should().Be(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void AnyUri_FromString_StoresValueCorrectly()
    {
        var value = XdmValue.AnyUri("https://example.com/path");

        value.Type.Should().Be(XdmType.AnyUri);
        value.AsUri().ToString().Should().Be("https://example.com/path");
    }

    [Fact]
    public void AnyUri_RelativeUri_StoresCorrectly()
    {
        var value = XdmValue.AnyUri("/relative/path");

        value.AsUri().IsAbsoluteUri.Should().BeFalse();
    }

    #endregion

    #region Factory Methods - Base64Binary

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x01, 0x02, 0x03 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0xFD })]
    public void Base64Binary_StoresValueCorrectly(byte[] input)
    {
        var value = XdmValue.Base64Binary(input);

        value.Type.Should().Be(XdmType.Base64Binary);
        value.IsEmpty.Should().BeFalse();
        value.AsBinary().Should().BeEquivalentTo(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void Base64Binary_AsString_ReturnsBase64Encoded()
    {
        var input = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var value = XdmValue.Base64Binary(input);

        value.AsString().Should().Be("SGVsbG8=");
    }

    #endregion

    #region Factory Methods - HexBinary

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x01, 0x02, 0x03 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0xFD })]
    public void HexBinary_StoresValueCorrectly(byte[] input)
    {
        var value = XdmValue.HexBinary(input);

        value.Type.Should().Be(XdmType.HexBinary);
        value.IsEmpty.Should().BeFalse();
        value.AsBinary().Should().BeEquivalentTo(input);
        value.RawValue.Should().Be(input);
    }

    [Fact]
    public void HexBinary_AsString_ReturnsHexEncoded()
    {
        var input = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var value = XdmValue.HexBinary(input);

        value.AsString().Should().Be("48656C6C6F");
    }

    #endregion

    #region Factory Methods - UntypedAtomic

    [Theory]
    [InlineData("")]
    [InlineData("some value")]
    [InlineData("123")]
    public void UntypedAtomic_StoresValueCorrectly(string input)
    {
        var value = XdmValue.UntypedAtomic(input);

        value.Type.Should().Be(XdmType.UntypedAtomic);
        value.IsEmpty.Should().BeFalse();
        value.AsString().Should().Be(input);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var value1 = XdmValue.XsInteger(42);
        var value2 = XdmValue.XsInteger(42);

        value1.Equals(value2).Should().BeTrue();
        (value1 == value2).Should().BeTrue();
        (value1 != value2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var value1 = XdmValue.XsInteger(42);
        var value2 = XdmValue.XsInteger(43);

        value1.Equals(value2).Should().BeFalse();
        (value1 == value2).Should().BeFalse();
        (value1 != value2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTypes_AreNotEqual()
    {
        var value1 = XdmValue.XsInteger(42);
        var value2 = XdmValue.XsDouble(42.0);

        value1.Equals(value2).Should().BeFalse();
    }

    [Fact]
    public void Equality_StringValues_AreEqual()
    {
        var value1 = XdmValue.XsString("hello");
        var value2 = XdmValue.XsString("hello");

        value1.Equals(value2).Should().BeTrue();
    }

    [Fact]
    public void Equality_EmptyValues_AreEqual()
    {
        var value1 = XdmValue.Empty;
        var value2 = XdmValue.Empty;

        value1.Equals(value2).Should().BeTrue();
    }

    [Fact]
    public void Equality_ObjectEquals_WorksCorrectly()
    {
        object value1 = XdmValue.XsInteger(42);
        object value2 = XdmValue.XsInteger(42);

        value1.Equals(value2).Should().BeTrue();
    }

    [Fact]
    public void Equality_ObjectEqualsNull_ReturnsFalse()
    {
        var value = XdmValue.XsInteger(42);

        value.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equality_ObjectEqualsOtherType_ReturnsFalse()
    {
        var value = XdmValue.XsInteger(42);

        value.Equals("42").Should().BeFalse();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_SameValues_HaveSameHash()
    {
        var value1 = XdmValue.XsInteger(42);
        var value2 = XdmValue.XsInteger(42);

        value1.GetHashCode().Should().Be(value2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_LikelyDifferentHash()
    {
        var value1 = XdmValue.XsInteger(42);
        var value2 = XdmValue.XsInteger(43);

        // Not guaranteed, but very likely for different values
        value1.GetHashCode().Should().NotBe(value2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData(42L, "42")]
    [InlineData(0L, "0")]
    [InlineData(-1L, "-1")]
    public void ToString_Integer_ReturnsStringRepresentation(long input, string expected)
    {
        var value = XdmValue.XsInteger(input);

        value.ToString().Should().Be(expected);
    }

    [Fact]
    public void ToString_Boolean_ReturnsXmlForm()
    {
        XdmValue.Boolean(true).ToString().Should().Be("true");
        XdmValue.Boolean(false).ToString().Should().Be("false");
    }

    [Fact]
    public void ToString_String_ReturnsValue()
    {
        var value = XdmValue.XsString("hello");

        value.ToString().Should().Be("hello");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void AsQName_FromNull_ThrowsInvalidCastException()
    {
        var value = XdmValue.Empty;

        var act = () => value.AsQName();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsQName_FromString_ThrowsInvalidCastException()
    {
        var value = XdmValue.XsString("not a qname");

        var act = () => value.AsQName();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsUri_FromNull_ThrowsInvalidCastException()
    {
        var value = XdmValue.Empty;

        var act = () => value.AsUri();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsLong_FromUri_ThrowsInvalidCastException()
    {
        var value = XdmValue.AnyUri("https://example.com");

        var act = () => value.AsLong();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsDouble_FromUri_ThrowsInvalidCastException()
    {
        var value = XdmValue.AnyUri("https://example.com");

        var act = () => value.AsDouble();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsDecimal_FromUri_ThrowsInvalidCastException()
    {
        var value = XdmValue.AnyUri("https://example.com");

        var act = () => value.AsDecimal();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsBoolean_FromUri_ThrowsInvalidCastException()
    {
        var value = XdmValue.AnyUri("https://example.com");

        var act = () => value.AsBoolean();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AsDateTime_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("2024-06-15T10:30:45Z");

        value.AsDateTime().Year.Should().Be(2024);
        value.AsDateTime().Month.Should().Be(6);
        value.AsDateTime().Day.Should().Be(15);
    }

    [Fact]
    public void AsDateTime_FromInvalidString_ThrowsFormatException()
    {
        var value = XdmValue.XsString("not a date");

        var act = () => value.AsDateTime();

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void AsDate_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("2024-06-15");

        value.AsDate().Year.Should().Be(2024);
        value.AsDate().Month.Should().Be(6);
        value.AsDate().Day.Should().Be(15);
    }

    [Fact]
    public void AsTime_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("10:30:45");

        value.AsTime().Hour.Should().Be(10);
        value.AsTime().Minute.Should().Be(30);
        value.AsTime().Second.Should().Be(45);
    }

    [Fact]
    public void AsDuration_FromString_ParsesXmlDuration()
    {
        var value = XdmValue.XsString("PT2H30M");

        value.AsDuration().Should().Be(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AsLong_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("12345");

        value.AsLong().Should().Be(12345);
    }

    [Fact]
    public void AsDouble_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("123.456");

        value.AsDouble().Should().BeApproximately(123.456, 0.0001);
    }

    [Fact]
    public void AsDecimal_FromString_ParsesCorrectly()
    {
        var value = XdmValue.XsString("123.456");

        value.AsDecimal().Should().Be(123.456m);
    }

    [Fact]
    public void AsBinary_FromEmpty_ReturnsEmptyArray()
    {
        var value = XdmValue.Empty;

        value.AsBinary().Should().BeEmpty();
    }

    [Fact]
    public void AsBinary_FromInteger_ThrowsInvalidCastException()
    {
        var value = XdmValue.XsInteger(42);

        var act = () => value.AsBinary();

        act.Should().Throw<InvalidCastException>();
    }

    #endregion

    #region String Conversions From Numeric Types

    [Fact]
    public void AsString_FromLong_ParsesCorrectly()
    {
        var value = XdmValue.XsInteger(123);

        value.AsLong().Should().Be(123);
    }

    [Fact]
    public void AsString_FromDouble_ParsesCorrectly()
    {
        var value = XdmValue.XsDouble(3.14);

        value.AsDouble().Should().BeApproximately(3.14, 0.0001);
    }

    [Fact]
    public void AsFloat_FromDouble_ConvertsCorrectly()
    {
        var value = XdmValue.XsDouble(3.14);

        value.AsFloat().Should().BeApproximately(3.14f, 0.001f);
    }

    [Fact]
    public void AsDecimal_FromLong_ConvertsCorrectly()
    {
        var value = XdmValue.XsInteger(12345);

        value.AsDecimal().Should().Be(12345m);
    }

    #endregion
}
