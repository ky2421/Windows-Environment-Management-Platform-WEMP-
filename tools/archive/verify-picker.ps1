Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WinFind2 {
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
if (-not $p) { Write-Host 'app not running'; exit 1 }

# 用 Win32 找主窗口，避免 UIA RootElement 顶层枚举的不稳定
$mainHwnd = [WinFind2]::FindByTitle($p.Id, 'WEMP')
if ($mainHwnd -eq [IntPtr]::Zero) { Write-Host 'main window NOT FOUND'; exit 1 }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($mainHwnd)

# 1. 导航到开发环境
try {
  $lcond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
} catch { Write-Host ('lcond FAILED: ' + $_.Exception.Message); exit 1 }
Write-Host ('diag lcond: ' + [bool]$lcond + ' win: ' + [bool]$win)
$nav = $win.FindAll('Descendants', $lcond)
$navTarget = $null
foreach ($i in $nav) { if ($i.Current.Name -match 'devenv') { $navTarget = $i; break } }
if ($navTarget) { $navTarget.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Write-Host '1. nav devenv' }
Start-Sleep 3

# 2. 选择第一个模板
$items = $win.FindAll('Descendants', $lcond)
$tmpl = $null
foreach ($i in $items) { if ($i.Current.Name -match 'EnvTemplate') { $tmpl = $i; break } }
if ($tmpl) { $tmpl.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Write-Host '2. selected template' }
Start-Sleep 1

# 3. 点击部署按钮
$b1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$b2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '部署选中模板')
$bcond = New-Object System.Windows.Automation.AndCondition($b1, $b2)
$btn = $win.FindFirst('Descendants', $bcond)
if ($btn) { $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host '3. clicked deploy' }
Start-Sleep 3

# 4. 通过 Win32 找弹窗
$hwnd = [WinFind2]::FindByTitle($p.Id, '选择')
if ($hwnd -eq [IntPtr]::Zero) { Write-Host '4. picker NOT FOUND'; exit 1 }
Write-Host ('4. picker hwnd=0x{0:X}' -f $hwnd.ToInt64())
$dlg = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

# 5. 读取复选框
$cbc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::CheckBox)
$cbs = $dlg.FindAll('Descendants', $cbc)
Write-Host ('5. checkboxes: ' + $cbs.Count)
foreach ($c in $cbs) {
  $tog = $c.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
  Write-Host ('   [' + $c.Current.Name + '] state=' + $tog.Current.ToggleState + ' enabled=' + $c.Current.IsEnabled)
}

# 6. 取消关闭
$c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '取消')
$ccond = New-Object System.Windows.Automation.AndCondition($c1, $c2)
$cancel = $dlg.FindFirst('Descendants', $ccond)
if ($cancel) { $cancel.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host '6. cancel clicked' }
Start-Sleep 1
$hwnd2 = [WinFind2]::FindByTitle($p.Id, '选择')
Write-Host ('7. picker after cancel: ' + ($hwnd2 -ne [IntPtr]::Zero))
Write-Host 'ALL DONE'
