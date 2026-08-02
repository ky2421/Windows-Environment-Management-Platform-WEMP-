# WEMP 打包脚本（占位：Phase 10 接入 WiX 后启用）
# 用法: .\scripts\package.ps1 [-Version 0.1.0]

param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> package $Version (未实现：等待 installer/ 目录 WiX 项目)" -ForegroundColor Yellow
