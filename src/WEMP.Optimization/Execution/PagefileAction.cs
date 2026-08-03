using System.Runtime.InteropServices;
using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 虚拟内存优化执行器：关闭自动管理，为系统盘设置固定分页文件
/// （初始=物理内存，最大=物理内存×1.5）。物理内存 ≥ 32G 时保持自动管理。
/// 修改需重启生效。
/// </summary>
public sealed class PagefileAction : IOptimizationAction
{
    private const string MemoryManagementKey =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

    public string ItemType => "pagefile";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.LocalMachine.OpenSubKey(MemoryManagementKey, writable: false);
        var autoManaged = key?.GetValue("AutomaticManagedPagefile");
        var pagingFiles = key?.GetValue("PagingFiles");

        string[]? paging = pagingFiles switch
        {
            string[] files => files,
            string single => [single],
            _ => null,
        };

        return Task.FromResult<object?>(new PagefileBackup(
            autoManaged is null ? 1 : Convert.ToInt32(autoManaged), paging));
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 读取物理内存总量（MB）
        var totalMb = GetTotalPhysicalMemoryMb();

        // 32G 及以上：保持系统自动管理（方案 B）
        if (totalMb >= 32 * 1024)
        {
            using var managedKey = Registry.LocalMachine.CreateSubKey(MemoryManagementKey, writable: true);
            managedKey.SetValue("AutomaticManagedPagefile", 1, RegistryValueKind.DWord);
            return Task.FromResult<object?>($"物理内存 {totalMb / 1024.0:F1} GB ≥ 32G：保持系统自动管理，未设置固定大小");
        }

        // 方案 A：固定大小（初始=物理内存，最大=×1.5，单位 MB）
        var initial = totalMb;
        var maximum = (long)(totalMb * 1.5);
        var pagingValue = $"C:\\pagefile.sys {initial} {maximum}";

        using var key = Registry.LocalMachine.CreateSubKey(MemoryManagementKey, writable: true);
        key.SetValue("AutomaticManagedPagefile", 0, RegistryValueKind.DWord);
        key.SetValue("PagingFiles", new[] { pagingValue }, RegistryValueKind.MultiString);

        return Task.FromResult<object?>($"物理内存 {totalMb / 1024.0:F1} GB：系统盘分页文件已设为 {initial} MB ~ {maximum} MB（重启生效）");
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not PagefileBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var key = Registry.LocalMachine.CreateSubKey(MemoryManagementKey, writable: true);
        if (b.AutoManaged == 1)
        {
            key.SetValue("AutomaticManagedPagefile", 1, RegistryValueKind.DWord);
        }

        if (b.PagingFiles is { } files)
        {
            key.SetValue("PagingFiles", files, RegistryValueKind.MultiString);
        }

        return Task.CompletedTask;
    }

    private static long GetTotalPhysicalMemoryMb()
    {
        var status = new MemoryStatusEx();
        status.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        return GlobalMemoryStatusEx(ref status)
            ? (long)(status.ullTotalPhys / 1024 / 1024)
            : 16384; // 查询失败按 16G 处理
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

/// <summary>虚拟内存备份数据。</summary>
public sealed record PagefileBackup(int AutoManaged, string[]? PagingFiles);
