using System.ComponentModel;

namespace AutoLiquid_Library.Enum
{
    /// <summary>
    /// 量程
    /// </summary>
    public enum ELiquidRange
    {
        Ten = 10,
        [Description("12.5")]
        TwelvePointFive = 12,   // 枚举值本身只能是整数，这里设为12作为唯一标识
        Twenty = 20,
        Fifty = 50,
        OneHundredTen = 110,
        TwoHundred = 200,
        ThreeHundred = 300,
        OneThousand = 1000,
        OneThousandTwoHundred = 1200,
        FiveThousand = 5000,
    }
}