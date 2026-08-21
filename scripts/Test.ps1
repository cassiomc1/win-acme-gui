$ErrorActionPreference = 'Stop'
# Native commands (dotnet) do not honor $ErrorActionPreference; without these checks a failing
# restore or test run would still exit 0 and report success to automation.
$PSNativeCommandUseErrorActionPreference = $true
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'WinAcmeGui.sln'

function Assert-NativeSuccess([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE." }
}

dotnet restore $solution
Assert-NativeSuccess 'dotnet restore'
dotnet test $solution --no-restore --configuration Release
Assert-NativeSuccess 'dotnet test'
