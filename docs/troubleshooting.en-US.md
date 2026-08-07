# Troubleshooting

- **No installation found:** use Select `wacs.exe`, check permissions and run `wacs.exe --version` in PowerShell.
- **Unreadable renewal:** preserve the original file, open the displayed path and inspect the log. Unknown formats stay read-only.
- **UAC rejected:** retry as administrator or fix task/store permissions; the GUI does not elevate the whole session silently.
- **Command failed:** inspect masked output and the original win-acme log. The exit code is preserved.
- **Download blocked:** use an empty destination and check the displayed source/integrity; select an official version manually if needed.
