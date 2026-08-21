# win-acme GUI

Portable Windows desktop administration center for [win-acme](https://www.win-acme.com/), built with .NET 8 and WPF.

## Current scope

The current shell provides:

- read-only discovery of `wacs.exe` through scheduled tasks, processes, `PATH`, known locations and manual selection;
- validation with `wacs.exe --version`, effective `settings.json`/`ConfigurationPath` resolution and isolated renewal inventory;
- search by friendly name, ID, domain or status;
- normal renewal, forced renewal, cancellation and revocation for editable renewal rows;
- a manual certificate wizard with HTTP-01 or TLS-ALPN-01 validation, RSA/EC keys, certificate-store/PFX/PEM output and staging preview;
- Portuguese (Brazil) and English labels;
- a GUI-only light/dark theme toggle;
- shell-free typed command execution, secret redaction, cancellation and an authenticated allowlisted elevated worker on Windows. The GUI↔worker handshake never places the shared token on a command line, verifies the connected process identity before sending requests and HMAC-authenticates every response;
- safe official x64 release download, SHA-256 verification, ZIP preflight and backup primitives.

The GUI never edits `*.renewal.json` directly or decrypts win-acme secrets. Unknown, malformed or shared-configuration renewals remain visible and read-only.

### Confirmation and mutation policy

- Normal renewal is started by the explicit Renew action.
- Forced renewal asks for an additional confirmation.
- Cancel and Revoke require typing the renewal friendly name; revocation is intended for compromised keys.
- New certificate creation requires review, acceptance of the Let's Encrypt terms and confirmation before execution.
- Read-only rows and operations without a valid active installation are disabled or rejected.

The current shell does not expose IIS management, DNS-plugin setup, renewal edit/clone, scheduled-task management, a settings editor/restore screen, a browser for the original win-acme logs or diagnostic ZIP export. Use the official win-acme console for those workflows until they are implemented and validated.

## Build and test

The core projects and non-visual localization layer can be tested on macOS/Linux:

```bash
dotnet restore WinAcmeGui.sln
dotnet test WinAcmeGui.sln --configuration Release
dotnet build WinAcmeGui.sln --configuration Release
```

The WPF visual project targets `net8.0-windows` on Windows. On non-Windows hosts it compiles its testable non-visual layer so the cross-platform suite remains runnable. The GitHub Windows acceptance workflow currently runs on `windows-latest` and checks tests, WPF/worker compilation, unsigned CI packaging, package hashes and uploads the validated ZIP as a workflow artifact. It is not a complete Windows 10/11/Server matrix and does not replace real UAC, IIS, Scheduled Tasks, certificate-store or staging-lifecycle acceptance.

## Portable package on Windows

```powershell
pwsh ./scripts/Publish-Portable.ps1 -SigningCertificatePath .\release-signing.pfx
```

The script tests the solution, publishes self-contained `win-x64` GUI and worker binaries, copies the operational documentation and notices, writes a relative-path SHA-256 manifest, and creates `artifacts/WinAcmeGui-<version>-win-x64.zip`.

Production packages must be Authenticode-signed with `-SigningCertificatePath` (and optionally `-SigningCertificatePassword`). `-AllowUnsigned` is reserved for CI/development validation and cannot pass the runtime worker trust boundary. The release downloader accepts only approved HTTPS GitHub hosts, official x64 assets with a SHA-256 digest and safe ZIP contents; it does not validate the downloaded `wacs.exe` Authenticode certificate.

Use the staging endpoint for first-run certificate acceptance tests. The GUI never silently overwrites an existing win-acme directory.

## Documentation

- [Portuguese user guide](docs/user-guide.pt-BR.md)
- [English user guide](docs/user-guide.en-US.md)
- [Troubleshooting / pt-BR](docs/troubleshooting.pt-BR.md)
- [Troubleshooting / English](docs/troubleshooting.en-US.md)
- [Compatibility and validation matrix](docs/compatibility.md)
