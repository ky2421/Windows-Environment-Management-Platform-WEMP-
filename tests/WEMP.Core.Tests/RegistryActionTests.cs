using Microsoft.Win32;
using System.Text.Json;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;

namespace WEMP.Core.Tests;

/// <summary>注册表执行器测试：使用 HKCU 隔离测试键（无需管理员权限）。</summary>
public class RegistryActionTests
{
    private const string TestKeyPath = @"SOFTWARE\WEMP\Tests\RegistryAction";
    private const string TestValueName = "TestValue";

    private static OptimizationTarget Target(string valueData = "1") => new()
    {
        Key = $@"HKCU\{TestKeyPath}",
        ValueName = TestValueName,
        ValueData = JsonDocument.Parse(valueData).RootElement,
    };

    private static void Cleanup()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        baseKey.DeleteSubKeyTree(TestKeyPath, throwOnMissingSubKey: false);
    }

    [Fact]
    public async Task Apply_then_Restore_roundtrips_value()
    {
        Cleanup();
        try
        {
            // 准备原始值
            using (var key = Registry.CurrentUser.CreateSubKey(TestKeyPath))
            {
                key.SetValue(TestValueName, "original", RegistryValueKind.String);
            }

            var action = new RegistryAction();
            var target = Target(@"""optimized""");

            var backup = await action.BackupAsync(target, CancellationToken.None);
            Assert.IsType<RegistryBackup>(backup);
            Assert.True(((RegistryBackup)backup!).Exists);

            await action.ApplyAsync(target, backup, CancellationToken.None);

            using (var key = Registry.CurrentUser.OpenSubKey(TestKeyPath))
            {
                Assert.Equal("optimized", key!.GetValue(TestValueName));
            }

            await action.RestoreAsync(target, backup, CancellationToken.None);

            using (var key = Registry.CurrentUser.OpenSubKey(TestKeyPath))
            {
                Assert.Equal("original", key!.GetValue(TestValueName));
            }
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public async Task Apply_numeric_value_writes_dword()
    {
        Cleanup();
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(TestKeyPath))
            {
                key.SetValue(TestValueName, 0, RegistryValueKind.DWord);
            }

            var action = new RegistryAction();
            var target = Target("1"); // 数字

            var backup = await action.BackupAsync(target, CancellationToken.None);
            await action.ApplyAsync(target, backup, CancellationToken.None);

            using var readKey = Registry.CurrentUser.OpenSubKey(TestKeyPath);
            var value = readKey!.GetValue(TestValueName);
            Assert.IsType<int>(value);
            Assert.Equal(1, value);
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public async Task Restore_removes_value_when_backup_was_missing()
    {
        Cleanup();
        try
        {
            // 键存在但值不存在 → 备份 Exists=false，回滚应删除值
            using (var key = Registry.CurrentUser.CreateSubKey(TestKeyPath))
            {
                key.SetValue("OtherValue", "keep");
            }

            var action = new RegistryAction();
            var target = Target(@"""value""");

            var backup = await action.BackupAsync(target, CancellationToken.None);
            Assert.False(((RegistryBackup)backup!).Exists);

            await action.ApplyAsync(target, backup, CancellationToken.None);
            await action.RestoreAsync(target, backup, CancellationToken.None);

            using var readKey = Registry.CurrentUser.OpenSubKey(TestKeyPath);
            Assert.Null(readKey!.GetValue(TestValueName));
            Assert.Equal("keep", readKey.GetValue("OtherValue"));
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void Target_Parse_handles_kb_json()
    {
        const string json = "{\"key\":\"HKLM\\\\SOFTWARE\\\\Test\",\"valueName\":\"Disabled\",\"valueData\":1}";

        var target = OptimizationTarget.Parse(json);

        Assert.NotNull(target);
        Assert.Equal(@"HKLM\SOFTWARE\Test", target!.Key);
        Assert.Equal("Disabled", target.ValueName);
        Assert.True(target.ValueData!.Value.ValueKind == System.Text.Json.JsonValueKind.Number);
    }
}
