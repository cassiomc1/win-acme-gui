# Troubleshooting

Preserve the original file first and copy the status code shown by the GUI. Do not place passwords, tokens, private keys or unmasked output in tickets.

## Discovery and inventory

- **No installation found:** use **Select `wacs.exe`**, check permissions and run `wacs.exe --version` in PowerShell.
- **`discovery.configuration.collision`:** multiple installations resolve to the same effective configuration. Keep one operational context; the others remain isolated/diagnostic.
- **`renewal.read_only`:** the row is invalid, unknown or uses shared configuration. Use the displayed path, correct the environment in win-acme and refresh the GUI.
- **`renewal.json.invalid`, `renewal.json.incomplete` or `renewal.file.unreadable`:** preserve the JSON, check permissions/syntax and inspect the original log. The GUI does not rewrite renewal JSON.
- **`renewal.plugin.unknown`:** the plugin is not understood by the GUI. Use the win-acme console; do not force a GUI mutation.
- **`renewal.directory.unreadable`:** check that the configuration path exists and the account can read it.

## Operations

| Code | Likely cause | Action |
|---|---|---|
| `process.start.notfound` | The executable or a dependency was not found | Revalidate the `wacs.exe` path and reinstall if needed. |
| `process.start.denied` | Access denied when starting the executable | Check file permissions and antivirus blocking. |
| `process.start.failed` | The executable could not be started for another reason | Revalidate `wacs.exe` and permissions. |
| `process.exit.nonzero` | win-acme exited with an error | Inspect masked output, exit code and the original log. |
| `operation.cancelled` | The user cancelled the operation | Confirm the process exited and refresh the inventory. |
| `operation.timeout` | The process exceeded its limit | Inspect the log and retry in staging. |
| `renewal.read_only` | The renewal is not editable | Correct the document/environment in win-acme. |
| `certificate.*` | Invalid wizard input | Correct domains, validation, key, storage, absolute path or terms. |

Cancel and Revoke require the exact friendly name. Revocation is for a compromised key, not an ordinary renewal.

## UAC and trust

- **`elevation.uac.rejected`:** accept the UAC prompt or use an authorized account.
- **`elevation.worker.missing`:** keep `worker/WinAcmeGui.ElevatedWorker.exe` beside the GUI.
- **`elevation.worker.untrusted`, `elevation.executable.untrusted` or `elevation.worker.publisher.mismatch`:** use an Authenticode-signed production package; the GUI and worker must share the same trusted publisher.
- **`elevation.worker.start.failed` or `elevation.worker.timeout`:** check permissions, antivirus, the worker path and Windows logs.
- **`elevation.operation.not_allowed`:** the operation or argument is outside the allowlist; do not bypass the block.
- **`elevation.protocol.*`:** the GUI and worker disagree about the protocol, or the connected process was not the elevated worker we started. Re-extract a complete package and do not mix `worker` folders from different versions.

## Download blocked

Check connectivity to the official GitHub hosts and use an empty destination. Redirects, unapproved hosts, missing or mismatched digests, invalid signatures, untrusted executables and unsafe ZIP contents are intentionally blocked. Do not disable these checks in production.

## Validation boundary

The Windows workflow validates tests, WPF/worker compilation, unsigned CI packaging and hashes on `windows-latest`. It does not replace dedicated acceptance on each Windows edition with UAC, IIS, Scheduled Tasks, certificate-store and staging-issuance scenarios.
