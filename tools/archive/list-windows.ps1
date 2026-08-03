Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinEnum2 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
}
'@

$target = [int]$args[0]
$found = [System.Collections.ArrayList]::new()
$cb = [WinEnum2+EnumWindowsProc]{
    param($h, $lp)
    $winPid = 0
    [WinEnum2]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
    if ($winPid -eq $target) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinEnum2]::GetWindowText($h, $sb, 256) | Out-Null
        $vis = [WinEnum2]::IsWindowVisible($h)
        [void]$found.Add("h=$h vis=$vis title=[$($sb.ToString())]")
    }
    return $true
}
[WinEnum2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$found
