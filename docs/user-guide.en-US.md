# Quick guide — win-acme GUI

1. Extract the portable ZIP to a writable folder and keep the `worker` directory beside `WinAcmeGui.exe`.
2. Open `WinAcmeGui.exe`. On startup it searches scheduled tasks/processes, `PATH`, known locations and the application folder for `wacs.exe`.
3. Check version, endpoint, effective configuration path and loaded renewals. Discovery is read-only; invalid files remain visible as diagnostic rows instead of being discarded.
4. Select `wacs.exe` to switch the active installation. Different installations, endpoints and configuration directories are never merged.
5. Use the search box by friendly name, ID, domain or status. Renew and Force operate on the selected renewal; Force requires an additional confirmation. Cancel and Revoke require the friendly name; Revoke is intended only for a compromised key. Unreadable, unknown or shared-configuration rows remain read-only.
6. Use New certificate to review domains, optional email, validation, key, storage and staging before execution. PEM/PFX storage requires an absolute output path. Explicitly accept the Let's Encrypt terms; the wizard supports the manual source and HTTP-01 or TLS-ALPN-01, but it does not configure DNS plugins.
7. Mutating operations are sent to the elevated worker through UAC, one operation at a time. Cancellation terminates the process and waits for it to exit. If UAC is rejected, the worker is missing or not trusted/signed, the operation fails without executing an arbitrary process.

Use staging to validate the integration. The built-in downloader accepts only the official x64 release with a SHA-256 digest, verifies the `wacs.exe` Authenticode signature and performs safe extraction into an empty folder. A production package must be signed by the publishing process; `-AllowUnsigned` is for CI/development only. win-acme-protected passwords are shown only as configured; the GUI never decrypts them.
