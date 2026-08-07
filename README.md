# win-acme GUI

Portable Windows desktop administration center for [win-acme](https://www.win-acme.com/), built with .NET 8 and WPF.

## What it does

On startup the GUI checks scheduled/process/PATH/known locations for `wacs.exe`, validates candidates with `wacs.exe --version`, resolves `settings.json` and its effective `ConfigurationPath`, and loads `*.renewal.json` files without modifying them. Multiple installations remain isolated and a manual executable selection is available.

The current desktop shell includes:

- dashboard with active installation, endpoint, configuration path and loaded renewals;
- renewal search surface with official renew, forced renew, cancel and revoke actions;
- guided certificate draft for manual domains, HTTP/DNS/TLS validation, RSA/EC keys, certificate store/PFX/PEM storage and staging preview;
- Portuguese (Brazil) and English labels;
- typed, shell-free command execution with secret redaction;
- backup manifests, diagnostics export and safe ZIP extraction primitives;
- an allowlisted elevated-worker boundary for operations that need UAC.

The GUI does not decrypt win-acme secrets or edit renewal JSON by hand. Existing settings are only written through explicit, backup-first workflows.

## Build and test

The core projects and non-visual localization layer can be tested on macOS/Linux:

```bash
dotnet restore WinAcmeGui.sln
dotnet test WinAcmeGui.sln --configuration Release
```

The WPF visual project targets `net8.0-windows` on Windows. On non-Windows hosts it compiles its testable non-visual layer so the cross-platform suite remains runnable. WPF build and UI smoke tests must run on a Windows 10/11 or Windows Server 2016+ x64 machine.

## Portable package on Windows

```powershell
pwsh ./scripts/Publish-Portable.ps1
```

The script runs tests, publishes self-contained `win-x64` GUI and worker binaries, copies bilingual documentation and notices, writes a SHA-256 manifest, and creates `artifacts/WinAcmeGui-<version>-win-x64.zip`.

Use the staging endpoint for first-run certificate acceptance tests. The GUI never silently overwrites an existing win-acme directory or runs a production mutation without confirmation.

## Documentation

- [Portuguese user guide](docs/user-guide.pt-BR.md)
- [English user guide](docs/user-guide.en-US.md)
- [Troubleshooting / pt-BR](docs/troubleshooting.pt-BR.md)
- [Troubleshooting / English](docs/troubleshooting.en-US.md)
- [Compatibility matrix](docs/compatibility.md)
- [Design specification](docs/superpowers/specs/2026-08-07-win-acme-gui-design.md)
- [Implementation plan](docs/superpowers/plans/2026-08-07-win-acme-gui.md)
