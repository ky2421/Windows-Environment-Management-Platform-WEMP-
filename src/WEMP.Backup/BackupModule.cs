using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WEMP.Backup.Services;
using WEMP.Backup.UI;
using WEMP.Core.Abstractions;

namespace WEMP.Backup;

/// <summary>
/// 备份恢复模块：任务化全量/增量文件备份（glob 过滤、自动备份）、
/// 备份记录与文件条目管理、按记录还原。
/// </summary>
public sealed class BackupModule : IModule
{
    /// <summary>到期自动备份的检查周期。</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private IServiceProvider? _services;
    private Timer? _autoBackupTimer;
    private int _tickRunning;

    public string Name => "WEMP.Backup";

    public string DisplayName => "备份恢复";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "backup",
            "备份恢复",
            typeof(BackupPageViewModel),
            typeof(BackupPage),
            Order: 6),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        _services = services;
        _autoBackupTimer = new Timer(OnAutoBackupTick, null, CheckInterval, CheckInterval);
        Log.Information("备份模块：到期自动备份调度已启动（检查间隔 {Interval} 分钟）", CheckInterval.TotalMinutes);
        return Task.CompletedTask;
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _autoBackupTimer?.Dispose();
        _autoBackupTimer = null;
        return Task.CompletedTask;
    }

    private async void OnAutoBackupTick(object? state)
    {
        // 防重入：上一轮检查尚未结束（如大文件备份耗时）则跳过本轮
        if (Interlocked.Exchange(ref _tickRunning, 1) != 0)
        {
            return;
        }

        try
        {
            var services = _services;
            var backupService = services?.GetService<IBackupService>();
            if (backupService is null)
            {
                return;
            }

            var ran = await backupService.RunDueAutoBackupsAsync().ConfigureAwait(false);
            if (ran > 0)
            {
                Log.Information("到期自动备份完成：{Count} 个任务", ran);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "到期自动备份检查异常");
        }
        finally
        {
            Interlocked.Exchange(ref _tickRunning, 0);
        }
    }
}
