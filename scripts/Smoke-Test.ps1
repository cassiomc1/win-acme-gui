param([Parameter(Mandatory = $true)][string]$Package)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('win-acme-gui-smoke-' + [guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -Path $Package -DestinationPath $root
    $exe = Join-Path $root 'WinAcmeGui.exe'
    if (-not (Test-Path $exe)) { throw "GUI executable not found at $exe" }
    $worker = Join-Path $root 'worker/WinAcmeGui.ElevatedWorker.exe'
    if (-not (Test-Path $worker)) { throw "Elevated worker executable not found at $worker" }
    if (-not (Test-Path (Join-Path $root 'user-guide.pt-BR.md'))) { throw 'Portuguese guide is missing.' }
    if (-not (Test-Path (Join-Path $root 'user-guide.en-US.md'))) { throw 'English guide is missing.' }

    $manifestPath = Join-Path $root 'SHA256SUMS.json'
    if (-not (Test-Path $manifestPath)) { throw 'SHA256SUMS.json is missing.' }
    foreach ($entry in @(Get-Content $manifestPath -Raw | ConvertFrom-Json)) {
        if ([IO.Path]::IsPathRooted($entry.Path) -or $entry.Path.Contains('..')) { throw "Invalid manifest path: $($entry.Path)" }
        $file = Join-Path $root ($entry.Path -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path $file)) { throw "Manifest file is missing: $($entry.Path)" }
        $actual = (Get-FileHash $file -Algorithm SHA256).Hash
        if ($actual -ne $entry.Hash) { throw "Manifest hash mismatch: $($entry.Path)" }
    }

    $forbidden = Get-ChildItem $root -Recurse -File | Where-Object { $_.Extension -in '.pdb','.cs' -or $_.Name -match 'renewal\.json|settings\.json' }
    if ($forbidden) { throw "Forbidden package file found: $($forbidden.FullName -join ', ')" }
    Write-Host "Package structure and hashes OK: $root"
}
finally {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
}
