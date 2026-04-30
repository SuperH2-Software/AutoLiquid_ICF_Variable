using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using AutoLiquid_ICF_Variable.EntityJson;

namespace AutoLiquid_ICF_Variable.EntityCommon
{
    /// <summary>
    /// 取枪头信息 model
    /// </summary>
    public class TakeTipInfo
    {
        // 取枪头盘位
        public int TemplateIndex = 0;

        // 取枪头孔位
        public Position Pos = new Position();

        // 本次所用的枪头行、列index列表
        public List<RowCol> TipUsedIndexList = new List<RowCol>();
    }
}
