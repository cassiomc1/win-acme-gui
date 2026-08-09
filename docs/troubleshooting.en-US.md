# Troubleshooting

- **No installation found:** use Select `wacs.exe`, check permissions and run `wacs.exe --version` in PowerShell.
- **Unreadable renewal:** preserve the original file, open the displayed path and inspect the log. Unknown formats stay read-only.
- **UAC rejected or untrusted worker:** accept the elevated worker prompt and use an Authenticode-signed production package whose GUI and worker share the same signer. The GUI elevates only the allowlisted operation; a missing, altered or untrusted worker is blocked.
- **Command failed:** inspect masked output and the original win-acme log. The exit code is preserved.
- **Download blocked:** check connectivity to the official GitHub hosts, use an empty destination and confirm the release SHA-256 digest and Authenticode signature. Redirects, unapproved hosts, missing digests, invalid signatures and unsafe ZIP contents are intentionally blocked.
