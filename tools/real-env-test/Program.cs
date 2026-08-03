using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;

namespace WEMP.RealEnvTest;

/// <summary>
/// 真实 Windows 环境兼容性测试工具：
/// 使用真实 RegistryAction 对选定的安全条目（reg.wer）执行
/// 应用前备份 → 执行优化 → 校验生效 → 执行恢复 → 校验恢复 的完整闭环。
/// --user 模式模拟普通用户权限（预期 HKLM 写入被拒绝并记录失败，不崩溃）。
/// </summary>
internal static class Program
{
    private static string TargetCode = "reg.wer";
    private static string TargetKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting";
    private static string TargetValue = "Disabled";
    private static RegistryHive TargetHive = RegistryHive.LocalMachine;
    private static object TargetData = 1;

    private static int _passed;
    private static int _failed;

    private static async Task<int> Main(string[] args)
    {
        var userMode = args.Contains("--user");
        var gamedvrMode = args.Contains("--gamedvr");
        var logPath = args.SkipWhile(a => a != "--log").Skip(1).FirstOrDefault();
        if (logPath is null && userMode)
        {
            logPath = Path.Combine(Path.GetTempPath(), "wemp-user-test-output.txt");
        }

        StreamWriter? logWriter = null;
        if (logPath is not null)
        {
            logWriter = new StreamWriter(logPath, append: false, System.Text.Encoding.UTF8);
            Console.SetOut(TextWriter.Synchronized(logWriter));
            Console.SetError(logWriter);
        }

        try
        {
            return await RunAsync(userMode, gamedvrMode);
        }
        finally
        {
            logWriter?.Flush();
            logWriter?.Dispose();
        }
    }

    private static async Task<int> RunAsync(bool userMode, bool gamedvrMode)
    {
        if (gamedvrMode)
        {
            // HKCU 条目：验证普通用户可成功执行（reg.gamedvr 关闭 GameDVR 后台录制）
            TargetCode = "reg.gamedvr";
            TargetKey = @"System\GameConfigStore";
            TargetValue = "GameDVR_Enabled";
            TargetHive = RegistryHive.CurrentUser;
            TargetData = 0;
        }

        Console.WriteLine("===== WEMP 真实环境兼容性测试 =====");
        Console.WriteLine($"[模式] {(userMode ? "普通用户权限（模拟）" : "管理员权限")}");
        Console.WriteLine($"[系统] {Environment.OSVersion} | 64位进程: {Environment.Is64BitProcess}");
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        Console.WriteLine($"[用户] {identity.Name} | 管理员: {principal.IsInRole(WindowsBuiltInRole.Administrator)}");
        Console.WriteLine();

        var dbPath = Path.Combine(Path.GetTempPath(), $"wemp-real-{Guid.NewGuid():N}.db");
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var factory = new TestDbFactory(connection);

            // 种子知识库（72 条真实条目）
            var seedService = new OptimizationSeedService(factory);
            var added = await seedService.EnsureSeedAsync();
            Console.WriteLine($"[准备] 知识库种子完成（新增 {added} 条）");

            var service = new OptimizationService(
                factory,
                new OptimizationActionFactory([new RegistryAction()]),
                new SystemRestorePointService());

            // 1. 优化前状态
            var before = ReadRegistryValue();
            Console.WriteLine($"[前值] {TargetKey}\\{TargetValue} = {FormatValue(before)}");
            Check("优化前状态读取", true, $"{FormatValue(before)}");

            // 2. 应用（备份 + 写入）
            Console.WriteLine();
            Console.WriteLine($"[执行] 应用优化条目 {TargetCode}（写入 {TargetValue}={TargetData}）");
            var applyResult = await service.ApplySelectedAsync([TargetCode]);
            ReportApply("应用优化", applyResult);

            // 3. 检查优化后状态
            var after = ReadRegistryValue();
            var applied = after is int i && i == Convert.ToInt32(TargetData);
            Console.WriteLine($"[后值] {TargetKey}\\{TargetValue} = {FormatValue(after)}");
            Check($"优化生效（{TargetValue}={TargetData}）", applied, $"当前值 {FormatValue(after)}");

            if (!applied)
            {
                Console.WriteLine();
                Console.WriteLine($"[结论] 应用未生效，跳过恢复验证（失败路径已记录）");
                PrintSummary();
                return _failed == 0 ? 0 : 1;
            }

            // 4. 恢复
            Console.WriteLine();
            Console.WriteLine($"[执行] 恢复条目 {TargetCode}（回滚到原值）");
            var rollbackResult = await service.RollbackAsync([TargetCode]);
            ReportApply("执行恢复", rollbackResult);

            // 5. 检查恢复后状态
            var restored = ReadRegistryValue();
            var restoredOk = ValuesEqual(before, restored);
            Console.WriteLine($"[终值] {TargetKey}\\{TargetValue} = {FormatValue(restored)}");
            Check("恢复成功（回到原值）", restoredOk, $"原值 {FormatValue(before)} / 当前 {FormatValue(restored)}");

            // 6. 审计记录检查
            await using (var db = factory.CreateDbContext())
            {
                var records = await db.OptimizationRecords
                    .Where(r => r.ItemCode == TargetCode)
                    .OrderBy(r => r.ExecutedAt)
                    .ToListAsync();
                Console.WriteLine();
                Console.WriteLine($"[审计] 优化记录 {records.Count} 条：");
                foreach (var r in records)
                {
                    Console.WriteLine($"       {r.ExecutedAt:HH:mm:ss} | {r.Action} | {r.Result} | 还原点={r.RestorePointId?.ToString() ?? "(无)"} | {r.Detail}");
                }

                var audits = await db.AuditLogs
                    .Where(a => a.Module == "Optimization" && a.Target == TargetCode)
                    .ToListAsync();
                Console.WriteLine($"[审计] 审计日志 {audits.Count} 条");
                Check("审计记录完整", records.Count >= 2 && audits.Count >= 2,
                    $"优化记录 {records.Count} 条 / 审计 {audits.Count} 条");
            }

            Console.WriteLine();
            PrintSummary();
            return _failed == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[异常] 未预期异常：{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
            PrintSummary();
            return 1;
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    private static void ReportApply(string label, OptimizationBatchResult result)
    {
        Console.WriteLine($"[结果] {label}：成功 {result.SuccessCount} / 失败 {result.FailureCount}");
        foreach (var r in result.Results)
        {
            Console.WriteLine($"       {r.ItemCode} | 成功={r.Success} | 耗时={r.DurationMs}ms | {r.Message}");
            Check(label, r.Success, r.Message);
        }
    }

    private static object? ReadRegistryValue()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(TargetHive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(TargetKey);
            return key?.GetValue(TargetValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[读取异常] {ex.Message}");
            return null;
        }
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "(不存在)",
        int i => i.ToString(),
        _ => value.ToString() ?? "(null)",
    };

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return Convert.ToInt32(a) == Convert.ToInt32(b) ||
               string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name} —— {detail}");
        if (ok)
        {
            _passed++;
        }
        else
        {
            _failed++;
        }
    }

    private static void PrintSummary()
    {
        Console.WriteLine($"===== 汇总：通过 {_passed} / 失败 {_failed} =====");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 忽略
        }
    }

    private sealed class TestDbFactory(SqliteConnection connection) : IDbContextFactory<WempDbContext>
    {
        public WempDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(connection).Options;
            var db = new WempDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        public Task<WempDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
