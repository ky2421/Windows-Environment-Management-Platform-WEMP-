Add-Type -AssemblyName System.Drawing

# 验证 ICO 条目结构
$fs = [System.IO.File]::OpenRead('D:\WEMP\src\WEMP.App\Assets\app.ico')
$br = New-Object System.IO.BinaryReader($fs)
$reserved = $br.ReadUInt16()
$type = $br.ReadUInt16()
$count = $br.ReadUInt16()
Write-Host ("ICO header: reserved={0} type={1} entries={2}" -f $reserved, $type, $count)
$sizes = @()
for ($i = 0; $i -lt $count; $i++) {
    $w = $br.ReadByte(); $h = $br.ReadByte()
    $colors = $br.ReadByte(); $res = $br.ReadByte()
    $planes = $br.ReadUInt16(); $bpp = $br.ReadUInt16()
    $len = $br.ReadUInt32(); $off = $br.ReadUInt32()
    $dim = if ($w -eq 0) { 256 } else { $w }
    $sizes += "$dim" + "x" + "$h"
    Write-Host ("  entry {0}: {1}x{2} bpp={3} bytes={4}" -f $i, $dim, $h, $bpp, $len)
}
$br.Close()
$fs.Close()

# 逐尺寸验证可加载
foreach ($s in @(16, 32, 48, 64, 256)) {
    try {
        $ico = New-Object System.Drawing.Icon('D:\WEMP\src\WEMP.App\Assets\app.ico', $s, $s)
        $bmp = $ico.ToBitmap()
        Write-Host ("  load {0}: OK ({1}x{1})" -f $s, $bmp.Width)
        $ico.Dispose()
    } catch {
        Write-Host ("  load {0}: FAIL {1}" -f $s, $_.Exception.Message)
    }
}
