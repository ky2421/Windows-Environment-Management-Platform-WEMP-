# WEMP 发布脚本
# 用法: .\scripts\publish.ps1 [-SelfContained]
# 说明: clean 后全量 publish 到 dist\wemp，确保嵌入资源（优化知识库 JSON、模板等）为最新，
#       避免 bin 目录残留旧构建导致发布产物与源码不一致。

param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\wemp"

Write-Host "==> clean (Release)" -ForegroundColor Cyan
& dotnet clean "$root\WEMP.sln" -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "clean failed" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host "==> publish WEMP.App (Release)" -ForegroundColor Cyan
if ($SelfContained) {
    & dotnet publish "$root\src\WEMP.App\WEMP.App.csproj" -c Release -o $out --self-contained -r win-x64
}
else {
    & dotnet publish "$root\src\WEMP.App\WEMP.App.csproj" -c Release -o $out
}
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

Write-Host "==> publish OK: $out" -ForegroundColor Green
