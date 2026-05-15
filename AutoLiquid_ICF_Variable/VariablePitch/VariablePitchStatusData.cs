using System.Collections.Generic;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 0x0A 读取移液枪状态 应答解析
    /// 应答帧：00 0A S1 S2 00 00 00 00
    ///   S1(DATA1): Bit0=活塞自检中  Bit1=校准中
    ///   S2(DATA2): Bit0=松开枪头中  Bit1=14MM间距(原点)  Bit2=吸液中  Bit3=排液中
    /// </summary>
    public class VariablePitchStatusData
    {
        public byte DATA1 { get; }   // DATA1（活塞自检中 / 校准中）
        public byte DATA2 { get; }   // DATA2 （松开枪头中 / 14MM原点 / 吸液中 / 排液中）

        /// <summary>Bit0 S1：正在松开枪头</summary>
        public bool IsReleasingTip => (DATA2 & 0x01) != 0;
        /// <summary>Bit1 S1：14MM间距（原点）</summary>
        public bool IsAtOrigin14mm => (DATA2 & 0x02) != 0;
        /// <summary>Bit2 S1：吸液中</summary>
        public bool IsAspirating => (DATA2 & 0x04) != 0;
        /// <summary>Bit3 S1：排液中</summary>
        public bool IsDispensing => (DATA2 & 0x08) != 0;
        /// <summary>Bit0 S2：活塞自检中</summary>
        public bool IsPistonHoming => (DATA1 & 0x01) != 0;
        /// <summary>Bit1 S2：校准中</summary>
        public bool IsCalibrating => (DATA1 & 0x02) != 0;
        /// <summary>模块是否正在执行动作</summary>
        public bool IsBusy =>
            IsReleasingTip || IsAspirating || IsDispensing ||
            IsPistonHoming || IsCalibrating;

        public VariablePitchStatusData(byte data1 = 0, byte data2 = 0)
        {
            DATA1 = data1;
            DATA2 = data2;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (IsReleasingTip) parts.Add("松开枪头中");
            if (IsAtOrigin14mm) parts.Add("14MM原点");
            if (IsAspirating) parts.Add("吸液中");
            if (IsDispensing) parts.Add("排液中");
            if (IsPistonHoming) parts.Add("活塞自检中");
            if (IsCalibrating) parts.Add("校准中");
            return parts.Count > 0 ? string.Join(" | ", parts) : "空闲";
        }
    }
}