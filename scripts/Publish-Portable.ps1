param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'WinAcmeGui.sln'
$appProject = Join-Path $root 'src/WinAcmeGui.App/WinAcmeGui.App.csproj'
$workerProject = Join-Path $root 'src/WinAcmeGui.ElevatedWorker/WinAcmeGui.ElevatedWorker.csproj'
$artifactRoot = Join-Path $root 'artifacts'
$publish = Join-Path $artifactRoot 'publish/win-x64'
$zip = Join-Path $artifactRoot "WinAcmeGui-$((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmm'))-win-x64.zip"

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) { throw 'Portable WPF publishing must run on Windows.' }
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish | Out-Null

dotnet test $solution --configuration Release
dotnet publish $appProject -c Release -r win-x64 --self-contained true -o $publish -p:PublishSingleFile=false -p:DebugType=None
dotnet publish $workerProject -c Release -r win-x64 --self-contained true -o (Join-Path $publish 'worker') -p:PublishSingleFile=false -p:DebugType=None

Copy-Item (Join-Path $root 'README.md') $publish
Copy-Item (Join-Path $root 'docs/user-guide.pt-BR.md') $publish
Copy-Item (Join-Path $root 'docs/user-guide.en-US.md') $publish
Copy-Item (Join-Path $root 'docs/troubleshooting.pt-BR.md') $publish
Copy-Item (Join-Path $root 'docs/troubleshooting.en-US.md') $publish
Copy-Item (Join-Path $root 'docs/compatibility.md') $publish
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish

$forbidden = Get-ChildItem $publish -Recurse -File | Where-Object { $_.Extension -in '.pdb','.cs' -or $_.Name -match 'renewal\.json|settings\.json' }
if ($forbidden) { throw "Forbidden source/configuration material found in package: $($forbidden.FullName -join ', ')" }
$hashes = Get-ChildItem $publish -Recurse -File | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash
$hashes | ConvertTo-Json | Set-Content (Join-Path $publish 'SHA256SUMS.json')
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Portable package: $zip"
Write-Host (Get-FileHash $zip -Algorithm SHA256 | Format-Table -AutoSize | Out-String)
