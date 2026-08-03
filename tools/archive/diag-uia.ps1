Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WinDiag {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  public static IntPtr FindByTitle(int pid, string part) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      var sb = new StringBuilder(256);
      GetWindowText(h, sb, 256);
      uint p2 = 0;
      GetWindowThreadProcessId(h, out p2);
      if (p2 == pid && sb.ToString().Contains(part)) { found = h; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@

$p = Get-Process -Name WEMP.App -ErrorAction SilentlyContinue
Write-Host ('pid: ' + $p.Id)

$h = [WinDiag]::FindByTitle($p.Id, 'WEMP')
Write-Host ('main hwnd: ' + $h)
Write-Host ('hwnd zero? ' + ($h -eq [IntPtr]::Zero))

try {
  $lcond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
  Write-Host ('lcond: ' + [bool]$lcond)
} catch {
  Write-Host ('lcond FAILED: ' + $_.Exception.Message)
}

try {
  $win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  Write-Host ('win: ' + [bool]$win + ' name=' + $win.Current.Name)
  $nav = $win.FindAll('Descendants', $lcond)
  Write-Host ('nav count: ' + $nav.Count)
} catch {
  Write-Host ('win/FindAll FAILED: ' + $_.Exception.Message)
}
