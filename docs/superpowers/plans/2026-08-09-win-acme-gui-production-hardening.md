# win-acme GUI Production Hardening Implementation Plan

**Status:** Completed in merged PR #1 (`81a1e46`). The repository CI and Windows acceptance workflow passed the solution tests, WPF/worker build, CI package and hash smoke test on `windows-latest`. This does not certify a full Windows 10/11/Server matrix or real UAC, IIS, Scheduled Tasks, certificate-store and staging-lifecycle acceptance.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the reviewed production blockers and deliver a safe, testable portable win-acme administration GUI with honest Windows validation boundaries.

**Architecture:** Keep Domain/Application platform-neutral and move all process, filesystem, download, Windows, and UAC behavior behind explicit interfaces. The WPF shell consumes application services, owns no command construction, and refreshes the active installation after every mutation. Invalid external documents remain represented as diagnostic rows instead of being discarded.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, FluentAssertions, `ProcessStartInfo.ArgumentList`, named-pipe UAC worker, `System.Text.Json`, `System.IO.Compression`, PowerShell packaging.

## Global Constraints

- Never edit `*.renewal.json` directly or expose/decrypt existing win-acme secrets.
- Every win-acme mutation goes through typed arguments and an explicit operation service.
- Discovery remains read-only and resolves the effective `Client.ConfigurationPath` and endpoint before inventory is shown.
- Invalid or unknown renewal documents remain visible with diagnostics and are not editable.
- Cancellation kills child processes and returns a distinct cancelled/timeout result.
- Downloads fail closed without approved HTTPS source, digest, safe ZIP preflight, and supported package metadata.
- Production claims are limited to validations actually run; WPF/UAC/IIS/Task Scheduler require a Windows acceptance run.

---

### Task 1: Correct command contracts and certificate execution

**Files:**
- Modify: `src/WinAcmeGui.Application/Certificates/CertificateDraft.cs`
- Modify: `src/WinAcmeGui.Application/Certificates/CertificateDraftValidator.cs`
- Modify: `src/WinAcmeGui.Application/Operations/WinAcmeCommandFactory.cs`
- Create: `src/WinAcmeGui.Application/Certificates/ManageCertificate.cs`
- Modify: `src/WinAcmeGui.App/Features/NewCertificateWindow.xaml.cs`
- Modify: `src/WinAcmeGui.App/Features/NewCertificateWindow.xaml`
- Test: `tests/WinAcmeGui.Application.Tests/Certificates/CertificateDraftValidatorTests.cs`
- Test: `tests/WinAcmeGui.Application.Tests/Certificates/CertificateCommandFactoryTests.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Certificates/ManageCertificateTests.cs`

- [x] Write failing tests proving supported validation modes map to valid win-acme plugin/mode tokens, unsupported generic DNS is rejected, and certificate execution invokes the runner.
- [x] Run the focused certificate tests and confirm they fail for the current invalid command/no-op execution.
- [x] Implement typed validation choices, official CLI mapping, runner-backed `ManageCertificate`, and a window callback that returns the real operation result.
- [x] Run the focused tests and then the full solution tests.

### Task 2: Resolve installations and preserve diagnostic inventory

**Files:**
- Modify: `src/WinAcmeGui.Application/Discovery/DiscoverInstallations.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Discovery/InstallationValidator.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Configuration/WinAcmeConfigurationReader.cs`
- Modify: `src/WinAcmeGui.Domain/Renewals/Renewal.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Renewals/RenewalDocumentReader.cs`
- Modify: `src/WinAcmeGui.Application/Inventory/InventoryService.cs`
- Modify: `src/WinAcmeGui.App/Shell/MainWindowViewModel.cs`
- Test: `tests/WinAcmeGui.Infrastructure.Tests/Configuration/WinAcmeConfigurationReaderTests.cs`
- Test: `tests/WinAcmeGui.Infrastructure.Tests/Renewals/RenewalDocumentReaderTests.cs`
- Test: `tests/WinAcmeGui.Application.Tests/Inventory/InventoryServiceTests.cs`
- Test: `tests/WinAcmeGui.Application.Tests/Discovery/DiscoverInstallationsTests.cs`

- [x] Add failing tests for effective configuration paths, cancellation propagation, malformed structure, unauthorized files, invalid-renewal visibility, and status derived from history.
- [x] Run those tests and verify each fails against the current behavior.
- [x] Implement one resolved installation snapshot, safe path normalization, tolerant renewal rows, explicit status/diagnostics, and cancellation-preserving exception handling.
- [x] Clear selected renewal on installation changes and expose the resolved configuration path/endpoint from the loaded snapshot.
- [x] Run focused and full tests.

### Task 3: Make operation execution safe and refreshable

**Files:**
- Modify: `src/WinAcmeGui.Infrastructure/Configuration/WinAcmeConfigurationReader.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Operations/WinAcmeProcessRunner.cs`
- Modify: `src/WinAcmeGui.Application/Renewals/ManageRenewal.cs`
- Modify: `src/WinAcmeGui.App/Shell/MainWindowViewModel.cs`
- Modify: `src/WinAcmeGui.App/Shell/MainWindow.xaml.cs`
- Test: `tests/WinAcmeGui.Infrastructure.Tests/Operations/WinAcmeProcessRunnerTests.cs`
- Test: `tests/WinAcmeGui.Application.Tests/Renewals/ManageRenewalTests.cs`

