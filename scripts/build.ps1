# WEMP 构建脚本
# 用法: .\scripts\build.ps1 [-Configuration Debug|Release]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> dotnet build ($Configuration)" -ForegroundColor Cyan
& dotnet build "$root\WEMP.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Write-Host "==> dotnet test" -ForegroundColor Cyan
& dotnet test "$root\WEMP.sln" -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "test failed" }

Write-Host "==> build OK" -ForegroundColor Green
