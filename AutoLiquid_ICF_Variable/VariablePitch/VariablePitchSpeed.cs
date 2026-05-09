using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 变距移液模块速度档位（对应0x06/0x07命令）
    /// </summary>
    public enum VariablePitchSpeed : byte
    {
        Slow = 0x01, // 慢
        Medium = 0x02, // 中
        Fast = 0x03, // 快
        Fastest = 0x04  // 最快
    }
}