- [x] Add failing tests for cancellation/timeout child cleanup, concurrent stdout/stderr capture, absolute executable enforcement, and post-operation refresh hooks.
- [x] Run focused tests and confirm the failures.
- [x] Implement linked cancellation, safe process termination, thread-safe output collection, typed failures, confirmation policy for destructive/forced actions, and refresh after mutations.
- [x] Run focused and full tests.

### Task 4: Implement the elevated worker boundary

**Files:**
- Modify: `src/WinAcmeGui.ElevatedWorker/Operations/AllowlistedOperationDispatcher.cs`
- Modify: `src/WinAcmeGui.ElevatedWorker/Program.cs`
- Create: `src/WinAcmeGui.Application/Operations/IElevatedOperationClient.cs`
- Create: `src/WinAcmeGui.Infrastructure/Operations/NamedPipeElevatedOperationClient.cs`
- Modify: `src/WinAcmeGui.App/Shell/MainWindowViewModel.cs`
- Test: `tests/WinAcmeGui.ElevatedWorker.Tests/Operations/AllowlistedOperationDispatcherTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Operations/NamedPipeElevatedOperationClientTests.cs`

- [x] Add failing tests for path validation, operation-specific argument allowlists, protocol version/authentication rejection, and cancellation.
- [x] Run focused tests and confirm the failures.
- [x] Implement a per-operation authenticated named-pipe protocol, `runas` worker launch, strict operation/argument validation, and application integration for operations requiring administration.
- [x] Run focused and full tests; document that real UAC requires Windows acceptance.

### Task 5: Harden download, integrity, and extraction

**Files:**
- Modify: `src/WinAcmeGui.Infrastructure/Downloads/OfficialReleaseCatalog.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Downloads/OfficialReleaseClient.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Downloads/PackageVerifier.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Downloads/SafeZipExtractor.cs`
- Modify: `src/WinAcmeGui.Infrastructure/Downloads/WinAcmeDownloader.cs`
- Test: `tests/WinAcmeGui.Infrastructure.Tests/Downloads/OfficialReleaseCatalogTests.cs`
- Test: `tests/WinAcmeGui.Infrastructure.Tests/Downloads/SafeZipExtractorTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Downloads/WinAcmeDownloaderTests.cs`

- [x] Add failing tests for missing digest rejection, redirect host validation, streamed download limits, duplicate/partial extraction prevention, total size limits, and unsafe link metadata.
- [x] Run focused tests and confirm the failures.
- [x] Implement fail-closed release metadata, streamed temporary files, approved final responses, complete ZIP preflight, bounded extraction, and rollback cleanup.
- [x] Run focused and full tests.

### Task 6: Finish UI, localization, settings, diagnostics, and documentation

**Files:**
- Modify: `src/WinAcmeGui.App/Shell/MainWindow.xaml`
- Modify: `src/WinAcmeGui.App/Shell/MainWindow.xaml.cs`
- Modify: `src/WinAcmeGui.App/Shell/MainWindowViewModel.cs`
- Modify: `src/WinAcmeGui.App/Features/NewCertificateWindow.xaml`
- Modify: `src/WinAcmeGui.App/Features/NewCertificateWindow.xaml.cs`
- Modify: `src/WinAcmeGui.App/Localization/CultureService.cs`
- Modify: `docs/user-guide.pt-BR.md`
- Modify: `docs/user-guide.en-US.md`
- Modify: `docs/troubleshooting.pt-BR.md`
- Modify: `docs/troubleshooting.en-US.md`
- Modify: `README.md`

- [x] Add failing non-visual tests for resource parity, language switching, command confirmation policy, and invalid-renewal presentation.
- [x] Implement functional navigation states, complete bilingual labels, accessible names, operation progress/errors, diagnostics/settings wiring, and accurate status cards.
- [x] Update docs to describe only implemented operations and the Windows acceptance boundary.
- [x] Run tests and parse all XAML as XML.

### Task 7: Package and release verification

**Files:**
- Modify: `scripts/Publish-Portable.ps1`
- Modify: `scripts/Smoke-Test.ps1`
- Modify: `scripts/Test.ps1`
- Modify: `docs/compatibility.md`
- Modify: test project package references
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/windows-acceptance.yml`

- [x] Add failing script/config checks for package contents, deterministic version metadata, test dependency audit, and Windows target compilation.
- [x] Implement package verification, x64 release metadata, Windows-only acceptance hooks, and updated test dependencies without runtime vulnerability reports.
- [x] Run all available local verification, package checks that do not require Windows, and report unexecuted Windows-only checks explicitly.

### Task 8: Final verification and handoff

- [x] Run `dotnet restore WinAcmeGui.sln`.
- [x] Run `dotnet test WinAcmeGui.sln --configuration Release` and record the complete result.
- [x] Run `dotnet build WinAcmeGui.sln --configuration Release` and `git diff --check`.
- [x] Run dependency vulnerability/outdated checks and inspect the final diff/status.
- [x] Verify every reviewed finding is either fixed with a regression test or explicitly documented as Windows-only pending acceptance.
