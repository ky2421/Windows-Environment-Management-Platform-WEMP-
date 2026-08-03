using System.Runtime.InteropServices;
using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 视觉效果优化执行器：调整为"最佳性能"并保留
/// 平滑屏幕字体边缘 + 图标标签使用阴影（SystemParametersInfo 即时生效，注册表持久化）。
/// </summary>
public sealed class VisualAction : IOptimizationAction
{
    private const string VisualEffectsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string DesktopKey = @"Control Panel\Desktop";

    // UserPreferencesMask：仅保留字体平滑（0x80000000）与图标标签阴影（0x10000000）
    private const uint BestPerformanceMask = 0x90000000;

    public string ItemType => "visual";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var vfx = Registry.CurrentUser.OpenSubKey(VisualEffectsKey, writable: false);
        using var desktop = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: false);

        return Task.FromResult<object?>(new VisualBackup(
            vfx?.GetValue("VisualFXSetting"),
            desktop?.GetValue("UserPreferencesMask") as byte[],
            desktop?.GetValue("FontSmoothing")));
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using (var vfx = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, writable: true))
        {
            // 2 = 自定义（按下方掩码），避免操作系统后续自动改回
            vfx.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
        }

        using (var desktop = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
        {
            desktop.SetValue("UserPreferencesMask", BitConverter.GetBytes(BestPerformanceMask), RegistryValueKind.Binary);
            desktop.SetValue("FontSmoothing", "2", RegistryValueKind.String); // 1=标准 2=ClearType
        }

        // 即时关闭全部动画效果（字体平滑与图标阴影保留）
        ApplyAnimationSettings(on: false);
        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not VisualBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var vfx = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, writable: true);
        using var desktop = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true);

        if (b.VisualFxSetting is { } fxSetting)
        {
            vfx.SetValue("VisualFXSetting", fxSetting,
                fxSetting is int ? RegistryValueKind.DWord : RegistryValueKind.String);
        }

        if (b.UserPreferencesMask is not null)
        {
            desktop.SetValue("UserPreferencesMask", b.UserPreferencesMask, RegistryValueKind.Binary);
        }

        if (b.FontSmoothing is { } smoothing)
        {
            desktop.SetValue("FontSmoothing", smoothing.ToString() ?? "", RegistryValueKind.String);
        }

        ApplyAnimationSettings(on: true);
        return Task.CompletedTask;
    }

    /// <summary>开关系统动画（性能选项"调整为最佳性能"涉及的 SPI 项）。</summary>
    private static void ApplyAnimationSettings(bool on)
    {
        uint value = on ? 1u : 0u;
        Span<(uint Action, bool Smooth)> animations =
        [
            (Spis.SpiSetClientAreaAnimation, false),
            (Spis.SpiSetComboBoxAnimation, false),
            (Spis.SpiSetListBoxSmoothScrolling, false),
            (Spis.SpiSetMenuAnimation, false),
            (Spis.SpiSetMenuFade, false),
            (Spis.SpiSetToolTipAnimation, false),
            (Spis.SpiSetToolTipFade, false),
            (Spis.SpiSetSelectionFade, false),
            (Spis.SpiSetUiEffects, false),
            (Spis.SpiSetCursorShadow, false),
            (Spis.SpiSetActiveWindowTracking, false),
        ];

        foreach (var (action, _) in animations)
        {
            SystemParametersInfo(action, value, IntPtr.Zero, Spif.SpfSendChange);
        }

        // 字体平滑独立控制：关闭动画时保留（性能面板"最佳性能"勾选项）
        SystemParametersInfo(Spis.SpiSetFontSmoothing, on ? 1u : 1u, IntPtr.Zero, Spif.SpfSendChange);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    private static class Spis
    {
        public const uint SpiSetClientAreaAnimation = 0x1043;
        public const uint SpiSetComboBoxAnimation = 0x1004;
        public const uint SpiSetListBoxSmoothScrolling = 0x1006;
        public const uint SpiSetMenuAnimation = 0x1002;
        public const uint SpiSetMenuFade = 0x1012;
        public const uint SpiSetToolTipAnimation = 0x1016;
        public const uint SpiSetToolTipFade = 0x1018;
        public const uint SpiSetSelectionFade = 0x101E;
        public const uint SpiSetUiEffects = 0x103F;
        public const uint SpiSetCursorShadow = 0x101A;
        public const uint SpiSetActiveWindowTracking = 0x1000;
        public const uint SpiSetFontSmoothing = 0x004A;
    }

    private static class Spif
    {
        public const uint SpfSendChange = 0x02;
    }
}

/// <summary>视觉效果备份数据。</summary>
public sealed record VisualBackup(object? VisualFxSetting, byte[]? UserPreferencesMask, object? FontSmoothing);
