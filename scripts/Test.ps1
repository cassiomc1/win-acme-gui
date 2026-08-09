$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet restore (Join-Path $root 'WinAcmeGui.sln')
dotnet test (Join-Path $root 'WinAcmeGui.sln') --no-restore --configuration Release
dotnet build (Join-Path $root 'WinAcmeGui.sln') --no-restore --configuration Release
