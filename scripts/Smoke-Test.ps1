param([Parameter(Mandatory = $true)][string]$Package)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('win-acme-gui-smoke-' + [guid]::NewGuid().ToString('N'))
Expand-Archive -Path $Package -DestinationPath $root
$exe = Join-Path $root 'WinAcmeGui.exe'
if (-not (Test-Path $exe)) { throw "GUI executable not found at $exe" }
if (-not (Test-Path (Join-Path $root 'worker'))) { throw 'Elevated worker directory is missing.' }
if (-not (Test-Path (Join-Path $root 'user-guide.pt-BR.md'))) { throw 'Portuguese guide is missing.' }
if (-not (Test-Path (Join-Path $root 'user-guide.en-US.md'))) { throw 'English guide is missing.' }
Write-Host "Package structure OK: $root"
