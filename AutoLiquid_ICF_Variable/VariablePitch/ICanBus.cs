using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// CAN 总线通信抽象接口
    /// 变距移液模块规格：500K 波特率，标准帧（11位ID），数据帧，8字节
    ///
    /// 接入真实硬件时，请实现此接口并注入 VariablePitchManager：
    ///   - ZLGCAN（周立功）: 使用 ControlCAN.dll SDK
    ///   - PCAN-USB:         使用 PCAN-Basic API
    ///   - 串口转CAN网关:     SerialPort + 协议拆包
    /// </summary>
    public interface ICanBus : IDisposable
    {
        /// <summary>CAN 设备是否已打开</summary>
        bool IsOpen { get; }

        /// <summary>打开 CAN 设备（500K，标准帧）</summary>
        bool Open();

        /// <summary>关闭 CAN 设备</summary>
        void Close();

        /// <summary>
        /// 发送 8 字节 CAN 标准数据帧
        /// </summary>
        /// <param name="canId">CAN 帧标识符（= 设备ID，标准11位）</param>
        /// <param name="data">8 字节载荷：data[0]=功能代码，data[1-7]=参数</param>
        bool Send(uint canId, byte[] data);

        /// <summary>
        /// 发送帧并等待应答
        /// 应答帧特征：data[0]==0x00，data[1]==发送的功能代码
        /// </summary>
        /// <param name="canId">发送帧 CAN ID</param>
        /// <param name="data">8 字节发送数据</param>
        /// <param name="timeoutMs">超时毫秒</param>
        /// <returns>8 字节应答载荷；超时或失败返回 null</returns>
        Task<byte[]> SendAndReceiveAsync(uint canId, byte[] data, int timeoutMs = 5000);
    }
}
