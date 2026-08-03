$env:DOTNET_ROOT = 'D:\DevTools\dotnet'
Start-Process -FilePath 'D:\DevTools\dotnet\dotnet.exe' -ArgumentList 'D:\WEMP\src\WEMP.App\bin\Debug\net8.0-windows\WEMP.App.dll'
Start-Sleep -Seconds 8

$p = Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
if (-not $p) {
    Write-Output 'no-window'
    exit 1
}
$pid0 = $p.Id
Write-Output ("window-pid: {0}" -f $pid0)

# 模拟点击关闭按钮（叉掉窗口）
$closed = $p.CloseMainWindow()
Write-Output ("close-request: {0}" -f $closed)
Start-Sleep -Seconds 5

$alive = Get-Process -Id $pid0 -ErrorAction SilentlyContinue
if ($alive) {
    $m = [math]::Round($alive.WorkingSet64 / 1MB, 1)
    Write-Output ("STILL-ALIVE pid={0} mem={1}MB title=[{2}]" -f $pid0, $m, $alive.MainWindowTitle)
} else {
    Write-Output 'exited-cleanly'
}
