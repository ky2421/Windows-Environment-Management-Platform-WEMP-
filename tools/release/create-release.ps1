# Create GitHub Release v1.0.0 and upload the setup installer.
# Uses the GitHub credential from Git Credential Manager (no gh CLI required).
# Usage: powershell -ExecutionPolicy Bypass -File create-release.ps1

$ErrorActionPreference = 'Stop'

$repo = 'ky2421/Windows-Environment-Management-Platform-WEMP-'
$tag = 'v1.0.0'
$assetPath = 'D:\WEMP\dist\WEMP-1.0.0-setup.exe'
$bodyPath = Join-Path $PSScriptRoot 'release-body.md'

if (-not (Test-Path $assetPath)) { throw "asset not found: $assetPath" }

# Reuse the credential stored by Git Credential Manager
$stdin = "protocol=https`nhost=github.com`n`n"
$credOut = $stdin | git credential fill 2>$null
$token = ($credOut -split "`n" | Where-Object { $_ -like 'password=*' } | Select-Object -First 1).Substring(9)
if (-not $token) { throw 'failed to acquire GitHub token from credential manager' }
Write-Host "token acquired (len $($token.Length))"

$headers = @{ Authorization = "Bearer $token"; 'User-Agent' = 'wemp-release-script' }

# Check existing releases to avoid duplicates
$existing = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Get
$dup = $existing | Where-Object { $_.tag_name -eq $tag }
if ($dup) {
    Write-Host "release $tag already exists, updating instead"
    $rel = $dup | Select-Object -First 1
} else {
    $body = [System.IO.File]::ReadAllText($bodyPath, [System.Text.Encoding]::UTF8)
    $payload = @{ tag_name = $tag; name = 'WEMP V1.0.0'; body = $body } | ConvertTo-Json
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Post -Body $payload -ContentType 'application/json; charset=utf-8'
    Write-Host "release created: $($rel.html_url)"
}

# Upload installer asset (skip if already uploaded)
$assets = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($rel.id)/assets" -Headers $headers -Method Get
$existingAsset = $assets | Where-Object { $_.name -eq 'WEMP-1.0.0-setup.exe' }
if ($existingAsset) {
    Write-Host "asset already uploaded: $($existingAsset.browser_download_url)"
} else {
    $asset = Invoke-RestMethod -Uri "https://uploads.github.com/repos/$repo/releases/$($rel.id)/assets?name=WEMP-1.0.0-setup.exe" -Headers $headers -Method Post -InFile $assetPath -ContentType 'application/octet-stream'
    Write-Host "asset uploaded: $($asset.browser_download_url)"
}

Write-Host "DONE: $($rel.html_url)"
