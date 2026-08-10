# User guide — win-acme GUI

## Before you start

- Use Windows 10/11 or Windows Server x64 as the operational target. The application is distributed as a portable `win-x64` ZIP.
- Keep `worker/WinAcmeGui.ElevatedWorker.exe` beside `WinAcmeGui.exe`.
- Use an Authenticode-signed package in production. `-AllowUnsigned` packages are for CI/development only.
- Use the staging endpoint to validate the integration before issuing production certificates.
- Use the header or Settings page to switch between English/Portuguese and the light/dark GUI theme; these preferences do not modify win-acme.

## Discover an installation

1. Open `WinAcmeGui.exe`.
2. At startup it searches related scheduled tasks, running processes, `PATH`, known locations and the application directory for `wacs.exe`.
3. Each candidate is validated with `wacs.exe --version`. The GUI resolves `settings.json`, the effective configuration path and the ACME endpoint without changing those files.
4. Select an executable manually when needed. Different installations, endpoints and configuration directories are never merged.
5. Invalid, unknown or shared-configuration renewals remain visible as diagnostic rows and cannot be changed.

## Operate renewals

Use the search box by friendly name, ID, domain or status.

- **Renew:** runs the selected renewal.
- **Force:** runs a forced renewal after an additional confirmation.
- **Cancel:** requires typing the exact friendly name.
- **Revoke:** requires the friendly name and should be used only for a compromised key.

Actions are disabled for read-only rows, without an active installation or while another operation is running. After a successful operation, the inventory is loaded again.

## Create a certificate

1. Open **New certificate**.
2. Enter one or more domains, an optional email, validation, key type and storage.
3. The current wizard supports the manual source, HTTP-01 or TLS-ALPN-01, RSA/EC keys and `certificatestore`, `pemfiles` or `pfxfile` storage.
4. PEM/PFX storage requires an absolute output path.
5. Accept the Let's Encrypt terms, review the preview and choose staging while testing.
6. Confirm execution. The operation uses the official `wacs.exe` and never edits renewal JSON directly.

DNS plugins, IIS sources, bindings, renewal edit/clone and automatic IIS installation are not exposed by the current shell. Use the win-acme console for those workflows.

## Download win-acme

The download action accepts only the approved official x64 path with a SHA-256 digest. On Windows, `wacs.exe` is Authenticode-checked before use; the ZIP also passes preflight checks for traversal, links, conflicts and unsafe content. The destination must be empty and is never silently overwritten.

## UAC, cancellation and security

System-changing operations go through the elevated worker using UAC, one operation at a time. The worker must exist, be signed and share the same trusted publisher as the GUI. Cancellation terminates the child process and waits for it to exit.

The GUI does not decrypt protected passwords, display secrets or expose unmasked operation output. See the [troubleshooting guide](troubleshooting.en-US.md) when a diagnostic code is shown.
