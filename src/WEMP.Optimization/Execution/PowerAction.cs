using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 电源计划优化执行器：切换高性能电源计划，并设置
/// USB 选择性暂停=禁用、PCI Express 链接状态电源管理=关闭、
/// 处理器最小/最大状态=100%。需管理员权限。
/// </summary>
public sealed partial class PowerAction : IOptimizationAction
{
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    // 电源子项 GUID（系统固定值）
    private const string SubGroupUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string SettingUsbSelectiveSuspend = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";
    private const string SubGroupPcie = "501a4d13-42af-4429-9fd1-a8218c268e20";
    private const string SettingPcieAspm = "ee12f906-d277-404b-b6da-e5fa1a576df5";
    private const string SubGroupProcessor = "54533251-82be-4824-96c1-47b60b740d00";
    private const string SettingProcessorMin = "893dee8e-2bef-41e0-89c6-b55d0929964c";
    private const string SettingProcessorMax = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    /// <summary>高性能模式下各子项的期望值。</summary>
    private static readonly (string SubGroup, string Setting, int Value)[] Tuning =
    [
        (SubGroupUsb, SettingUsbSelectiveSuspend, 0),      // USB 选择性暂停：禁用
        (SubGroupPcie, SettingPcieAspm, 0),                // PCIe 链接状态电源管理：关闭
        (SubGroupProcessor, SettingProcessorMin, 100),     // 最小处理器状态：100%
        (SubGroupProcessor, SettingProcessorMax, 100),     // 最大处理器状态：100%
    ];

    public string ItemType => "power";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync("powercfg.exe", "/getactivescheme", cancellationToken);
        var match = GuidRegex().Match(result.Output);
        var activeScheme = match.Success ? match.Groups[1].Value : "";

        // 备份当前方案各子项的 AC/DC 索引值
        var settings = new List<PowerSettingBackup>();
        foreach (var (subGroup, setting, _) in Tuning)
        {
            var query = await ProcessRunner.RunAsync(
                "powercfg.exe", $"/query {activeScheme} {subGroup} {setting}", cancellationToken);
            var values = IndexRegex().Matches(query.Output)
                .Select(m => m.Groups[1].Value)
                .Take(2)
                .Select(v => Convert.ToInt32(v, 16))
                .ToArray();
            if (values.Length == 2)
            {
                settings.Add(new PowerSettingBackup(subGroup, setting, values[0], values[1]));
            }
        }

        return new PowerBackup(activeScheme, settings);
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/setactive {HighPerformanceGuid}", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"切换电源计划失败（需管理员权限）：{result.Output.Trim()}");
        }

        // 应用子项设置（AC + DC 一致）
        foreach (var (subGroup, setting, value) in Tuning)
        {
            foreach (var cmd in new[] { "setacvalueindex", "setdcvalueindex" })
            {
                var r = await ProcessRunner.RunAsync(
                    "powercfg.exe", $"/{cmd} {HighPerformanceGuid} {subGroup} {setting} {value}", cancellationToken);
                if (!r.Success)
                {
                    throw new InvalidOperationException($"设置电源子项失败：{r.Output.Trim()}");
                }
            }
        }

        // 应用更改
        var apply = await ProcessRunner.RunAsync(
            "powercfg.exe", "/setactive SCHEME_CURRENT", cancellationToken);
        if (!apply.Success)
        {
            throw new InvalidOperationException($"应用电源设置失败：{apply.Output.Trim()}");
        }

        return null;
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not PowerBackup b || string.IsNullOrEmpty(b.ActiveScheme))
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        // 恢复原方案
        var result = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/setactive {b.ActiveScheme}", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"恢复电源计划失败：{result.Output.Trim()}");
        }

        // 恢复原方案子项值
        foreach (var setting in b.Settings)
        {
            foreach (var (cmd, value) in new[]
                     {
                         ("setacvalueindex", setting.AcValue),
                         ("setdcvalueindex", setting.DcValue),
                     })
            {
                var r = await ProcessRunner.RunAsync(
                    "powercfg.exe", $"/{cmd} {b.ActiveScheme} {setting.SubGroup} {setting.Setting} {value}", cancellationToken);
                if (!r.Success)
                {
                    throw new InvalidOperationException($"恢复电源子项失败：{r.Output.Trim()}");
                }
            }
        }

        var apply = await ProcessRunner.RunAsync(
            "powercfg.exe", "/setactive SCHEME_CURRENT", cancellationToken);
        if (!apply.Success)
        {
            throw new InvalidOperationException($"应用电源设置失败：{apply.Output.Trim()}");
        }
    }

    [GeneratedRegex(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"0x([0-9a-fA-F]{8})")]
    private static partial Regex IndexRegex();
}

/// <summary>电源计划备份数据。</summary>
public sealed record PowerBackup(string ActiveScheme, List<PowerSettingBackup> Settings);

/// <summary>电源子项 AC/DC 值备份。</summary>
public sealed record PowerSettingBackup(string SubGroup, string Setting, int AcValue, int DcValue);
