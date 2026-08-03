using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Services;

/// <summary>
/// 优化服务实现：每个优化项执行 备份 → 应用 两阶段，
/// 结果与备份数据持久化到 optimization_records 并写入审计日志；
/// 回滚基于最近一次成功备份恢复。
/// </summary>
public sealed class OptimizationService(
    IDbContextFactory<WempDbContext> dbFactory,
    OptimizationActionFactory actionFactory,
    ISystemRestorePointService? systemRestorePoint = null) : IOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<OptimizationItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.OptimizationItems
            .OrderBy(i => i.Category)
            .ThenBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<OptimizationBatchResult> ApplyOneKeyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var items = await db.OptimizationItems
            .Where(i => i.Enabled)
            .OrderBy(i => i.Category)
            .ThenBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        var restorePointId = await CreateRestorePointAsync("WEMP 一键优化", cancellationToken);
        return await ApplyBatchAsync(items, "one-key", restorePointId, cancellationToken);
    }

    public async Task<OptimizationBatchResult> ApplySelectedAsync(
        IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var codeSet = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = await db.OptimizationItems
            .Where(i => codeSet.Contains(i.Code))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        var restorePointId = await CreateRestorePointAsync("WEMP 选定优化", cancellationToken);
        return await ApplyBatchAsync(items, "selected", restorePointId, cancellationToken);
    }

    /// <summary>优化前创建系统还原点；失败（非管理员/未启用）时返回 null，不阻塞优化。</summary>
    private async Task<long?> CreateRestorePointAsync(string description, CancellationToken cancellationToken)
    {
        if (systemRestorePoint is null)
        {
            return null;
        }

        try
        {
            return await systemRestorePoint.CreateRestorePointAsync(description, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "系统还原点创建异常，本次优化不关联还原点");
            return null;
        }
    }

    public async Task<OptimizationBatchResult> RollbackAsync(
        IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        var codeSet = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<OptimizationResult>();
        foreach (var code in codeSet)
        {
            results.Add(await RollbackOneAsync(code, cancellationToken));
        }

        return new OptimizationBatchResult(results);
    }

    public async Task<OptimizationBatchResult> RollbackAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        // 找到每个 Code 最近一次成功 apply 的记录
        var latest = await db.OptimizationRecords
            .Where(r => r.Action == "apply" && r.Result == "success")
            .GroupBy(r => r.ItemCode)
            .Select(g => g.OrderByDescending(r => r.ExecutedAt).First().ItemCode)
            .ToListAsync(cancellationToken);

        return await RollbackAsync(latest, cancellationToken);
    }

    public async Task<OptimizationBatchResult> RollbackRecordAsync(
        long recordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var record = await db.OptimizationRecords
            .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record is null)
        {
            return new OptimizationBatchResult(
                [new OptimizationResult($"#{recordId}", $"#{recordId}", false, "记录不存在", 0, "rollback")]);
        }

        if (record.Action != "apply" || record.Result != "success" || string.IsNullOrEmpty(record.BeforeJson))
        {
            return new OptimizationBatchResult(
                [new OptimizationResult(record.ItemCode, record.ItemCode, false, "该记录不支持回滚", 0, "rollback")]);
        }

        var item = await db.OptimizationItems
            .FirstOrDefaultAsync(i => i.Code == record.ItemCode, cancellationToken);
        if (item is null)
        {
            return new OptimizationBatchResult(
                [new OptimizationResult(record.ItemCode, record.ItemCode, false, "知识库中不存在该条目", 0, "rollback")]);
        }

        // 恢复到该记录应用前的快照；审计 trigger 标记为 history 以区分手动回滚
        var result = await RestoreSnapshotAsync(item, record.BeforeJson, "history", cancellationToken);
        return new OptimizationBatchResult([result]);
    }

    public async Task<IReadOnlyList<OptimizationRecord>> GetHistoryAsync(
        int count, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.OptimizationRecords
            .OrderByDescending(r => r.ExecutedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    // ---- 内部实现 ----

    private async Task<OptimizationBatchResult> ApplyBatchAsync(
        IReadOnlyList<OptimizationItem> items, string trigger, long? restorePointId, CancellationToken cancellationToken)
    {
        var results = new List<OptimizationResult>();
        foreach (var item in items)
        {
            results.Add(await ApplyOneAsync(item, trigger, restorePointId, cancellationToken));
        }

        return new OptimizationBatchResult(results);
    }

    private async Task<OptimizationResult> ApplyOneAsync(
        OptimizationItem item, string trigger, long? restorePointId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var action = actionFactory.Get(item.Category);
        var target = OptimizationTarget.Parse(item.TargetJson)
            ?? throw new InvalidOperationException($"知识库条目缺少目标参数：{item.Code}");

        try
        {
            // 阶段 1：备份
            object? backup = null;
            if (item.IsRecoverable && action.SupportsBackup)
            {
                backup = await action.BackupAsync(target, cancellationToken);
            }

            // 阶段 2：应用
            var after = await action.ApplyAsync(target, backup, cancellationToken);

            await RecordAsync(item.Code, "apply", trigger, "success",
                backup, after, null, restorePointId, stopwatch.ElapsedMilliseconds, cancellationToken);
            Log.Information("优化项 {Item} 应用成功（{Trigger}）", item.Code, trigger);

            return new OptimizationResult(item.Code, item.Name, true, "成功", stopwatch.ElapsedMilliseconds, "apply");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RecordAsync(item.Code, "apply", trigger, "failed",
                null, null, ex.Message, restorePointId, stopwatch.ElapsedMilliseconds, cancellationToken);
            Log.Error(ex, "优化项 {Item} 应用失败（{Trigger}）", item.Code, trigger);

            return new OptimizationResult(item.Code, item.Name, false, ex.Message, stopwatch.ElapsedMilliseconds, "apply");
        }
    }

    private async Task<OptimizationResult> RollbackOneAsync(string code, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var item = await db.OptimizationItems.FirstOrDefaultAsync(i => i.Code == code, cancellationToken);
        if (item is null)
        {
            return new OptimizationResult(code, code, false, "知识库中不存在该条目", 0, "rollback");
        }

        var latest = await db.OptimizationRecords
            .Where(r => r.ItemCode == code && r.Action == "apply" && r.Result == "success")
            .OrderByDescending(r => r.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null || string.IsNullOrEmpty(latest.BeforeJson))
        {
            return new OptimizationResult(code, item.Name, false, "无可用备份，无法回滚", 0, "rollback");
        }

        return await RestoreSnapshotAsync(item, latest.BeforeJson, "manual", cancellationToken);
    }

    /// <summary>用指定快照 JSON 恢复优化项并记录回滚审计。</summary>
    private async Task<OptimizationResult> RestoreSnapshotAsync(
        OptimizationItem item, string beforeJson, string trigger, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var action = actionFactory.Get(item.Category);
        var target = OptimizationTarget.Parse(item.TargetJson)
            ?? throw new InvalidOperationException($"知识库条目缺少目标参数：{item.Code}");

        try
        {
            var backup = DeserializeBackup(item.Category, beforeJson);
            await action.RestoreAsync(target, backup, cancellationToken);

            await RecordAsync(item.Code, "rollback", trigger, "success",
                null, beforeJson, null, null, stopwatch.ElapsedMilliseconds, cancellationToken);
            Log.Information("优化项 {Item} 回滚成功（{Trigger}）", item.Code, trigger);

            return new OptimizationResult(item.Code, item.Name, true, "已恢复原始状态", stopwatch.ElapsedMilliseconds, "rollback");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RecordAsync(item.Code, "rollback", trigger, "failed",
                null, null, ex.Message, null, stopwatch.ElapsedMilliseconds, cancellationToken);
            Log.Error(ex, "优化项 {Item} 回滚失败（{Trigger}）", item.Code, trigger);

            return new OptimizationResult(item.Code, item.Name, false, ex.Message, stopwatch.ElapsedMilliseconds, "rollback");
        }
    }

    private async Task RecordAsync(
        string code,
        string actionName,
        string trigger,
        string result,
        object? before,
        object? after,
        string? detail,
        long? restorePointId,
        long durationMs,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.OptimizationRecords.Add(new OptimizationRecord
        {
            ItemCode = code,
            Action = actionName,
            Trigger = trigger,
            Result = result,
            BeforeJson = Serialize(before),
            AfterJson = Serialize(after),
            Detail = detail,
            DurationMs = durationMs,
            RestorePointId = restorePointId,
            ExecutedAt = DateTime.Now,
        });

        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            Module = "Optimization",
            Level = result == "success" ? "info" : "error",
            Action = $"optimize.{actionName}",
            Target = code,
            Message = detail,
            Result = result,
            DurationMs = durationMs,
            User = Environment.UserName,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static object? DeserializeBackup(string category, string json) =>
        category.ToLowerInvariant() switch
        {
            "registry" or "game" => JsonSerializer.Deserialize<RegistryBackup>(json, JsonOptions),
            "service" => JsonSerializer.Deserialize<List<ServiceBackup>>(json, JsonOptions),
            "startup" => JsonSerializer.Deserialize<List<StartupEntry>>(json, JsonOptions),
            "network" => JsonSerializer.Deserialize<List<NetworkAdapterState>>(json, JsonOptions),
            "disk" => JsonSerializer.Deserialize<DiskBackup>(json, JsonOptions),
            "power" => JsonSerializer.Deserialize<PowerBackup>(json, JsonOptions),
            "memory" => JsonSerializer.Deserialize<List<ProcessBackup>>(json, JsonOptions),
            "visual" => JsonSerializer.Deserialize<VisualBackup>(json, JsonOptions),
            "background" => JsonSerializer.Deserialize<BackgroundBackup>(json, JsonOptions),
            "gpu" => JsonSerializer.Deserialize<List<GpuPreferenceEntry>>(json, JsonOptions),
            "pagefile" => JsonSerializer.Deserialize<PagefileBackup>(json, JsonOptions),
            "hags" => JsonSerializer.Deserialize<HagBackup>(json, JsonOptions),
            "device" => JsonSerializer.Deserialize<DeviceBackup>(json, JsonOptions),
            "timer" => JsonSerializer.Deserialize<TimerBackup>(json, JsonOptions),
            "scheduled-task" => JsonSerializer.Deserialize<List<TaskBackup>>(json, JsonOptions),
            "windows-feature" => JsonSerializer.Deserialize<List<FeatureBackup>>(json, JsonOptions),
            _ => null,
        };
}
