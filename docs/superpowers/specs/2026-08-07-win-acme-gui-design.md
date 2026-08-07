# win-acme GUI — Design Specification

**Date:** 2026-08-07  
**Status:** Approved  
**Product:** Portable Windows desktop interface for win-acme

## 1. Purpose

Build a complete graphical administration interface for [win-acme](https://www.win-acme.com/) that automatically discovers existing installations and configuration, loads current renewals without modifying them, and exposes the supported certificate lifecycle through safe guided workflows.

The GUI is an administration layer over the official `wacs.exe`; it does not reimplement ACME. Existing environments remain usable from the win-acme console when the GUI is closed.

## 2. Supported Environment

- Windows 10 and Windows 11, x64.
- Windows Server 2016 or newer, x64.
- Portable, self-contained `win-x64` distribution; installing the .NET runtime is not required.
- WPF on .NET 8.
- Initial compatibility target: win-acme 2.2.x, with capability detection and safe read-only handling for unknown future formats.
- IIS is optional. Features that require IIS are hidden or disabled with an explanation when IIS is unavailable.
- Application languages: Portuguese (Brazil), `pt-BR`, and English (United States), `en-US`.

## 3. Product Principles

1. **Preserve existing systems.** Startup discovery and inventory are read-only.
2. **Use the official engine.** Supported mutations execute through `wacs.exe` command-line options instead of editing `*.renewal.json` directly.
3. **Elevate only when required.** Administrative operations use Windows UAC at the point of need.
4. **Make risk visible.** Destructive or production-affecting actions state their effect, require explicit confirmation, and create a recoverable backup where configuration files may change.
5. **Never expose secrets.** Secret values are neither displayed nor written to application logs. DPAPI-protected win-acme secrets are not decrypted by the GUI.
6. **Keep the console escape hatch.** The original command, with secrets masked, output, log location, and official documentation remain accessible.

## 4. Architecture

The solution uses WPF with MVVM. The presentation layer contains navigation, localized views, assistants, confirmations, and live operation output. An application layer coordinates use cases and exposes interfaces to focused Windows and win-acme adapters. The infrastructure layer handles process execution, filesystem reads, scheduled tasks, certificate stores, IIS detection, downloads, integrity checks, backups, and local preferences.

The UI never depends directly on JSON layout or Windows APIs. Domain models represent installations, endpoints, renewals, certificates, task health, capabilities, and operation results. Adapters translate external state into those models, allowing unit tests to run outside a configured server and integration tests to use deterministic fixtures.

## 5. Startup Discovery

Discovery runs asynchronously and reports progress. It evaluates these sources without performing an unbounded drive scan:

1. Executable and arguments in win-acme-related Windows scheduled tasks.
2. Running `wacs.exe` processes.
3. Executables resolvable through `PATH`.
4. Known locations under `%ProgramData%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`, the GUI directory, and paths previously selected in GUI preferences.
5. Manual selection when automatic discovery does not find the desired instance.

Each candidate is canonicalized, deduplicated, checked for accessibility, and validated with `wacs.exe --version` using a timeout. The GUI resolves its adjacent `settings.json`, `ClientName`, optional `ConfigurationPath`, and configured ACME endpoint. The default configuration path is `%ProgramData%\{ClientName}\{BaseUriHost}` when no override exists, consistent with the official [settings reference](https://www.win-acme.com/reference/settings).

Every `*.renewal.json` in the resolved configuration path is treated as a renewal, consistent with the official [renewal management documentation](https://www.win-acme.com/manual/renewal-management). Parsing is tolerant: known metadata and plugin configuration populate typed summaries while the source document remains untouched. Invalid or unknown documents stay visible with diagnostics and are not editable.

If multiple valid installations are found, the GUI lists them separately and requires one active context. Data from different installations or ACME endpoints is never merged. The selection is stored as a path reference, not as copied configuration.

## 6. Navigation and Features

The selected layout is a desktop administration center with persistent left navigation and guided assistants for complex operations.

### 6.1 Home

- Active installation, version, architecture, configuration path, and ACME endpoint.
- Counts for healthy, due soon, failed, expired, and unreadable renewals.
- Scheduled-task status and last/next run.
- Recent failures and expiring certificates.
- Contextual recommended actions.

### 6.2 Renewals

- Search, sort, and filters by status, source, validation, store, installation, and expiry.
- Details for identity, domains, plugin chain, certificate, history, errors, and relevant logs.
- Normal renewal, forced renewal, guided edit, clone, cancel, and revoke.
- Editing is implemented as a safe win-acme-supported recreation flow. The original remains recoverable until the replacement succeeds.
- Cancel and revoke remain distinct. Revocation includes a warning that it is intended for key compromise, matching official guidance.

### 6.3 New Certificate Assistant

The assistant builds an operation from detected capabilities:

1. Source: IIS or manual domains, plus source plugins exposed by the selected distribution.
2. Order strategy.
3. CSR/key type: RSA or EC where supported.
4. Validation: HTTP, DNS, or TLS and installed validation plugins.
5. Certificate store(s).
6. Installation action(s), including IIS bindings or scripts.
7. Review, production/staging selection, masked command preview, and execution.

The assistant validates dependencies between choices before execution. Plugin-specific fields are driven by a versioned capability catalog, augmented by safe CLI capability detection. Unsupported combinations are not emitted.

### 6.4 Installation

- View all discovered installations without merging them.
- Select the active installation and manually browse to `wacs.exe`.
- Download an official win-acme package, select architecture/distribution, verify source and integrity, and extract it to a user-selected folder.
- Check for updates and show release information; updates require explicit approval.
- Never silently replace an existing win-acme directory.

### 6.5 System

- IIS presence and accessibility.
- Local-machine certificate stores and certificate correlation.
- win-acme scheduled task status, health, recreation action, and manual run.
- ACME account and production/staging endpoint summary.
- Permissions and environment diagnostics.

### 6.6 Settings

- Structured editor for supported low-risk `settings.json` options.
- JSON changes preserve unknown properties and formatting where practical.
- A timestamped backup is created immediately before a settings write.
- Secret fields show only configured/not configured. Setting a secret uses win-acme-supported mechanisms; the GUI does not reveal existing values.
- GUI-only preferences live beside the portable executable when writable, with `%LocalAppData%\WinAcmeGui` as fallback.

### 6.7 Logs and Diagnostics

- Search and filter win-acme logs.
- Stream stdout and stderr for GUI-started commands.
- Operation status includes start time, duration, exit code, masked command, and resolved log path.
- Export a diagnostic ZIP containing application metadata, capability information, selected sanitized logs, and redacted configuration summaries. Private keys, passwords, vault payloads, account keys, and certificate cache contents are excluded.

### 6.8 About

- GUI and win-acme versions.
- Official documentation links.
- Open-source notices and licenses.

## 7. Localization and Accessibility

`pt-BR` is selected when the Windows UI culture is Portuguese; otherwise `en-US` is selected. The user may change language in Settings, and the choice persists. GUI messages, validation, assistants, confirmations, and documentation shipped with the app are localized. Raw `wacs.exe` output remains exact for diagnostic value and receives localized contextual labels when available.

All controls have keyboard navigation, accessible names, visible focus, sufficient contrast, scalable text, and status announcements. Light/dark mode follows Windows by default and can be overridden.

## 8. Command Execution and Elevation

Arguments are represented as typed tokens and passed with `ProcessStartInfo.ArgumentList`; commands are not assembled through a shell string. Sensitive tokens carry a redaction policy used by previews, logs, exceptions, and diagnostic exports.

Operations begin unelevated. When an operation requires administration, the GUI starts a small bundled elevated worker through `runas`. Requests and responses use a versioned, authenticated, per-operation local IPC channel. The worker accepts only allowlisted operations and typed arguments, never an arbitrary command line.

Timeout, cancellation, non-zero exit, missing executable, incompatible version, UAC rejection, locked file, and malformed output are distinct results with localized recovery guidance. After every mutating operation, the active installation is rescanned.

## 9. Download Security

The downloader uses only the official win-acme release source over TLS, follows a strict host allowlist, identifies architecture, downloads to a temporary file, and verifies the strongest integrity evidence officially published for that release. At minimum it validates ZIP structure and Authenticode signatures of signed binaries when present. The review screen shows version, distribution, architecture, source, destination, and verification result before extraction.

Extraction rejects absolute paths, parent traversal, links, and overwrite outside the selected directory. Existing non-empty destinations require a different folder or explicit versioned subfolder; silent overwrite is prohibited.

## 10. Error Handling and Recovery

- Partial discovery results remain usable when one source fails.
- Unreadable renewals remain visible with their file path and parser diagnostic.
- Unsupported future formats are read-only.
- Before a settings or configuration replacement, backups go to a GUI-managed timestamped folder with a manifest and restore action.
- Cancel, revoke, endpoint changes, forced renewal, task recreation, and replacement require action-specific confirmation.
- Revocation requires typing the renewal friendly name or an equivalent high-friction confirmation.
- A failed replacement does not cancel the original renewal.
- The GUI never presents a failed command as successful solely because files changed.

## 11. Testing Strategy

### Unit tests

- Candidate discovery, path canonicalization, and deduplication.
- Default and overridden configuration-path resolution.
- Tolerant renewal and settings parsing, unknown-property preservation, and malformed fixtures.
- Capability mapping and valid CLI token generation.
- Secret redaction across preview, logs, errors, and diagnostics.
- Confirmation policy and backup manifests.
- `pt-BR` and `en-US` resource completeness and culture selection.

### Integration tests

- Deterministic fake `wacs.exe` processes for version, list, renew, force, cancel, revoke, timeout, cancellation, and non-zero exits.
- Scheduled-task, certificate-store, IIS, filesystem, and download adapters behind testable interfaces.
- Multiple installations and endpoints with overlapping renewal names.
- Elevated-worker request validation and rejection of non-allowlisted operations.
- Safe ZIP extraction and corrupt/malicious package fixtures.

### Windows system tests

- Windows 10/11 and Windows Server 2016 or newer.
- With and without IIS.
- Standard user, administrator, accepted UAC, and rejected UAC.
- Existing win-acme 2.2.x configuration, custom `ConfigurationPath`, production and staging endpoints, scheduled task, and local certificates.

### Acceptance scenarios

1. Detect a real existing installation and inventory it without changing timestamps or content.
2. Keep two installations isolated and switch active context safely.
3. Create a staging certificate, renew it, perform a safe guided edit, cancel it, and revoke only after reinforced confirmation.
4. Download or manually select win-acme.
5. Switch between `pt-BR` and `en-US`, restart, and retain preference.
6. Export a diagnostic package verified to contain no secrets or private keys.

## 12. Packaging and Documentation

Release output is a ZIP containing the self-contained WPF executable, elevated worker, dependencies, notices, and localized quick-start documentation. The application does not register itself, create a Windows service, or require an installer. A user-created shortcut is optional.

Documentation covers discovery, multiple installations, certificate workflows, UAC, staging, backups/restoration, logs, troubleshooting, security boundaries, and a win-acme-version capability matrix.

## 13. Explicit Non-Goals

- Reimplementing ACME or win-acme plugins.
- Editing renewal JSON by hand.
- Decrypting or displaying existing secrets.
- Silent certificate issuance, revocation, update, or installation replacement.
- Background resident service, telemetry, cloud account, or remote server management.
- ARM64/x86 packages in the first release.
- Languages other than `pt-BR` and `en-US` in the first release.

## 14. Completion Criteria

The feature is complete when the acceptance scenarios pass on the supported Windows matrix, automated tests are green, the portable ZIP runs without a separately installed .NET runtime, existing configurations remain unchanged during discovery, all GUI-owned text is available in both languages, and the documentation and compatibility matrix are included.
