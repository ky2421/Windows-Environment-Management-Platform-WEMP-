$env:DOTNET_ROOT = 'D:\DevTools\dotnet'
Start-Process -FilePath 'D:\DevTools\dotnet\dotnet.exe' -ArgumentList 'D:\WEMP\src\WEMP.App\bin\Debug\net8.0-windows\WEMP.App.dll'
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 2
    $w = Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
    if ($w) {
        $ids = ($w | ForEach-Object { $_.Id }) -join ','
        Write-Output ("t={0}s window-pids: {1}" -f ($i * 2), $ids)
    } else {
        Write-Output ("t={0}s no-window" -f ($i * 2))
    }
}
