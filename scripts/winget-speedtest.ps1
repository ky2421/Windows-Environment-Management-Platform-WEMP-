$dir = Join-Path $env:TEMP 'wemp-speedtest'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$pairs = @(
  @{ Label = 'Git';      Id = 'Git.Git' },
  @{ Label = 'Deno';     Id = 'DenoLand.Deno' },
  @{ Label = '7zip';     Id = '7zip.7zip' },
  @{ Label = 'VCRedist'; Id = 'Microsoft.VCRedist.2015+.x64' },
  @{ Label = 'MSYS2';    Id = 'MSYS2.MSYS2' }
)

$results = @()
foreach ($p in $pairs) {
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  winget download --id $p.Id --download-directory $dir --accept-source-agreements --disable-interactivity 2>&1 | Out-Null
  $sw.Stop()
  $size = (Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
  $mb = [math]::Round($size / 1MB, 1)
  $sec = [math]::Round($sw.Elapsed.TotalSeconds, 1)
  $speed = if ($sec -gt 0) { [math]::Round($mb / $sec, 2) } else { 0 }
  $results += ("{0} ({1}) => {2} MB in {3}s => {4} MB/s" -f $p.Label, $p.Id, $mb, $sec, $speed)
  Remove-Item $dir\* -Recurse -Force -ErrorAction SilentlyContinue
}

$results | Set-Content -Path (Join-Path $env:TEMP 'wemp-speedtest.txt') -Encoding UTF8
Write-Host "done: $($results.Count) downloads measured"
