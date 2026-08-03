Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WinEnum2 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

$p = Get-Process -Name WEMP.App -ErrorAction SilentlyContinue
$pid2 = $p.Id
$rows = New-Object System.Collections.ArrayList
$cb = [WinEnum2+EnumWindowsProc]{
  param($h, $l)
  $w = New-Object System.Text.StringBuilder 256
  [WinEnum2]::GetWindowText($h, $w, 256) | Out-Null
  $p2 = 0
  [WinEnum2]::GetWindowThreadProcessId($h, [ref]$p2) | Out-Null
  if ($p2 -eq $pid2) {
    [void]$rows.Add(('hwnd=0x{0:X} title=[{1}] enabled={2} visible={3}' -f $h.ToInt64(), $w.ToString(), [WinEnum2]::IsWindowEnabled($h), [WinEnum2]::IsWindowVisible($h)))
  }
  return $true
}
[WinEnum2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$rows | ForEach-Object { Write-Host $_ }
