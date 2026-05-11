using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 变距移液模块核心控制器
    ///
    /// 协议规格：
    ///   接口：CAN，波特率：500K，标准帧，数据帧，16进制
    ///   设备默认 ID：0x01（可通过 0x88 修改）
    ///   发送：CAN_ID=设备ID，DATA[0]=功能代码，DATA[1-7]=参数（不足补0）
    ///   应答：CAN_ID=0x00，DATA[0]=0x00，DATA[1]=功能代码回显，DATA[2-7]=返回数据
    /// </summary>
    public class VariablePitchController : IDisposable
    {
        // ── 变距范围常量 ─────────────────────────────────
        /// <summary>最小间距 4.5mm</summary>
        public const double PitchMinMm = 4.5;
        /// <summary>最大间距 14mm（原点）</summary>
        public const double PitchMaxMm = 14.0;
        /// <summary>半步变距单位：0.1mm</summary>
        private const double HalfStepUnit = 0.1;

        private readonly ICanBus _canBus;
        private uint _deviceId;

        /// <summary>当前设备 CAN ID</summary>
        public uint DeviceId => _deviceId;
        /// <summary>CAN 总线是否已连接</summary>
        public bool IsConnected => _canBus?.IsOpen == true;

        /// <param name="canBus">CAN 总线实现（真实硬件或模拟）</param>
        /// <param name="deviceId">设备 ID，默认 0x01</param>
        public VariablePitchController(ICanBus canBus, uint deviceId = 0x01)
        {
            _canBus = canBus ?? throw new ArgumentNullException(nameof(canBus));
            _deviceId = deviceId;
        }

        // ══════════════════════════════════════════════════
        // 连接管理
        // ══════════════════════════════════════════════════

        public bool Connect() => _canBus.Open();
        public void Disconnect() => _canBus.Close();

        // ══════════════════════════════════════════════════
        // 0x01 松开枪头
        // 发送：01 00 00 00 00 00 00 00
        // 应答：00 01 00 00 00 00 00 00
        // ══════════════════════════════════════════════════

        public async Task<bool> ReleaseTipAsync(int timeoutMs = 5000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.ReleaseTip, timeoutMs);
            return IsOk(resp, VariablePitchFunctionCode.ReleaseTip);
        }

        // ══════════════════════════════════════════════════
        // 0x02 变距（1mm步进，9~14mm）
        // 发送：02 XX 00 00 00 00 00 00
        //       XX：09=9mm 0A=10mm 0B=11mm 0C=12mm 0D=13mm 0E=14mm
        // 应答：00 02 XX 00 00 00 00 00
        // ══════════════════════════════════════════════════

        /// <param name="pitchMm">目标间距（mm），整数 9~14</param>
        public async Task<bool> SetPitchAsync(int pitchMm, int timeoutMs = 5000)
        {
            if (pitchMm < 9 || pitchMm > 14)
                throw new ArgumentOutOfRangeException(nameof(pitchMm), "变距范围为 9~14 mm（整数）");

            byte[] resp = await SendAsync(VariablePitchFunctionCode.SetPitch, timeoutMs, (byte)pitchMm);
            return IsOk(resp, VariablePitchFunctionCode.SetPitch);
        }

        // ══════════════════════════════════════════════════
        // 0x03 0.5mm 步进变距（9.0~14.0mm）
        // 发送：03 XX 00 00 00 00 00 00
        //       XX = round(pitchMm * 10)，单位 0.1mm
        //       示例：9.5mm → 0x5F(95)
        // 应答：00 03 XX 00 00 00 00 00
        // ══════════════════════════════════════════════════

        /// <param name="pitchMm">目标间距（mm），0.5mm步进，范围 9.0~14.0</param>
        public async Task<bool> SetPitchHalfStepAsync(double pitchMm, int timeoutMs = 5000)
        {
            if (pitchMm < PitchMinMm || pitchMm > PitchMaxMm)
                throw new ArgumentOutOfRangeException(nameof(pitchMm), "变距范围为 9.0~14.0 mm");

            // 对齐到 0.5mm 步长
            double aligned = Math.Round(pitchMm * 2.0, MidpointRounding.AwayFromZero) / 2.0;
            byte code = (byte)Math.Round(aligned / HalfStepUnit);  // 单位 0.1mm

            byte[] resp = await SendAsync(VariablePitchFunctionCode.SetPitchHalf, timeoutMs, code);
            return IsOk(resp, VariablePitchFunctionCode.SetPitchHalf);
        }

        // ══════════════════════════════════════════════════
        // 0x04 吸液
        // 发送：04 HH LL 00 00 00 00 00（体积大端16位，µL）
        // 应答：00 04 HH LL 00 00 00 00（剩余体积）
        // ══════════════════════════════════════════════════

        /// <param name="volumeUl">吸液体积（µL）</param>
        /// <returns>吸液后剩余体积（µL）；失败返回 null</returns>
        public async Task<int?> AspirateAsync(int volumeUl, int timeoutMs = 10000)
        {
            if (volumeUl <= 0)
                throw new ArgumentOutOfRangeException(nameof(volumeUl), "吸液体积必须大于 0");

            byte[] resp = await SendAsync(VariablePitchFunctionCode.Aspirate, timeoutMs,
                (byte)(volumeUl >> 8), (byte)(volumeUl & 0xFF));

            if (!IsOk(resp, VariablePitchFunctionCode.Aspirate)) return null;
            return (resp[2] << 8) | resp[3];
        }

        // ══════════════════════════════════════════════════
        // 0x05 排液
        // 发送：05 HH LL 00 00 00 00 00（体积大端16位，µL）
        // 应答：00 05 HH LL 00 00 00 00（剩余体积）
        // ══════════════════════════════════════════════════

        /// <param name="volumeUl">排液体积（µL）</param>
        /// <returns>排液后剩余体积（µL）；失败返回 null</returns>
        public async Task<int?> DispenseAsync(int volumeUl, int timeoutMs = 10000)
        {
            if (volumeUl <= 0)
                throw new ArgumentOutOfRangeException(nameof(volumeUl), "排液体积必须大于 0");

            byte[] resp = await SendAsync(VariablePitchFunctionCode.Dispense, timeoutMs,
                (byte)(volumeUl >> 8), (byte)(volumeUl & 0xFF));

            if (!IsOk(resp, VariablePitchFunctionCode.Dispense)) return null;
            return (resp[2] << 8) | resp[3];
        }

        // ══════════════════════════════════════════════════
        // 0x06 设置吸排液速度
        // 发送：06 AS DS 00 00 00 00 00（AS=吸速，DS=排速）
        // 应答：00 06 00 00 00 00 00 00
        // 速度：01=慢 02=中 03=快 04=最快
        // ══════════════════════════════════════════════════

        public async Task<bool> SetSpeedAsync(
            VariablePitchSpeed aspirateSpeed,
            VariablePitchSpeed dispenseSpeed,
            int timeoutMs = 2000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.SetSpeed, timeoutMs,
                (byte)aspirateSpeed, (byte)dispenseSpeed);
            return IsOk(resp, VariablePitchFunctionCode.SetSpeed);
        }

        // ══════════════════════════════════════════════════
        // 0x07 读取吸排液速度
        // 发送：07 00 00 00 00 00 00 00
        // 应答：00 07 AS DS 00 00 00 00
        // ══════════════════════════════════════════════════

        /// <returns>(吸液速度, 排液速度)；失败返回 null</returns>
        public async Task<(VariablePitchSpeed Aspirate, VariablePitchSpeed Dispense)?> ReadSpeedAsync(
            int timeoutMs = 2000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.ReadSpeed, timeoutMs);
            if (!IsOk(resp, VariablePitchFunctionCode.ReadSpeed)) return null;
            return ((VariablePitchSpeed)resp[2], (VariablePitchSpeed)resp[3]);
        }

        // ══════════════════════════════════════════════════
        // 0x08 活塞自检回零
        // 发送：08 00 00 00 00 00 00 00
        // 应答：00 08 00 00 00 00 00 00
        // ══════════════════════════════════════════════════

        public async Task<bool> PistonHomeAsync(int timeoutMs = 15000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.PistonHome, timeoutMs);
            return IsOk(resp, VariablePitchFunctionCode.PistonHome);
        }

        // ══════════════════════════════════════════════════
        // 0x09 读取吸液排液量（µL）
        // 发送：09 00 00 00 00 00 00 00
        // 应答：00 09 HH LL 00 00 00 00
        // ══════════════════════════════════════════════════

        /// <returns>当前液量（µL）；失败返回 null</returns>
        public async Task<int?> ReadVolumeAsync(int timeoutMs = 2000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.ReadVolume, timeoutMs);
            if (!IsOk(resp, VariablePitchFunctionCode.ReadVolume)) return null;
            return (resp[2] << 8) | resp[3];
        }

        // ══════════════════════════════════════════════════
        // 0x0A 读取移液枪状态
        // 发送：0A 00 00 00 00 00 00 00
        // 应答：00 0A S1 S2 00 00 00 00
        //       S1(DATA2)：Bit0=松开枪头 Bit1=14MM间距 Bit2=吸液中 Bit3=排液中
        //       S2(DATA3)：Bit0=活塞自检中 Bit1=校准中
        // ══════════════════════════════════════════════════

        /// <returns>状态对象；失败返回 null</returns>
        public async Task<VariablePitchStatusData> ReadStatusAsync(int timeoutMs = 2000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.ReadStatus, timeoutMs);
            if (!IsOk(resp, VariablePitchFunctionCode.ReadStatus)) return null;
            return new VariablePitchStatusData(resp[2], resp[3]);
        }

        // ══════════════════════════════════════════════════
        // 0x88 修改产品 ID（1~255）
        // 发送：88 ID 00 00 00 00 00 00
        // 应答：00 88 00 00 00 00 00 00
        // ══════════════════════════════════════════════════

        public async Task<bool> ModifyDeviceIdAsync(byte newId, int timeoutMs = 2000)
        {
            if (newId == 0)
                throw new ArgumentOutOfRangeException(nameof(newId), "设备 ID 范围为 1~255");

            byte[] resp = await SendAsync(VariablePitchFunctionCode.ModifyId, timeoutMs, newId);
            if (!IsOk(resp, VariablePitchFunctionCode.ModifyId)) return false;
            _deviceId = newId;
            return true;
        }

        // ══════════════════════════════════════════════════
        // 0x89 查询产品 ID
        // 发送：89 00 00 00 00 00 00 00
        // 应答：00 89 ID 00 00 00 00 00
        // ══════════════════════════════════════════════════

        /// <returns>产品 ID（1~255）；失败返回 null</returns>
        public async Task<byte?> QueryDeviceIdAsync(int timeoutMs = 2000)
        {
            byte[] resp = await SendAsync(VariablePitchFunctionCode.QueryId, timeoutMs);
            if (!IsOk(resp, VariablePitchFunctionCode.QueryId)) return null;
            return resp[2];
        }

        // ══════════════════════════════════════════════════
        // 等待模块空闲
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 轮询状态直到模块空闲
        /// </summary>
        /// <param name="pollIntervalMs">轮询间隔（ms）</param>
        /// <param name="timeoutMs">最大等待时间（ms）</param>
        public async Task<bool> WaitIdleAsync(int pollIntervalMs = 200, int timeoutMs = 30000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                var status = await ReadStatusAsync();
                if (status == null) return false;
                if (!status.IsBusy) return true;
                await Task.Delay(pollIntervalMs);
            }
            return false;
        }

        // ══════════════════════════════════════════════════
        // 私有辅助
        // ══════════════════════════════════════════════════

        /// <summary>构建 8 字节 CAN 发送帧</summary>
        private static byte[] BuildFrame(byte funcCode, params byte[] args)
        {
            var frame = new byte[8];
            frame[0] = funcCode;
            for (int i = 0; i < args.Length && i < 7; i++)
                frame[i + 1] = args[i];
            return frame;
        }

        /// <summary>发送并等待应答的统一入口</summary>
        private Task<byte[]> SendAsync(byte funcCode, int timeoutMs, params byte[] args)
            => _canBus.SendAndReceiveAsync(_deviceId, BuildFrame(funcCode, args), timeoutMs);

        /// <summary>校验应答帧：DATA[0]==0x00 且 DATA[1]==期望功能代码</summary>
        private static bool IsOk(byte[] resp, byte expectedFunc)
            => resp != null && resp.Length >= 2 && resp[0] == 0x00 && resp[1] == expectedFunc;

        public void Dispose() => _canBus?.Dispose();
    }
}
