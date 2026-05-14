using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// ICanBus 日志装饰器：透明代理任意 ICanBus 实现，
    /// 在每次发送/接收时通过回调输出 16 进制原始数据。
    /// </summary>
    public sealed class LoggingCanBus : ICanBus
    {
        private readonly ICanBus _inner;
        private readonly Action<string> _log;

        /// <param name="inner">被装饰的真实/模拟 CAN 实现</param>
        /// <param name="log">日志回调（线程安全由调用方保证）</param>
        public LoggingCanBus(ICanBus inner, Action<string> log)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool IsOpen => _inner.IsOpen;
        public bool Open() => _inner.Open();
        public void Close() => _inner.Close();
        public void Dispose() => _inner.Dispose();

        public bool Send(uint canId, byte[] data)
        {
            _log($"→ TX  CAN_ID=0x{canId:X3}  [{ToHex(data)}]");
            return _inner.Send(canId, data);
        }

        public async Task<byte[]> SendAndReceiveAsync(uint canId, byte[] data, int timeoutMs = 5000)
        {
            _log($"→ TX  CAN_ID=0x{canId:X3}  [{ToHex(data)}]");

            byte[] resp = await _inner.SendAndReceiveAsync(canId, data, timeoutMs);

            if (resp != null)
                _log($"← RX  CAN_ID=0x{0:X3}      [{ToHex(resp)}]");
            else
                _log($"← RX  ⚠ 超时 / 无应答  (timeout={timeoutMs} ms)");

            return resp;
        }

        // ── 辅助 ────────────────────────────────────────────
        private static string ToHex(byte[] data)
            => data == null || data.Length == 0
               ? "(empty)"
               : string.Join(" ", data.Select(b => b.ToString("X2")));
    }
}