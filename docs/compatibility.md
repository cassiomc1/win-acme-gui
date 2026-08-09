# Compatibility matrix

| win-acme | Distribution | Discovery | Renewal inventory | Supported GUI mutations |
|---|---|---:|---:|---:|
| 2.2.x | trimmed | yes | yes, including diagnostic rows | renew, force, cancel, revoke, manual certificate |
| 2.2.x | pluggable | yes | yes, including diagnostic rows | same operations when the installed plugins support the requested command |
| unknown/future | any | version candidate | read-only when parsed | disabled until validated |

The certificate wizard currently exposes only the manual source with HTTP-01 and TLS-ALPN-01 self-hosting validation. Generic DNS provider flows remain outside the GUI because they depend on the provider plugin and its credentials.

Windows targets are Windows 10/11 x64 and Windows Server 2016+ x64. The production package must carry Authenticode signatures for the GUI and elevated worker; unsigned packages are limited to CI/development smoke checks. The package includes an x64 elevated worker and uses it through an authenticated named pipe; real UAC, IIS, Scheduled Tasks, Authenticode and certificate-store behavior require acceptance testing on Windows. IIS-dependent controls are unavailable with a diagnostic when IIS is not installed.
