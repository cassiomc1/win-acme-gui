# Compatibility and validation matrix

This document separates the intended support target from the environments actually exercised by the repository. A green GitHub run is evidence for that runner; it is not evidence for every Windows edition or every optional integration.

## Intended target

| Scope | Position | Evidence currently available |
|---|---|---|
| Windows 10/11 x64 | Supported target | No dedicated OS matrix is committed yet. |
| Windows Server 2016+ x64 | Supported target | No dedicated Server 2016+ image is committed yet. |
| GitHub `windows-latest` | CI-verified | Restore, full cross-platform test suite, WPF/worker build, unsigned CI package and manifest/hash smoke test. |
| win-acme 2.2.x, trimmed | Primary compatibility target | Typed command and renewal-reader tests; official x64 release metadata. |
| win-acme 2.2.x, pluggable | Read/operate when the installed renewal is understood | Unknown source/plugin combinations stay diagnostic and read-only. |
| Unknown/future win-acme formats | Read-only fallback | Candidate can remain visible with version or parser diagnostics. |

The repository does not yet claim a full Windows 10, Windows 11 and Windows Server acceptance matrix. Real UAC, IIS, Scheduled Tasks, certificate-store, Authenticode runtime and staging-lifecycle behavior still require dedicated Windows acceptance evidence.

## Current GUI capability boundary

| Capability | Current state |
|---|---|
| Discovery and installation selection | Available; discovery is read-only. |
| Renewal inventory and filtering | Available; invalid, unknown and shared-configuration rows are diagnostic/read-only. |
| Renew, Force, Cancel and Revoke | Available for editable rows; confirmation rules apply. |
| Manual certificate creation | Available for HTTP-01/TLS-ALPN-01, RSA/EC and certificate-store/PFX/PEM output. |
| Official download | Available for the approved x64 release path, SHA-256 digest and safe ZIP checks. `wacs.exe` Authenticode is not validated by the downloader. |
| GUI appearance | Available; light/dark theme is local to the GUI and does not modify win-acme. |
| IIS source/bindings | Not exposed by the current shell. |
| DNS provider setup | Not exposed; configure provider plugins in win-acme. |
| Renewal edit/clone | Not exposed. |
| Scheduled-task management | Discovery may inspect task command lines; task health, recreation and manual-run controls are not exposed. |
| Settings editor/restore | Backup-first infrastructure exists, but no settings editor/restore screen is exposed. |
| Original win-acme log browser and diagnostic ZIP export | Not exposed by the current shell; the Activity page only shows this session's redacted operations. |

## Packaging and trust

The release target is a self-contained `win-x64` ZIP; a separately installed .NET runtime is not required. Production packages must carry Authenticode signatures for the GUI and elevated worker, and both files must share the trusted publisher. `-AllowUnsigned` is limited to CI/development smoke checks.

The package includes an x64 elevated worker and uses it through a hardened authenticated named pipe: the shared token travels only over the pipe (never the command line), the connected process identity is verified against the spawned worker before any request is sent, and responses are HMAC-authenticated with that token. The downloader accepts only approved HTTPS GitHub hosts, official x64 assets with a SHA-256 digest and safe ZIP contents; it does not validate the downloaded `wacs.exe` Authenticode certificate.

The certificate wizard currently exposes only the manual source with HTTP-01 and TLS-ALPN-01 self-hosting validation. Generic DNS provider flows remain outside the GUI because they depend on the provider plugin and its credentials.
