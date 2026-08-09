# win-acme GUI

Portable Windows desktop administration center for [win-acme](https://www.win-acme.com/), built with .NET 8 and WPF.

## What it does

On startup the GUI checks scheduled/process/PATH/known locations for `wacs.exe`, validates candidates with `wacs.exe --version`, resolves `settings.json` and its effective `ConfigurationPath`, and loads `*.renewal.json` files without modifying them. Multiple installations remain isolated and a manual executable selection is available.

The current desktop shell includes:

- dashboard with active installation, endpoint, configuration path and loaded renewals;
- renewal search surface with official renew, forced renew, cancel and revoke actions;
  - guided certificate draft for manual domains, HTTP-01 or TLS-ALPN-01 validation, RSA/EC keys, certificate store/PFX/PEM storage and staging preview;
- Portuguese (Brazil) and English labels;
- typed, shell-free command execution with secret redaction;
- backup manifests, diagnostics export and safe ZIP extraction primitives;
  - an authenticated named-pipe boundary to an allowlisted elevated worker for UAC operations.

The GUI does not decrypt win-acme secrets or edit renewal JSON by hand. Existing settings are only written through explicit, backup-first workflows. Generic DNS validation is not exposed because a DNS provider plugin and its credentials must be configured in win-acme first.

## Build and test

The core projects and non-visual localization layer can be tested on macOS/Linux:

```bash
dotnet restore WinAcmeGui.sln
dotnet test WinAcmeGui.sln --configuration Release
```

The WPF visual project targets `net8.0-windows` on Windows. On non-Windows hosts it compiles its testable non-visual layer so the cross-platform suite remains runnable. WPF execution, real UAC, IIS, Scheduled Tasks and portable-package smoke tests must run on a Windows 10/11 or Windows Server 2016+ x64 machine; this repository does not claim those checks from a macOS/Linux run.

## Portable package on Windows

```powershell
pwsh ./scripts/Publish-Portable.ps1
```

The script runs tests, publishes self-contained `win-x64` GUI and worker binaries, copies bilingual documentation and notices, writes a relative-path SHA-256 manifest, and creates `artifacts/WinAcmeGui-<version>-win-x64.zip`. A production package must be Authenticode-signed with `-SigningCertificatePath`; `-AllowUnsigned` is reserved for CI/dev validation and cannot pass the runtime worker trust boundary. The release downloader accepts only approved HTTPS GitHub hosts, official x64 assets with a SHA-256 digest, and safe ZIP contents.

Use the staging endpoint for first-run certificate acceptance tests. The GUI never silently overwrites an existing win-acme directory or runs a production mutation without confirmation.

## Documentation

- [Portuguese user guide](docs/user-guide.pt-BR.md)
- [English user guide](docs/user-guide.en-US.md)
- [Troubleshooting / pt-BR](docs/troubleshooting.pt-BR.md)
- [Troubleshooting / English](docs/troubleshooting.en-US.md)
- [Compatibility matrix](docs/compatibility.md)
- [Design specification](docs/superpowers/specs/2026-08-07-win-acme-gui-design.md)
- [Implementation plan](docs/superpowers/plans/2026-08-07-win-acme-gui.md)
