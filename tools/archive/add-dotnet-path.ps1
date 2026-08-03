$ErrorActionPreference = 'Stop'
$dotnet = 'D:\DevTools\dotnet'
$key = 'HKCU:\Environment'

$current = (Get-ItemProperty -Path $key -Name Path -ErrorAction SilentlyContinue).Path

if ($current -and ($current -split ';') -contains $dotnet) {
    Write-Output 'already-in-path'
} else {
    $new = if ($current) { $current.TrimEnd(';') + ';' + $dotnet } else { $dotnet }
    Set-ItemProperty -Path $key -Name Path -Value $new -Type ExpandString
    Write-Output ("updated: {0}" -f $new)
}

# 广播环境变量变更，让新启动的进程读到
Add-Type -Namespace Win32 -Name Native -MemberDefinition @'
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
'@ -ErrorAction SilentlyContinue

$null = [Win32.Native]::SendMessageTimeout([IntPtr]::Zero, 0x1A, [UIntPtr]::Zero, 'Environment', 0x0002, 5000, [ref]([UIntPtr]::Zero))

# 验证：新进程中 where dotnet
$probe = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c where dotnet' -Wait -PassThru -WindowStyle Hidden
$probe | Out-Null
Write-Output 'broadcast-done'
