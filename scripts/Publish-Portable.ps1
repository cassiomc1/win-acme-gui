param(
    [string]$Version = '',
    [string]$SigningCertificatePath = '',
    [string]$SigningCertificatePassword = '',
    [switch]$AllowUnsigned
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'WinAcmeGui.sln'
$appProject = Join-Path $root 'src/WinAcmeGui.App/WinAcmeGui.App.csproj'
$workerProject = Join-Path $root 'src/WinAcmeGui.ElevatedWorker/WinAcmeGui.ElevatedWorker.csproj'
$artifactRoot = Join-Path $root 'artifacts'
$publish = Join-Path $artifactRoot 'publish/win-x64'

if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $env:GITHUB_REF_NAME }
if ([string]::IsNullOrWhiteSpace($Version)) {
    try { $Version = (& git -C $root describe --tags --always --dirty 2>$null).Trim() } catch { $Version = '' }
}
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = '0.0.0-local' }
$Version = $Version.TrimStart('v')
$packageVersion = if ($Version -match '^\d+\.\d+\.\d+([-.].*)?$') { $Version } else { "0.0.0-$($Version -replace '[^0-9A-Za-z.-]', '-')" }
$zip = Join-Path $artifactRoot "WinAcmeGui-$packageVersion-win-x64.zip"

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) { throw 'Portable WPF publishing must run on Windows.' }
# Native commands ignore $ErrorActionPreference; a failed test/publish must abort the script,
# otherwise a package can be produced from stale or partial output.
$PSNativeCommandUseErrorActionPreference = $true
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
Get-ChildItem $artifactRoot -Filter 'WinAcmeGui-*-win-x64.zip' -ErrorAction SilentlyContinue | Remove-Item -Force
New-Item -ItemType Directory -Force -Path $publish | Out-Null

dotnet test $solution --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE." }
dotnet publish $appProject -c Release -r win-x64 --self-contained true -o $publish -p:PublishSingleFile=false -p:DebugType=None -p:Version=$packageVersion
if ($LASTEXITCODE -ne 0) { throw "GUI publish failed with exit code $LASTEXITCODE." }
dotnet publish $workerProject -c Release -r win-x64 --self-contained true -o (Join-Path $publish 'worker') -p:PublishSingleFile=false -p:DebugType=None -p:Version=$packageVersion
if ($LASTEXITCODE -ne 0) { throw "Worker publish failed with exit code $LASTEXITCODE." }

Copy-Item (Join-Path $root 'README.md') $publish
Copy-Item (Join-Path $root 'docs/user-guide.pt-BR.md') $publish
Copy-Item (Join-Path $root 'docs/user-guide.en-US.md') $publish
Copy-Item (Join-Path $root 'docs/troubleshooting.pt-BR.md') $publish
Copy-Item (Join-Path $root 'docs/troubleshooting.en-US.md') $publish
Copy-Item (Join-Path $root 'docs/compatibility.md') $publish
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish

$forbidden = Get-ChildItem $publish -Recurse -File | Where-Object { $_.Extension -in '.pdb','.cs' -or $_.Name -match 'renewal\.json|settings\.json' }
if ($forbidden) { throw "Forbidden source/configuration material found in package: $($forbidden.FullName -join ', ')" }
$workerExecutable = Join-Path $publish 'worker/WinAcmeGui.ElevatedWorker.exe'
${guiExecutable} = Join-Path $publish 'WinAcmeGui.exe'
if (-not (Test-Path $guiExecutable)) { throw 'GUI executable is missing from the package.' }
if (-not (Test-Path $workerExecutable)) { throw 'Elevated worker executable is missing from the package.' }
if ([string]::IsNullOrWhiteSpace($SigningCertificatePath)) {
    if (-not $AllowUnsigned) { throw 'Production packages must be Authenticode-signed. Provide -SigningCertificatePath or explicitly use -AllowUnsigned for CI/dev validation.' }
}
else {
    if (-not (Test-Path $SigningCertificatePath)) { throw "Signing certificate not found: $SigningCertificatePath" }
    $signtool = (Get-Command signtool.exe -ErrorAction Stop).Source
    $signArguments = @('sign', '/fd', 'SHA256', '/tr', 'http://timestamp.digicert.com', '/td', 'SHA256', '/f', $SigningCertificatePath)
    if (-not [string]::IsNullOrWhiteSpace($SigningCertificatePassword)) { $signArguments += @('/p', $SigningCertificatePassword) }
    foreach ($executable in @($guiExecutable, $workerExecutable)) {
        & $signtool @signArguments $executable
        if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed for $executable." }
    }
}
$hashes = Get-ChildItem $publish -Recurse -File | Sort-Object FullName | ForEach-Object {
    [PSCustomObject]@{
        Path = $_.FullName.Substring($publish.Length + 1).Replace('\', '/')
        Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    }
}
$hashes | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $publish 'SHA256SUMS.json')
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Portable package: $zip"
Write-Host (Get-FileHash $zip -Algorithm SHA256 | Format-Table -AutoSize | Out-String)
