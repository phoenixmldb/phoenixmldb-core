namespace PhoenixmlDb.Xdm;

/// <summary>
/// XDM atomic value types based on XML Schema.
/// </summary>
public enum XdmType : byte
{
    // Special
    UntypedAtomic = 0,

    // String types
    XsString = 1,
    NormalizedString = 2,
    Token = 3,
    Language = 4,
    NmToken = 5,
    Name = 6,
    NCName = 7,
    Id = 8,
    IdRef = 9,
    Entity = 10,

    // Numeric types
    XsDecimal = 20,
    XsInteger = 21,
    XsLong = 22,
    XsInt = 23,
    XsShort = 24,
    Byte = 25,
    NonNegativeInteger = 26,
    PositiveInteger = 27,
    UnsignedLong = 28,
    UnsignedInt = 29,
    UnsignedShort = 30,
    UnsignedByte = 31,
    NonPositiveInteger = 32,
    NegativeInteger = 33,
    XsFloat = 34,
    XsDouble = 35,

    // Date/time types
    DateTime = 40,
    DateTimeStamp = 41,
    Date = 42,
    Time = 43,
    GYearMonth = 44,
    GYear = 45,
    GMonthDay = 46,
    GDay = 47,
    GMonth = 48,
    Duration = 49,
    YearMonthDuration = 50,
    DayTimeDuration = 51,

    // Other types
    Boolean = 60,
    Base64Binary = 61,
    HexBinary = 62,
    AnyUri = 63,
    QName = 64,
    Notation = 65,

    // XQuery 3.1 additions
    Map = 80,
    Array = 81,
    Function = 82
}
