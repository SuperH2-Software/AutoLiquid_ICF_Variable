using AutoLiquid_ICF_Variable.EntityJson;
using AutoLiquid_ICF_Variable.Utils;
using System;
using System.Threading.Tasks;

namespace AutoLiquid_ICF_Variable.VariablePitch
{
    /// <summary>
    /// 变距移液模块全局管理器（静态单例）
    ///
    /// 在 App 启动时初始化：
    ///   VariablePitchManager.Initialize(new YourRealCanBusImpl());
    ///   // 无硬件时使用模拟：
    ///   VariablePitchManager.Initialize(new SimulatedCanBus());
    ///
    /// 在 CmdHelper / TipHelper 中按需调用：
    ///   if (head.IsVariable)
    ///       await VariablePitchManager.SetPitchIfVariableAsync(head, 9.0);
    /// </summary>
    public static class VariablePitchManager
    {
        private static VariablePitchController _controller;

        /// <summary>当前控制器实例（未初始化时为 null）</summary>
        public static VariablePitchController Controller => _controller;

        /// <summary>是否已初始化且连接</summary>
        public static bool IsReady => _controller?.IsConnected == true;

        private static Head _head = ParamsHelper.HeadList[0];

        // ── 生命周期 ─────────────────────────────────────────

        /// <summary>
        /// 初始化管理器（应用启动时调用一次）
        /// </summary>
        /// <param name="canBus">CAN 总线实现（真实硬件或 SimulatedCanBus）</param>
        /// <param name="deviceId">设备 CAN ID，默认 0x01</param>
        public static void Initialize(ICanBus canBus, uint deviceId = 0x01)
        {
            _controller?.Dispose();
            _controller = new VariablePitchController(canBus, deviceId);
        }

        /// <summary>释放资源（应用关闭时调用）</summary>
        public static void Release()
        {
            _controller?.Disconnect();
            _controller?.Dispose();
            _controller = null;
        }

        // ── CmdHelper 集成辅助方法 ────────────────────────────
        // 以下方法封装 IsVariable 判断，IsVariable=false 直接返回 true/null
        // 让调用方原有 P 轴逻辑保持不变

        /// <summary>
        /// 若 head.IsVariable 为 true，执行 CAN 变距
        /// </summary>
        /// <param name="head">移液头配置</param>
        /// <param name="targetPitchMm">目标间距(mm)；null 则使用 head.ChannelStep</param>
        public static async Task<bool> SetPitchAsync(double targetPitchMm)
        {
            if (!_head.IsVariable) return true;
            EnsureReady();
            return await _controller.SetPitchHalfStepAsync(targetPitchMm);
        }

        /// <summary>
        /// 若 head.IsVariable 为 true，通过 CAN 吸液；
        /// IsVariable=false 返回 null，调用方走传统 P 轴
        /// </summary>
        /// <returns>剩余体积 µL；不变距或失败返回 null</returns>
        public static async Task<double?> AspirateAsync(double volumeUl)
        {
            if (!_head.IsVariable) return null;
            EnsureReady();
            return await _controller.AspirateAsync(volumeUl);
        }

        /// <summary>
        /// 若 head.IsVariable 为 true，通过 CAN 排液；
        /// IsVariable=false 返回 null，调用方走传统 P 轴
        /// </summary>
        /// <returns>剩余体积 µL；不变距或失败返回 null</returns>
        public static async Task<double?> DispenseAsync(double volumeUl)
        {
            if (!_head.IsVariable) return null;
            EnsureReady();
            return await _controller.DispenseAsync(volumeUl);
        }

        /// <summary>
        /// 若 head.IsVariable 为 true，执行活塞回零（初始化/复位时调用）
        /// </summary>
        public static async Task<bool> PistonHomeAsync(int timeoutMs = 15000)
        {
            if (!_head.IsVariable) return true;
            EnsureReady();
            return await _controller.PistonHomeAsync(timeoutMs);
        }

        /// <summary>
        /// 若 head.IsVariable 为 true，执行松开枪头（退枪头时调用）
        /// </summary>
        public static async Task<bool> ReleaseTipAsync(int timeoutMs = 5000)
        {
            if (!_head.IsVariable) return true;
            EnsureReady();
            return await _controller.ReleaseTipAsync(timeoutMs);
        }

        /// <summary>
        /// 若 head.IsVariable 为 true，设置吸排液速度
        /// </summary>
        public static async Task<bool> SetSpeedAsync(VariablePitchSpeed aspirateSpeed, VariablePitchSpeed dispenseSpeed, int timeoutMs = 2000)
        {
            if (!_head.IsVariable) return true;
            EnsureReady();
            return await _controller.SetSpeedAsync(aspirateSpeed, dispenseSpeed, timeoutMs);
        }

        /// <summary>变距（供 CmdHelper 直接调用）</summary>
        public static bool SetPitch(double targetPitchMm)
            => Task.Run(() => SetPitchAsync(targetPitchMm)).GetAwaiter().GetResult();

        /// <summary>吸液（供 CmdHelper 直接调用）</summary>
        public static double? Aspirate(double volumeUl)
            => Task.Run(() => AspirateAsync( volumeUl)).GetAwaiter().GetResult();

        /// <summary>排液（供 CmdHelper 直接调用）</summary>
        public static double? Dispense(double volumeUl)
            => Task.Run(() => DispenseAsync(volumeUl)).GetAwaiter().GetResult();

        /// <summary>活塞回零（供 CmdHelper 直接调用）</summary>
        public static bool PistonHome(int timeoutMs = 15000)
            => Task.Run(() => PistonHomeAsync(timeoutMs)).GetAwaiter().GetResult();

        /// <summary>松开枪头（供 CmdHelper 直接调用）</summary>
        public static bool ReleaseTip(int timeoutMs = 5000)
            => Task.Run(() => ReleaseTipAsync(timeoutMs)).GetAwaiter().GetResult();

        /// <summary>设置吸排液速度（供 CmdHelper 直接调用）</summary>
        public static bool SetSpeed(VariablePitchSpeed aspirateSpeed, VariablePitchSpeed dispenseSpeed, int timeoutMs = 2000)
            => Task.Run(() => SetSpeedAsync(aspirateSpeed, dispenseSpeed, timeoutMs)).GetAwaiter().GetResult();

        // ── 私有辅助 ──────────────────────────────────────────
        private static void EnsureReady()
        {
            if (_controller == null)
                throw new InvalidOperationException(
                    "VariablePitchManager 尚未初始化，请先调用 Initialize()");
            if (!_controller.IsConnected)
                throw new InvalidOperationException("变距模块 CAN 总线未连接");
        }
    }
}
