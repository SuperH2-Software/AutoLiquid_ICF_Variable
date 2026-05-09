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
    /// 移液头设置父类
    /// </summary>
    [Serializable()]
    public class Head
    {
        // 是否启用移液头
        public bool Available = true;

        /**
         * 可变距
         */
        // 是否可变距（true=外挂变距模块，false=机器原始不变距模块）
        public bool IsVariable = false;
        // 变距是否与x、y轴同时运动
        public bool VariableMoveSameTime = true;
        // 变距模块 CAN 设备 ID（默认 0x01，可通过调试界面修改）
        public byte VariableCanDeviceId = 0x01;

        /**
         * Y轴相关
         */
        // Y轴是否可用
        public bool YAvailable = true;
        // Y轴是否使用移液头移动
        public bool YMoveWithHead = false;
        // 脱枪头时是否移动Y轴
        public bool YMoveWhileReleaseTip = true;

        /**
         * P轴相关
         */
        // P轴是否可用（变距模块时 P 轴由 CAN 接管）
        public bool PAvailable = true;


        /**
         * 移液头属性
         */
        // 通道行数
        public int ChannelRow = 1;
        // 通道列数
        public int ChannelCol = 1;
        // 通道间距（mm），变距模式下此值作为 CAN 变距目标值
        public decimal ChannelStep = 9.0m;
        // 移液头量程
        public ELiquidRange HeadLiquidRange = ELiquidRange.Ten;
        // 行走逻辑
        public EWalkingLogic WalkingLogic = EWalkingLogic.SameTime;


        /**
         * 推脱板
         */
        // 退枪头是否使用推脱板（否：使用拉提方式）
        public bool ReleaseTipUsePush = false;
        // 推脱板推脱次数
        public int ReleaseTipUsePushCount = 2;
        // 推脱板轴
        public EAxis ReleaseTipAxis = EAxis.P;

        /**
         * 速度
         */
        // 速度设置是否可见
        public bool SpeedVisible = false;
    }
}
