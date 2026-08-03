# WEMP 打包脚本（Inno Setup）
# 用法: .\scripts\package.ps1 [-Version 1.0.0] [-SkipPublish]
# 说明: 先确保 dist\wemp 存在（scripts\publish.ps1 产物），再编译安装包到 dist\WEMP-<version>-setup.exe。
#       依赖 Inno Setup 6（winget install JRSoftware.InnoSetup）。

param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "dist\wemp"

# Inno Setup 6 可能安装在程序文件或用户级目录
$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "未找到 Inno Setup：请先安装（winget install JRSoftware.InnoSetup）"
}

if (-not (Test-Path (Join-Path $publishDir "WEMP.App.exe"))) {
    if ($SkipPublish) { throw "dist\wemp 不存在且已指定 -SkipPublish" }
    Write-Host "==> dist\wemp 不存在，先执行 publish" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "publish.ps1")
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }
}

Write-Host "==> 编译安装包 WEMP-$Version (Inno Setup)" -ForegroundColor Cyan
& $iscc (Join-Path $root "installer\wemp.iss") "/DMyAppVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setup = Join-Path $root "dist\WEMP-$Version-setup.exe"
if (Test-Path $setup) {
    Write-Host "==> 打包成功: $setup" -ForegroundColor Green
}
else {
    throw "打包产物未生成：$setup"
}
