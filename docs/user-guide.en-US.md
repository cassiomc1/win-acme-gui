# Quick guide — win-acme GUI

1. Extract the portable ZIP to a writable folder.
2. Open `WinAcmeGui.exe`. On startup it searches scheduled tasks/processes, `PATH`, known locations and the application folder for `wacs.exe`.
3. Check version, endpoint, configuration path and loaded renewals. Discovery does not write to existing files.
4. Select `wacs.exe` to switch the active installation. Different installations and endpoints are never merged.
5. Select a renewal and use Renew, Force, Cancel or Revoke. Cancel and revoke require the friendly name; revoke is intended for a compromised key.
6. Use New certificate to review domains, validation, key, storage and staging before execution.

Use staging to validate the integration. win-acme-protected passwords are shown only as configured; the GUI never decrypts them.
