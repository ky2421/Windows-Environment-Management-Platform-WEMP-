# 一次性脚本：为 optimization-items.json 每条添加 riskLevel 字段（UTF-8 处理，中文转义为 \uXXXX 不影响解析）
$ErrorActionPreference = 'Stop'
$path = 'D:\WEMP\database\seed\optimization-items.json'

$map = @{
    'svc.diagtrack'           = 'safe'
    'svc.print-spooler'       = 'advanced'
    'svc.xbox-services'       = 'advanced'
    'svc.sysmain'             = 'advanced'
    'svc.fax'                 = 'safe'
    'svc.geolocation'         = 'safe'
    'reg.wer'                 = 'safe'
    'reg.telemetry'           = 'safe'
    'reg.gamedvr'             = 'safe'
    'game.xbox-components'    = 'advanced'
    'hags.enable'             = 'advanced'
    'hags.disable'            = 'advanced'
    'vbs.memory-integrity-off'= 'aggressive'
    'startup.oem-bloat'       = 'safe'
    'startup.cleanup'         = 'safe'
    'network.dns'             = 'advanced'
    'disk.cleanup'            = 'safe'
    'disk.hiberfil'           = 'advanced'
    'disk.trim-ssd'           = 'safe'
    'power.high-performance'  = 'advanced'
    'pagefile.auto-tune'      = 'advanced'
    'visual.best-performance' = 'advanced'
    'background.disable-all'  = 'safe'
    'delivery.optimization-off' = 'safe'
    'gpu.force-high-performance' = 'advanced'
    'device.umbus-off'        = 'advanced'
    'device.hpet-off'         = 'aggressive'
    'bios.xmp-expo'           = 'aggressive'
    'timer.platform-clock'    = 'advanced'
    'guide.gpu-panel'         = 'safe'
    'appx.remove-bloatware'   = 'aggressive'
    'memory.background-apps'  = 'safe'
}

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$root = $text | ConvertFrom-Json
$root.PSObject.Properties.Remove('kbVersion')
$root | Add-Member -NotePropertyName kbVersion -NotePropertyValue 5
$changed = 0
foreach ($item in $root.items) {
    $level = $map[$item.code]
    if (-not $level) { throw "未映射的条目: $($item.code)" }
    $item | Add-Member -NotePropertyName riskLevel -NotePropertyValue $level
    $changed++
}

$out = $root | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($path, $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "已标注 $changed 条，riskLevel 映射完成，kbVersion=5"
