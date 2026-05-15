using AutoLiquid_ICF_Variable.VariablePitch;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AutoLiquid_ICF_Variable.Window
{
    /// <summary>
    /// 变距移液模块完整调试界面
    /// 支持真实CAN硬件（实现ICanBus后注入）和模拟模式（SimulatedCanBus）
    /// </summary>
    public partial class WindowVariablePitchDebug : MetroWindow
    {
        // ── 字段 ─────────────────────────────────────────────
        private VariablePitchController _ctrl;
        private DispatcherTimer _autoRefreshTimer;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        // 状态灯颜色
        private static readonly SolidColorBrush BrushActive = new SolidColorBrush(Colors.LimeGreen);
        private static readonly SolidColorBrush BrushInactive = new SolidColorBrush(Colors.Gray);
        private static readonly SolidColorBrush BrushConnected = new SolidColorBrush(Colors.LimeGreen);

        // ── 构造 ─────────────────────────────────────────────
        public WindowVariablePitchDebug()
        {
            InitializeComponent();

            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _autoRefreshTimer.Tick += async (s, e) =>
            {
                if (_ctrl?.IsConnected == true)
                    await RefreshStatusAsync();
            };

            this.Loaded += (s, e) => Utils.ViewUtils.ShowLogo(this);
        }

        // ════════════════════════════════════════════════════
        // 连接
        // ════════════════════════════════════════════════════

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!byte.TryParse(TxtDeviceId.Text.Trim(), out byte deviceId) || deviceId == 0)
            {
                AppendLog("❌ 设备 ID 无效（范围 1~255）");
                return;
            }

            ICanBus canBus;
            if (ChkSimulate.IsChecked == true)
            {
                canBus = new SimulatedCanBus();
            }
            else
            {
                try
                {
                    canBus = CreateRealCanBus();
                    AppendLog("ℹ 使用真实CAN总线（ZlgCanBus / USB-CAN2）");
                }
                catch (NotImplementedException ex)
                {
                    AppendLog($"❌ {ex.Message}");
                    return;
                }
            }

            // ★ 套上日志装饰器，发送/接收自动打印 16 进制原始帧
            canBus = new LoggingCanBus(canBus, AppendLog);

            _ctrl?.Dispose();
            _ctrl = new VariablePitchController(canBus, deviceId);
            bool ok = _ctrl.Connect();

            if (ok)
            {
                SetConnectedState(true);
                AppendLog($"✅ 已连接  设备ID=0x{deviceId:X2}" +
                          $"  [{(ChkSimulate.IsChecked == true ? "模拟模式" : "真实CAN")}]");
            }
            else
            {
                AppendLog("❌ 连接失败，请检查：① USB-CAN是否插好  ② controlcan.dll是否在程序目录  ③ 设备索引是否正确");
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer.Stop();
            TglAutoRefresh.IsOn = false;
            _ctrl?.Disconnect();
            _ctrl?.Dispose();
            _ctrl = null;
            SetConnectedState(false);
            ResetStatusLights();
            AppendLog("🔌 已断开连接");
        }

        /// <summary>
        /// 创建真实 ZLG USB-CAN 适配器实例
        ///
        /// 默认：USB-CAN2，设备索引0，CAN通道0
        /// 如需多卡或单路USB-CAN，修改此处参数：
        ///   ZlgDeviceType.UsbCan  = USB-CAN 单路（DevType=3）
        ///   ZlgDeviceType.UsbCan2 = USB-CAN2双路（DevType=4）
        ///   deviceIndex = 同型第几个设备（0起）
        ///   canChannel  = CAN通道（0或1）
        /// </summary>
        private static ICanBus CreateRealCanBus()
        {
            return new ZlgCanBus(
                deviceType: ZlgDeviceType.UsbCan2,
                deviceIndex: 0,
                canChannel: 0);
        }

        // ════════════════��═══════════════════════════════════
        // 状态刷新
        // ════════════════════════════════════════════════════

        private void BtnRefreshStatus_Click(object sender, RoutedEventArgs e)
            => _ = RefreshStatusAsync();

        private void TglAutoRefresh_Toggled(object sender, RoutedEventArgs e)
        {
            if (TglAutoRefresh.IsOn)
                _autoRefreshTimer.Start();
            else
                _autoRefreshTimer.Stop();
        }

        private async System.Threading.Tasks.Task RefreshStatusAsync()
        {
            if (_ctrl == null) return;

            var status = await _ctrl.ReadStatusAsync();
            //var volume = await _ctrl.ReadVolumeAsync();

            Dispatcher.Invoke(() =>
            {
                if (status != null)
                {
                    EllReleasingTip.Fill = status.IsReleasingTip ? BrushActive : BrushInactive;
                    EllAtOrigin.Fill = status.IsAtOrigin14mm ? BrushActive : BrushInactive;
                    EllAspirating.Fill = status.IsAspirating ? BrushActive : BrushInactive;
                    EllDispensing.Fill = status.IsDispensing ? BrushActive : BrushInactive;
                    EllPistonHoming.Fill = status.IsPistonHoming ? BrushActive : BrushInactive;
                    EllCalibrating.Fill = status.IsCalibrating ? BrushActive : BrushInactive;
                }
                //LblCurrentVolume.Content = volume.HasValue ? $"{volume.Value} µL" : "—— µL";
            });
        }

        // ════════════════════════════════════════════════════
        // 基础操作
        // ════════════════════════════════════════════════════

        private async void BtnReleaseTip_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            using var _ = DisableButton(sender);
            AppendLog("▶ 松开枪头...");
            bool ok = await _ctrl.ReleaseTipAsync();
            AppendLog(ok ? "✅ 松开枪头 完成" : "❌ 松开枪头 失败");
            await RefreshStatusAsync();
        }

        private async void BtnPistonHome_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            using var _ = DisableButton(sender);
            AppendLog("▶ 活塞自检回零...");
            bool ok = await _ctrl.PistonHomeAsync();
            AppendLog(ok ? "✅ 活塞回零 完成" : "❌ 活塞回零 失败");
            await RefreshStatusAsync();
        }

        private async void BtnReadStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            AppendLog("▶ 读取状态...");
            var status = await _ctrl.ReadStatusAsync();
            if (status != null)
                AppendLog($"✅ 状态：{status}  " +
                          $"[RAW Data1=0x{status.DATA1:X2}  Data2=0x{status.DATA2:X2}]");
            else
                AppendLog("❌ 读取状态 失败");
            await RefreshStatusAsync();
        }

        private async void BtnReadVolume_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            AppendLog("▶ 读取液量...");
            var vol = await _ctrl.ReadVolumeAsync();
            if (vol.HasValue)
            {
                LblCurrentVolume.Content = $"{vol.Value} µL";
                AppendLog($"✅ 当前液量：{vol.Value} µL");
            }
            else
                AppendLog("❌ 读取液量 失败");
        }

        // ════════════════════════════════════════════════════
        // 变距控制
        // ════════════════════════════════════════════════════

        private async void BtnSetPitch1mm_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            if (CmbPitch1mm.SelectedItem == null) return;

            string rawText = (CmbPitch1mm.SelectedItem as System.Windows.Controls.ComboBoxItem)
                             ?.Content?.ToString()?.Trim() ?? "";
            if (!int.TryParse(rawText, out int mm))
            {
                AppendLog("❌ 变距值无效");
                return;
            }
            using var _ = DisableButton(sender);
            AppendLog($"▶ 整数变距 → {mm} mm...");
            bool ok = await _ctrl.SetPitchAsync(mm);
            AppendLog(ok ? $"✅ 变距 {mm} mm 成功" : $"❌ 变距 {mm} mm 失败");
        }

        private async void BtnSetPitchHalf_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            if (!double.TryParse(TxtPitchHalf.Text.Trim(), out double pitch))
            {
                AppendLog("❌ 变距值无效（示例：9.5）");
                return;
            }
            using var _ = DisableButton(sender);
            AppendLog($"▶ 步进变距 → {pitch:F1} mm...");
            bool ok = await _ctrl.SetPitchHalfStepAsync(pitch);
            AppendLog(ok ? $"✅ 变距 {pitch:F1} mm 成功" : $"❌ 变距 {pitch:F1} mm 失败");
        }

        // ════════════════════════════════════════════════════
        // 吸排液
        // ════════════════════════════════════════════════════

        private async void BtnAspirate_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            if (!int.TryParse(TxtVolume.Text.Trim(), out int vol) || vol <= 0)
            {
                AppendLog("❌ 体积值无效（必须大于 0）");
                return;
            }
            using var _ = DisableButton(sender);
            AppendLog($"▶ 吸液 {vol} µL...");
            var remain = await _ctrl.AspirateAsync(vol);
            if (remain.HasValue)
            {
                LblCurrentVolume.Content = $"{remain.Value} µL";
                AppendLog($"✅ 吸液完成，剩余：{remain.Value} µL");
            }
            else
                AppendLog("❌ 吸液 失败");
        }

        private async void BtnDispense_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            if (!int.TryParse(TxtVolume.Text.Trim(), out int vol) || vol <= 0)
            {
                AppendLog("❌ 体积值无效（必须大于 0）");
                return;
            }
            using var _ = DisableButton(sender);
            AppendLog($"▶ 排液 {vol} µL...");
            var remain = await _ctrl.DispenseAsync(vol);
            if (remain.HasValue)
            {
                LblCurrentVolume.Content = $"{remain.Value} µL";
                AppendLog($"✅ 排液完成，剩余：{remain.Value} µL");
            }
            else
                AppendLog("❌ 排液 失败");
        }

        // ════════════════════════════════════════════════════
        // 速度
        // ════════════════════════════════════════════════════

        private async void BtnSetSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            var asp = (VariablePitchSpeed)(CmbAspSpeed.SelectedIndex + 1);
            var dis = (VariablePitchSpeed)(CmbDisSpeed.SelectedIndex + 1);
            using var _ = DisableButton(sender);
            AppendLog($"▶ 设置速度：吸液={asp} (0x{(byte)asp:X2})，排液={dis} (0x{(byte)dis:X2})...");
            bool ok = await _ctrl.SetSpeedAsync(asp, dis);
            AppendLog(ok ? "✅ 设置速度 成功" : "❌ 设置速度 失败");
        }

        private async void BtnReadSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;
            AppendLog("▶ 读取速度...");
            var result = await _ctrl.ReadSpeedAsync();
            if (result.HasValue)
            {
                int aspIdx = (int)result.Value.Aspirate - 1;
                int disIdx = (int)result.Value.Dispense - 1;
                if (aspIdx >= 0 && aspIdx < CmbAspSpeed.Items.Count) CmbAspSpeed.SelectedIndex = aspIdx;
                if (disIdx >= 0 && disIdx < CmbDisSpeed.Items.Count) CmbDisSpeed.SelectedIndex = disIdx;
                AppendLog($"✅ 速度读取成功：吸液={result.Value.Aspirate}，排液={result.Value.Dispense}");
            }
            else
                AppendLog("❌ 读取速度 失败");
        }

        // ════════════════════════════════════════════════════
        // 日志
        // ════════════════════════════════════════════════════

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logBuilder.Clear();
            TxtLog.Text = "";
        }

        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}]  {message}");
                TxtLog.Text = _logBuilder.ToString();
                LogScroll.ScrollToBottom();
            });
        }

        // ════════════════════════════════════════════════════
        // 窗口关闭
        // ════════════════════════════════════════════════════

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _autoRefreshTimer?.Stop();
            _ctrl?.Disconnect();
            _ctrl?.Dispose();
        }

        // ════════════════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════════════════

        private void SetConnectedState(bool connected)
        {
            BtnConnect.IsEnabled = !connected;
            BtnDisconnect.IsEnabled = connected;
            EllipseStatus.Fill = connected ? BrushConnected : BrushInactive;
            LblConnStatus.Content = connected ? "已连接" : "未连接";
            LblConnStatus.Foreground = connected
                ? new SolidColorBrush(Colors.Green)
                : new SolidColorBrush(Colors.Gray);
        }

        private void ResetStatusLights()
        {
            EllReleasingTip.Fill = BrushInactive;
            EllAtOrigin.Fill = BrushInactive;
            EllAspirating.Fill = BrushInactive;
            EllDispensing.Fill = BrushInactive;
            EllPistonHoming.Fill = BrushInactive;
            EllCalibrating.Fill = BrushInactive;
            LblCurrentVolume.Content = "—— µL";
        }

        private bool EnsureConnected()
        {
            if (_ctrl?.IsConnected == true) return true;
            AppendLog("⚠ 尚未连接，请先点击\"连接\"");
            return false;
        }

        /// <summary>临时禁用按钮防止重复点击，using 块结束后自动恢复</summary>
        private static IDisposable DisableButton(object sender)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                btn.IsEnabled = false;
                return new ActionDisposable(() => btn.IsEnabled = true);
            }
            return new ActionDisposable(() => { });
        }

        private sealed class ActionDisposable : IDisposable
        {
            private readonly Action _action;
            public ActionDisposable(Action action) => _action = action;
            public void Dispose() => _action?.Invoke();
        }
    }
}
