# 修正模块页面残余浅色硬编码
$ErrorActionPreference = 'Stop'
$files = Get-ChildItem 'D:\WEMP\src' -Recurse -Filter *.xaml | Where-Object { $_.FullName -notmatch 'obj|bin|WEMP.App' }
foreach ($f in $files) {
    $content = [System.IO.File]::ReadAllText($f.FullName)
    $orig = $content
    $content = $content.Replace('Background="#F5F6F8"', 'Background="{DynamicResource CardBackgroundBrush}"')
    $content = $content.Replace('Foreground="#444444"', 'Foreground="{DynamicResource TextSecondaryBrush}"')
    $content = $content.Replace('Foreground="#AA7700"', 'Foreground="{DynamicResource WarningBrush}"')
    $content = $content.Replace('Background="#E3E9F3"', 'Background="{DynamicResource AccentMutedBrush}"')
    $content = $content.Replace('Background="#F0E7D4"', 'Background="#4A3A1E"')
    $content = $content.Replace('Foreground="#AA6666"', 'Foreground="{DynamicResource DangerBrush}"')
    if ($content -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $content, (New-Object System.Text.UTF8Encoding $false))
        Write-Output "updated: $($f.Name)"
    }
}
Write-Output 'done'
