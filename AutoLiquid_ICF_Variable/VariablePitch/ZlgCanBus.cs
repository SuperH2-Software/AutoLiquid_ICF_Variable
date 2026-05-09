using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    // ══════════════════════════════════════════════════════════
    // 周立功 ZLG ControlCAN SDK 原生数据结构（安全托管版本）
    // 无需开启 AllowUnsafeBlocks，使用 MarshalAs 代替 fixed 数组
    // ══════════════════════════════════════════════════════════

    /// <summary>CAN 数据帧结构（对应 VCI_CAN_OBJ）</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VCI_CAN_OBJ
    {
        public uint ID;           // 帧ID（标准帧11位）
        public uint TimeStamp;    // 时间戳
        public byte TimeFlag;     // 是否使用时间戳
        public byte SendType;     // 发送类型（0=正常发送）
        public byte RemoteFlag;   // 远程帧标志（0=数据帧）
        public byte ExternFlag;   // 扩展帧标志（0=标准帧）
        public byte DataLen;      // 数据长度（0~8）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data;        // 数据字节
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Reserved;    // 保留

        /// <summary>创建一帧空白数据帧，自动初始化数组字段</summary>
        public static VCI_CAN_OBJ NewDataFrame()
        {
            return new VCI_CAN_OBJ
            {
                Data = new byte[8],
                Reserved = new byte[3],
                RemoteFlag = 0,  // 数据帧
                ExternFlag = 0,  // 标准帧
                SendType = 0   // 正常发送
            };
        }
    }

    /// <summary>CAN 初始化配置结构（对应 VCI_INIT_CONFIG）</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VCI_INIT_CONFIG
    {
        public uint AccCode;   // 验收码
        public uint AccMask;   // 屏蔽码
        public uint Reserved;
        public byte Filter;    // 0/1=接收所有帧，2=标准帧滤波，3=扩展帧滤波
        public byte Timing0;   // 波特率参数0
        public byte Timing1;   // 波特率参数1
        public byte Mode;      // 0=正常，1=只听，2=自测
    }

    // ══════════════════════════════════════════════════════════
    // ZLG ControlCAN.dll P/Invoke 声明
    // ══════════════════════════════════════════════════════════

    internal static class ControlCan
    {
        private const string DllName = "controlcan.dll";

        // 设备类型常量
        public const uint DEV_USBCAN = 3;  // USB-CAN（单路）
        public const uint DEV_USBCAN2 = 4;  // USB-CAN2（双路）

        // 返回值常量
        public const uint STATUS_OK = 1;
        public const uint STATUS_ERR = 0;

        [DllImport(DllName)] public static extern uint VCI_OpenDevice(uint DeviceType, uint DeviceInd, uint Reserved);
        [DllImport(DllName)] public static extern uint VCI_CloseDevice(uint DeviceType, uint DeviceInd);
        [DllImport(DllName)] public static extern uint VCI_InitCAN(uint DeviceType, uint DeviceInd, uint CANInd, ref VCI_INIT_CONFIG pInitConfig);
        [DllImport(DllName)] public static extern uint VCI_StartCAN(uint DeviceType, uint DeviceInd, uint CANInd);
        [DllImport(DllName)] public static extern uint VCI_ResetCAN(uint DeviceType, uint DeviceInd, uint CANInd);
        [DllImport(DllName)] public static extern uint VCI_ClearBuffer(uint DeviceType, uint DeviceInd, uint CANInd);

        /// <summary>发送 CAN 帧（Len=发送帧数量，通常传1）</summary>
        [DllImport(DllName)]
        public static extern uint VCI_Transmit(
            uint DeviceType, uint DeviceInd, uint CANInd,
            ref VCI_CAN_OBJ pSend, uint Len);

        /// <summary>接收 CAN 帧（WaitTime=-1 立即返回，>0 等待毫秒）</summary>
        [DllImport(DllName)]
        public static extern uint VCI_Receive(
            uint DeviceType, uint DeviceInd, uint CANInd,
            ref VCI_CAN_OBJ pReceive, uint Len, int WaitTime);

        [DllImport(DllName)]
        public static extern uint VCI_GetReceiveNum(
            uint DeviceType, uint DeviceInd, uint CANInd);
    }

    // ══════════════════════════════════════════════════════════
    // ZlgCanBus：实现 ICanBus 接口
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 周立功（ZLG）USB-CAN 适配器的 ICanBus 实现
    ///
    /// 支持型号：
    ///   USB-CAN  (DeviceType=3, 单路)
    ///   USB-CAN2 (DeviceType=4, 双路)
    ///
    /// 变距移液模块参数：500K 波特率、标准帧、数据帧
    /// ZLG 500K Timing: Timing0=0x00, Timing1=0x1C
    ///
    /// 使用方式：
    ///   var bus = new ZlgCanBus(ZlgDeviceType.UsbCan2, deviceIndex: 0, canChannel: 0);
    ///   VariablePitchManager.Initialize(bus, deviceId: 0x01);
    /// </summary>
    public class ZlgCanBus : ICanBus
    {
        // ── 配置 ─────────────────────────────────────────────
        private readonly uint _deviceType;   // DEV_USBCAN 或 DEV_USBCAN2
        private readonly uint _deviceIndex;  // 同类型设备中的序号（首个=0）
        private readonly uint _canChannel;   // CAN通道号（0或1）

        // ZLG 500K 波特率 Timing 参数（查手册 Table 2-1）
        private const byte Timing0_500K = 0x00;
        private const byte Timing1_500K = 0x1C;

        // ── 状态 ─────────────────────────────────────────────
        public bool IsOpen { get; private set; }

        // ── 接收队列（后台轮询线程 → 主线程） ─────────────────
        private readonly ConcurrentQueue<VCI_CAN_OBJ> _rxQueue
            = new ConcurrentQueue<VCI_CAN_OBJ>();
        private Thread _rxThread;
        private volatile bool _rxRunning;

        // 发送互斥锁，保证同一时刻只有一个 SendAndReceive 在执行
        private readonly SemaphoreSlim _txLock = new SemaphoreSlim(1, 1);

        // ── 构造 ─────────────────────────────────────────────

        /// <summary>
        /// 构造 ZLG USB-CAN 适配器实例
        /// </summary>
        /// <param name="deviceType">设备类型：<see cref="ZlgDeviceType"/></param>
        /// <param name="deviceIndex">同型设备序号（首个为 0）</param>
        /// <param name="canChannel">CAN 通道（0 或 1；USB-CAN 单路只有 0）</param>
        public ZlgCanBus(
            ZlgDeviceType deviceType = ZlgDeviceType.UsbCan2,
            uint deviceIndex = 0,
            uint canChannel = 0)
        {
            _deviceType = (uint)deviceType;
            _deviceIndex = deviceIndex;
            _canChannel = canChannel;
        }

        // ════════════════════════════════��═════════════════════
        // ICanBus.Open
        // ══════════════════════════════════════════════════════

        public bool Open()
        {
            if (IsOpen) return true;

            // 1. 打开设备
            if (ControlCan.VCI_OpenDevice(_deviceType, _deviceIndex, 0) != ControlCan.STATUS_OK)
                return false;

            // 2. 初始化 CAN 通道（500K，接收所有帧，正常模式）
            var config = new VCI_INIT_CONFIG
            {
                AccCode = 0x00000000,
                AccMask = 0xFFFFFFFF,
                Filter = 1,           // 接收所有帧
                Timing0 = Timing0_500K,
                Timing1 = Timing1_500K,
                Mode = 0            // 正常模式
            };

            if (ControlCan.VCI_InitCAN(_deviceType, _deviceIndex, _canChannel, ref config)
                != ControlCan.STATUS_OK)
            {
                ControlCan.VCI_CloseDevice(_deviceType, _deviceIndex);
                return false;
            }

            // 3. 清空缓冲区
            ControlCan.VCI_ClearBuffer(_deviceType, _deviceIndex, _canChannel);

            // 4. 启动 CAN
            if (ControlCan.VCI_StartCAN(_deviceType, _deviceIndex, _canChannel)
                != ControlCan.STATUS_OK)
            {
                ControlCan.VCI_CloseDevice(_deviceType, _deviceIndex);
                return false;
            }

            // 5. 启动后台接收线程
            _rxRunning = true;
            _rxThread = new Thread(RxThreadProc)
            {
                IsBackground = true,
                Name = "ZlgCanBus-RxThread"
            };
            _rxThread.Start();

            IsOpen = true;
            return true;
        }

        // ══════════════════════════════════════════════════════
        // ICanBus.Close
        // ══════════════════════════════════════════════════════

        public void Close()
        {
            if (!IsOpen) return;

            _rxRunning = false;
            _rxThread?.Join(500);

            ControlCan.VCI_ResetCAN(_deviceType, _deviceIndex, _canChannel);
            ControlCan.VCI_CloseDevice(_deviceType, _deviceIndex);

            // 清空队列
            while (_rxQueue.TryDequeue(out _)) { }

            IsOpen = false;
        }

        // ══════════════════════════════════════════════════════
        // ICanBus.Send
        // ══════════════════════════════════════════════════════

        public bool Send(uint canId, byte[] data)
        {
            if (!IsOpen || data == null || data.Length > 8) return false;

            var frame = BuildFrame(canId, data);
            return ControlCan.VCI_Transmit(
                _deviceType, _deviceIndex, _canChannel, ref frame, 1)
                == ControlCan.STATUS_OK;
        }

        // ══════════════════════════════════════════════════════
        // ICanBus.SendAndReceiveAsync
        // 发送 → 等待匹配应答（DATA[0]==0x00 && DATA[1]==funcCode）
        // ══════════════════════════════════════════════════════

        public async Task<byte[]> SendAndReceiveAsync(
            uint canId, byte[] data, int timeoutMs = 2000)
        {
            if (!IsOpen || data == null || data.Length < 1) return null;

            byte expectedFunc = data[0]; // 期望应答中回显的功能代码

            await _txLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // 清空队列中的残留旧帧
                while (_rxQueue.TryDequeue(out _)) { }

                // 发送请求帧
                if (!Send(canId, data)) return null;

                // 等待匹配应答
                var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
                while (DateTime.Now < deadline)
                {
                    if (_rxQueue.TryDequeue(out VCI_CAN_OBJ frame))
                    {
                        // 协议校验：DATA[0]==0x00 && DATA[1]==功能代码回显
                        if (frame.Data != null
                            && frame.Data.Length >= 2
                            && frame.Data[0] == 0x00
                            && frame.Data[1] == expectedFunc)
                        {
                            // 返回完整 8 字节应答数据
                            byte[] resp = new byte[8];
                            Array.Copy(frame.Data, resp, Math.Min(frame.Data.Length, 8));
                            return resp;
                        }
                        // 不匹配的帧丢弃（总线上可能有其他报文）
                    }
                    await Task.Delay(10).ConfigureAwait(false);
                }

                return null; // 超时
            }
            finally
            {
                _txLock.Release();
            }
        }

        // ══════════════════════════════════════════════════════
        // 后台接收线程：持续轮询 VCI_Receive，入队
        // ══════════════════════════════════════════════════════

        private void RxThreadProc()
        {
            var buf = VCI_CAN_OBJ.NewDataFrame();

            while (_rxRunning)
            {
                try
                {
                    uint count = ControlCan.VCI_Receive(
                        _deviceType, _deviceIndex, _canChannel,
                        ref buf, 1, 10 /* WaitTime=10ms */);

                    if (count > 0 && count != 0xFFFFFFFF)
                    {
                        // 深拷贝入队（buf 会被下一次 Receive 覆盖）
                        var copy = VCI_CAN_OBJ.NewDataFrame();
                        copy.ID = buf.ID;
                        copy.TimeStamp = buf.TimeStamp;
                        copy.TimeFlag = buf.TimeFlag;
                        copy.RemoteFlag = buf.RemoteFlag;
                        copy.ExternFlag = buf.ExternFlag;
                        copy.DataLen = buf.DataLen;
                        if (buf.Data != null)
                            Array.Copy(buf.Data, copy.Data, Math.Min(buf.Data.Length, 8));
                        _rxQueue.Enqueue(copy);
                    }
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 私有辅助
        // ══════════════════════════════════════════════════════

        /// <summary>构建标准数据帧（8字节，不足补0）</summary>
        private static VCI_CAN_OBJ BuildFrame(uint canId, byte[] data)
        {
            var frame = VCI_CAN_OBJ.NewDataFrame();
            frame.ID = canId;
            frame.DataLen = (byte)Math.Min(data.Length, 8);
            Array.Copy(data, frame.Data, frame.DataLen);
            return frame;
        }

        // ══════════════════════════════════════════════════════
        // IDisposable
        // ══════════════════════════════════════════════════════

        public void Dispose()
        {
            Close();
            _txLock?.Dispose();
        }
    }

    /// <summary>ZLG 设备类型枚举</summary>
    public enum ZlgDeviceType : uint
    {
        /// <summary>USB-CAN 单路适配器（DeviceType=3）</summary>
        UsbCan = 3,
        /// <summary>USB-CAN2 双路适配器（DeviceType=4）</summary>
        UsbCan2 = 4
    }
}
