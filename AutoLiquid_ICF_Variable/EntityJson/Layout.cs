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
    /// 盘位行列布局
    /// </summary>
    [Serializable()]
    public class Layout
    {
        // 行数
        public int RowCount = 3;

        // 列数
        public int ColCount = 3;
    }
}
