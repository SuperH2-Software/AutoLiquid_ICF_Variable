using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using AutoLiquid_Library.Enum;

namespace AutoLiquid_ICF_Variable.EntityJson
{
    /// <summary>
    /// 最大量程设置
    /// </summary>
    [Serializable()]
    public class Range
    {
        // 10微升
        public decimal Ten = 15m;

        // 12.5微升
        public decimal TwelvePointFive = 12.5m;

        // 20微升
        public decimal Twenty = 20m;

        // 50微升
        public decimal Fifty = 50m;

        // 110微升
        public decimal OneHundredTen = 110m;

        // 200微升
        public decimal TwoHundred = 200m;

        // 300微升
        public decimal ThreeHundred = 300m;

        // 1000微升
        public decimal OneThousand = 1000m;

        // 1200微升
        public decimal OneThousandTwoHundred = 1200m;

        // 5000微升
        public decimal FiveThousand = 5000m;
    }
}
