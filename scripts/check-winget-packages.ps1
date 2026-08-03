$ids = @(
  'Google.Chrome','DenoLand.Deno','Oven-sh.Bun',
  'RubyInstallerTeam.RubyWithDevKit.3.2','Qt.Qt','Google.Flutter',
  'Google.AndroidStudio','Microsoft.VCRedist.2015+.x64','MSYS2.MSYS2',
  'Oracle.MySQL','MariaDB.Server','PostgreSQL.PostgreSQL.16',
  'Redis.Redis','MongoDB.Server','Microsoft.SQLServer.2022.Developer',
  'Microsoft.WSL','nginx.nginx','Arduino.IDE',
  'JetBrains.IntelliJIDEA.Community','JetBrains.PyCharm.Community',
  'JetBrains.WebStorm','JetBrains.CLion'
)
$out = @()
foreach ($id in $ids) {
  $r = winget show --id $id --accept-source-agreements --disable-interactivity 2>&1 | Out-String
  $found = $r -match [regex]::Escape($id) -and $r -notmatch 'No package found'
  $out += ("{0} => {1}" -f $id, ($(if ($found) { 'FOUND' } else { 'NOT FOUND' })))
}
$out | Set-Content -Path "$env:TEMP\wemp-winget-check.txt" -Encoding UTF8
Write-Host "done: $($out.Count) packages checked"
