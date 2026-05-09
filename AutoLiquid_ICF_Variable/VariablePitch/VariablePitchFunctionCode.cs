using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 变距移液模块 CAN 功能代码
    /// 通讯参数：CAN 500K，标准帧，数据帧，16进制
    /// 帧格式：CAN_ID=设备ID，DATA[0]=功能代码，DATA[1-7]=参数
    /// </summary>
    public static class VariablePitchFunctionCode
    {
        /// <summary>0x01 松开枪头</summary>
        public const byte ReleaseTip = 0x01;
        /// <summary>0x02 变距（1mm步进，4.5~14mm）</summary>
        public const byte SetPitch = 0x02;
        /// <summary>0x03 0.5mm步进变距（4.5~14.0mm）</summary>
        public const byte SetPitchHalf = 0x03;
        /// <summary>0x04 吸液</summary>
        public const byte Aspirate = 0x04;
        /// <summary>0x05 排液</summary>
        public const byte Dispense = 0x05;
        /// <summary>0x06 设置吸排液速度</summary>
        public const byte SetSpeed = 0x06;
        /// <summary>0x07 读取吸排液速度</summary>
        public const byte ReadSpeed = 0x07;
        /// <summary>0x08 活塞自检回零</summary>
        public const byte PistonHome = 0x08;
        /// <summary>0x09 读取吸液排液量</summary>
        public const byte ReadVolume = 0x09;
        /// <summary>0x0A 读取移液枪状态</summary>
        public const byte ReadStatus = 0x0A;
        /// <summary>0x88 产品ID修改</summary>
        public const byte ModifyId = 0x88;
        /// <summary>0x89 查询产品ID</summary>
        public const byte QueryId = 0x89;
    }
}
