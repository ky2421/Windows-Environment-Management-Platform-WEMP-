using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 基于注册表（HKCU\Environment）的环境变量服务。
/// 值写入前展开 %VAR% 引用，写后广播 WM_SETTINGCHANGE 通知系统刷新。
/// </summary>
public sealed class EnvironmentVariableService : IEnvironmentVariableService
{
    private const string EnvironmentKey = "Environment";
    private const int WmSettingChange = 0x001A;

    public string? GetValue(string name, string scope = "user")
    {
        if (!string.Equals(scope, "user", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        using var key = Registry.CurrentUser.OpenSubKey(EnvironmentKey);
        return key?.GetValue(name) as string;
    }

    public string? SetValue(string name, string? value, string scope = "user")
    {
        var original = GetValue(name, scope);
        using var key = Registry.CurrentUser.CreateSubKey(EnvironmentKey, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException($"无法打开注册表键 HKCU\\{EnvironmentKey}");
        }

        if (value is null)
        {
            if (key.GetValue(name) is not null)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
        }
        else
        {
            key.SetValue(name, ExpandVariables(value));
        }

        NotifyEnvironmentChanged();
        return original;
    }

    /// <summary>展开 %VAR% 引用（仅本进程可见的环境变量）。</summary>
    internal static string ExpandVariables(string value)
    {
        var result = value;
        for (var i = 0; i < 5; i++)
        {
            var expanded = Environment.ExpandEnvironmentVariables(result);
            if (expanded == result)
            {
                break;
            }

            result = expanded;
        }

        return result;
    }

    private static void NotifyEnvironmentChanged()
    {
        var text = "Environment";
        var ptr = Marshal.StringToHGlobalUni(text);
        try
        {
            _ = SendMessageTimeout(
                HWND_BROADCAST, WmSettingChange, IntPtr.Zero, ptr,
                SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 1000, out _);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_ABORTIFHUNG = 0x0002,
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam,
        SendMessageTimeoutFlags flags, uint timeout, out IntPtr result);
}
