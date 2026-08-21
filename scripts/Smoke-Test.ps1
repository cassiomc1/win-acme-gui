param([Parameter(Mandatory = $true)][string]$Package)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Package)) { throw "Package not found: $Package" }
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
    $manifestEntries = @(Get-Content $manifestPath -Raw | ConvertFrom-Json)
    foreach ($entry in $manifestEntries) {
        if ([string]::IsNullOrWhiteSpace($entry.Path)) { throw 'Manifest contains an empty path.' }
        if ([IO.Path]::IsPathRooted($entry.Path) -or $entry.Path.Contains('..')) { throw "Invalid manifest path: $($entry.Path)" }
        $file = Join-Path $root ($entry.Path -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path $file)) { throw "Manifest file is missing: $($entry.Path)" }
        $actual = (Get-FileHash $file -Algorithm SHA256).Hash
        if ($actual -ne $entry.Hash) { throw "Manifest hash mismatch: $($entry.Path)" }
    }

    # The manifest must also be complete: any file shipped in the package but absent from
    # SHA256SUMS.json escapes hash accountability and fails the smoke test.
    $manifestPaths = [System.StringComparer]::OrdinalIgnoreCase
    $expected = [System.Collections.Generic.HashSet[string]]::new($manifestPaths)
    foreach ($entry in $manifestEntries) { $expected.Add($entry.Path) | Out-Null }
    $actualFiles = Get-ChildItem $root -Recurse -File | ForEach-Object {
        $_.FullName.Substring($root.Length + 1).Replace('\', '/')
    } | Where-Object { $_ -ne 'SHA256SUMS.json' }
    foreach ($file in $actualFiles) {
        if (-not $expected.Contains($file)) { throw "Package file is not covered by the manifest: $file" }
    }

    $forbidden = Get-ChildItem $root -Recurse -File | Where-Object { $_.Extension -in '.pdb','.cs' -or $_.Name -match 'renewal\.json|settings\.json' }
    if ($forbidden) { throw "Forbidden package file found: $($forbidden.FullName -join ', ')" }
    Write-Host "Package structure and hashes OK: $root"
}
finally {
    if (Test-Path $root) {
        try { Remove-Item $root -Recurse -Force }
        catch { Write-Warning "Could not clean up smoke test directory: $root" }
    }
}
