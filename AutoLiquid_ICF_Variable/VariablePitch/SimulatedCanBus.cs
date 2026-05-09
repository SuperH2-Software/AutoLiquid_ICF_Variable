using System;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 模拟 CAN 总线实现（无真实硬件时用于调试/开发）
    /// 自动按协议构造合法应答帧
    /// </summary>
    public class SimulatedCanBus : ICanBus
    {
        public bool IsOpen { get; private set; }

        // 模拟设备内部状态
        private int _simulatedVolumeUl = 0;
        private byte _simulatedAspirateSpeed = (byte)VariablePitchSpeed.Fast;
        private byte _simulatedDispenseSpeed = (byte)VariablePitchSpeed.Medium;
        private byte _simulatedDeviceId = 0x01;
        private byte _simulatedPitchCode = 0x8C; // 14mm（原点）

        public bool Open() { IsOpen = true; return true; }
        public void Close() { IsOpen = false; }

        public bool Send(uint canId, byte[] data) => IsOpen;

        public async Task<byte[]> SendAndReceiveAsync(uint canId, byte[] data, int timeoutMs = 2000)
        {
            if (!IsOpen || data == null || data.Length < 1) return null;

            // 模拟 CAN 延迟
            await Task.Delay(Math.Min(50, timeoutMs / 4));

            byte func = data[0];
            var resp = new byte[8];
            resp[0] = 0x00;
            resp[1] = func;

            switch (func)
            {
                case VariablePitchFunctionCode.ReleaseTip:
                    // 无附加数据
                    break;

                case VariablePitchFunctionCode.SetPitch:
                    _simulatedPitchCode = data[1];
                    resp[2] = data[1];
                    break;

                case VariablePitchFunctionCode.SetPitchHalf:
                    _simulatedPitchCode = data[1];
                    resp[2] = data[1];
                    break;

                case VariablePitchFunctionCode.Aspirate:
                    {
                        int vol = (data[1] << 8) | data[2];
                        _simulatedVolumeUl += vol;
                        resp[2] = (byte)(_simulatedVolumeUl >> 8);
                        resp[3] = (byte)(_simulatedVolumeUl & 0xFF);
                        break;
                    }
                case VariablePitchFunctionCode.Dispense:
                    {
                        int vol = (data[1] << 8) | data[2];
                        _simulatedVolumeUl = Math.Max(0, _simulatedVolumeUl - vol);
                        resp[2] = (byte)(_simulatedVolumeUl >> 8);
                        resp[3] = (byte)(_simulatedVolumeUl & 0xFF);
                        break;
                    }
                case VariablePitchFunctionCode.SetSpeed:
                    _simulatedAspirateSpeed = data[1];
                    _simulatedDispenseSpeed = data[2];
                    break;

                case VariablePitchFunctionCode.ReadSpeed:
                    resp[2] = _simulatedAspirateSpeed;
                    resp[3] = _simulatedDispenseSpeed;
                    break;

                case VariablePitchFunctionCode.PistonHome:
                    _simulatedVolumeUl = 0;
                    break;

                case VariablePitchFunctionCode.ReadVolume:
                    resp[2] = (byte)(_simulatedVolumeUl >> 8);
                    resp[3] = (byte)(_simulatedVolumeUl & 0xFF);
                    break;

                case VariablePitchFunctionCode.ReadStatus:
                    // 模拟：在原点（14mm）
                    resp[2] = 0x02; // Bit1=AtOrigin14mm
                    resp[3] = 0x00;
                    break;

                case VariablePitchFunctionCode.ModifyId:
                    _simulatedDeviceId = data[1];
                    break;

                case VariablePitchFunctionCode.QueryId:
                    resp[2] = _simulatedDeviceId;
                    break;

                default:
                    return null; // 未知命令
            }

            return resp;
        }

        public void Dispose() { IsOpen = false; }
    }
}
