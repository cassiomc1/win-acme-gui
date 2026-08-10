# win-acme GUI Implementation Plan

> **Status:** Historical and superseded. This original 14-task plan describes the broader target and intentionally retains its unchecked design steps; it is not a current implementation tracker. See [`README.md`](../../../README.md), [`docs/compatibility.md`](../../compatibility.md) and the [completed production hardening record](2026-08-09-win-acme-gui-production-hardening.md) for the current boundary and validation evidence.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete, bilingual, portable Windows desktop administration interface that discovers existing win-acme installations and safely manages their full certificate lifecycle through the official engine.

**Architecture:** A .NET 8 WPF MVVM application depends on platform-neutral Domain and Application projects. Focused Infrastructure adapters read win-acme and Windows state, execute typed commands, and expose only domain models. A minimal allowlisted elevated worker handles operations requiring UAC; all external data and operation results cross explicit interfaces that can be tested with fixtures.

**Tech Stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm 8.x, Microsoft.Extensions.DependencyInjection 8.x, Microsoft.Extensions.Hosting 8.x, System.Text.Json, xUnit 2.x, FluentAssertions 6.x, Microsoft.NET.Test.Sdk, coverlet.collector, PowerShell 5.1/7 for Windows smoke tests.

## Global Constraints

- Target Windows 10/11 x64 and Windows Server 2016 or newer x64.
- Publish as portable self-contained `win-x64`; no separately installed .NET runtime.
- Initial win-acme compatibility target is 2.2.x; unsupported future formats are visible but read-only.
- Startup discovery and inventory must not mutate existing files, tasks, certificates, or IIS state.
- Mutations use official `wacs.exe` options; never edit `*.renewal.json` directly.
- Never decrypt or display existing secrets; redact sensitive arguments, logs, errors, and diagnostic exports.
- GUI languages are exactly `pt-BR` and `en-US` for the first release.
- Do not add telemetry, a resident service, cloud accounts, or remote server management.
- Every production behavior begins with a failing automated test and follows red-green-refactor.
- Use nullable reference types, treat warnings as errors, and keep files focused on one responsibility.

## Planned File Map

```text
WinAcmeGui.sln
Directory.Build.props
Directory.Packages.props
src/
  WinAcmeGui.Domain/                 immutable models, enums, value objects
  WinAcmeGui.Application/            use cases and ports
  WinAcmeGui.Infrastructure/         win-acme/Windows/filesystem/download adapters
  WinAcmeGui.App/                    WPF shell, pages, view models, localization
  WinAcmeGui.ElevatedWorker/         allowlisted UAC worker
tests/
  WinAcmeGui.Domain.Tests/
  WinAcmeGui.Application.Tests/
  WinAcmeGui.Infrastructure.Tests/
  WinAcmeGui.App.Tests/
  WinAcmeGui.ElevatedWorker.Tests/
  Fixtures/                          sanitized settings, renewals, logs, fake wacs
scripts/
  Test.ps1
  Publish-Portable.ps1
  Smoke-Test.ps1
docs/
  user-guide.pt-BR.md
  user-guide.en-US.md
  troubleshooting.pt-BR.md
  troubleshooting.en-US.md
  compatibility.md
```

---

### Task 1: Solution Skeleton and Domain Contracts

**Files:**
- Create: `WinAcmeGui.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `src/WinAcmeGui.Domain/WinAcmeGui.Domain.csproj`
- Create: `src/WinAcmeGui.Domain/Installations/WinAcmeInstallation.cs`
- Create: `src/WinAcmeGui.Domain/Renewals/Renewal.cs`
- Create: `src/WinAcmeGui.Domain/Operations/OperationModels.cs`
- Create: `tests/WinAcmeGui.Domain.Tests/WinAcmeGui.Domain.Tests.csproj`
- Create: `tests/WinAcmeGui.Domain.Tests/Installations/WinAcmeInstallationTests.cs`
- Create: `scripts/Test.ps1`

**Interfaces:**
- Produces: `WinAcmeInstallation`, `WinAcmeVersion`, `AcmeEndpoint`, `Renewal`, `RenewalStatus`, `OperationRequest`, `OperationResult`, and `SensitiveArgument`.
- Consumes: no application code.

- [ ] **Step 1: Create the solution and test project only**

Run:

```powershell
dotnet new sln -n WinAcmeGui
dotnet new classlib -n WinAcmeGui.Domain -o src/WinAcmeGui.Domain -f net8.0
dotnet new xunit -n WinAcmeGui.Domain.Tests -o tests/WinAcmeGui.Domain.Tests -f net8.0
dotnet sln add src/WinAcmeGui.Domain tests/WinAcmeGui.Domain.Tests
dotnet add tests/WinAcmeGui.Domain.Tests reference src/WinAcmeGui.Domain
```

Set common properties in `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Write failing domain invariant tests**

```csharp
public sealed class WinAcmeInstallationTests
{
    [Fact]
    public void Create_rejects_relative_executable_path() =>
        FluentActions.Invoking(() => WinAcmeInstallation.Create(
            "wacs.exe", new WinAcmeVersion(2, 2, 9, 1), @"C:\ProgramData\win-acme", AcmeEndpoint.Production))
        .Should().Throw<ArgumentException>();

    [Fact]
    public void Sensitive_argument_never_reveals_value_in_display_text()
    {
        var argument = SensitiveArgument.Secret("--pfxpassword", "correct horse");
        argument.DisplayValue.Should().Be("••••••••");
        argument.Value.Should().Be("correct horse");
    }
}
```

- [ ] **Step 3: Run the tests and verify the expected failure**

Run: `dotnet test tests/WinAcmeGui.Domain.Tests/WinAcmeGui.Domain.Tests.csproj --no-restore`

Expected: compilation fails because `WinAcmeInstallation` and `SensitiveArgument` do not exist.

- [ ] **Step 4: Implement the minimum immutable domain types**

```csharp
public sealed record WinAcmeInstallation(
    string ExecutablePath,
    WinAcmeVersion Version,
    string ConfigurationPath,
    AcmeEndpoint Endpoint)
{
    public static WinAcmeInstallation Create(string executablePath, WinAcmeVersion version,
        string configurationPath, AcmeEndpoint endpoint)
    {
        if (!Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("Executable path must be absolute.", nameof(executablePath));
        return new(Path.GetFullPath(executablePath), version, Path.GetFullPath(configurationPath), endpoint);
    }
}

public sealed record SensitiveArgument(string Name, string Value, bool IsSecret)
{
    public string DisplayValue => IsSecret ? "••••••••" : Value;
    public static SensitiveArgument Plain(string name, string value) => new(name, value, false);
    public static SensitiveArgument Secret(string name, string value) => new(name, value, true);
}
```

Define the remaining contracts in the same domain files:

```csharp
public sealed record WinAcmeVersion(int Major, int Minor, int Build, int Revision);
public sealed record AcmeEndpoint(Uri BaseUri, bool IsProduction)
{
    public static AcmeEndpoint Production { get; } =
        new(new Uri("https://acme-v02.api.letsencrypt.org/"), true);
}
public enum RenewalStatus { Healthy, DueSoon, Failed, Expired, Unreadable }
public sealed record Renewal(
    string Id, string FriendlyName, IReadOnlyList<string> Domains,
    RenewalStatus Status, bool IsEditable, string SourcePath);
public enum OperationStatus { Succeeded, Failed, Cancelled, TimedOut }
public sealed record OperationRequest(
    string OperationId, string ExecutablePath, IReadOnlyList<SensitiveArgument> Arguments);
public sealed record OperationResult(
    OperationStatus Status, int? ExitCode, TimeSpan Duration,
    IReadOnlyList<string> Output, string? ErrorCode);
```

- [ ] **Step 5: Add the repository test entry point and verify green**

`scripts/Test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore "$PSScriptRoot/../WinAcmeGui.sln"
dotnet test "$PSScriptRoot/../WinAcmeGui.sln" --no-restore --configuration Release
```

Run: `pwsh ./scripts/Test.ps1`

Expected: all domain tests pass with zero warnings.

- [ ] **Step 6: Commit**

```bash
git add WinAcmeGui.sln Directory.Build.props Directory.Packages.props src tests scripts/Test.ps1
git commit -m "build: establish solution and domain contracts"
```

---

### Task 2: Tolerant Settings and Renewal Readers

**Files:**
- Create: `src/WinAcmeGui.Application/WinAcmeGui.Application.csproj`
- Create: `src/WinAcmeGui.Application/Configuration/IWinAcmeConfigurationReader.cs`
- Create: `src/WinAcmeGui.Infrastructure/WinAcmeGui.Infrastructure.csproj`
- Create: `src/WinAcmeGui.Infrastructure/Configuration/WinAcmeConfigurationReader.cs`
- Create: `src/WinAcmeGui.Infrastructure/Configuration/ConfigurationPathResolver.cs`
- Create: `src/WinAcmeGui.Infrastructure/Renewals/RenewalDocumentReader.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Configuration/WinAcmeConfigurationReaderTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Renewals/RenewalDocumentReaderTests.cs`
- Create: `tests/Fixtures/Settings/default.json`
- Create: `tests/Fixtures/Settings/custom-path.json`
- Create: `tests/Fixtures/Renewals/manual-http.renewal.json`
- Create: `tests/Fixtures/Renewals/unknown-plugin.renewal.json`
- Create: `tests/Fixtures/Renewals/malformed.renewal.json`

**Interfaces:**
- Produces: `IWinAcmeConfigurationReader.ReadAsync(string executablePath, CancellationToken)`, `ConfigurationSnapshot`, and `RenewalReadResult`.
- Consumes: Task 1 domain models.

- [ ] **Step 1: Write failing path-resolution and tolerant-reader tests**

```csharp
[Fact]
public async Task Uses_configuration_path_override_without_mutating_source()
{
    var before = await File.ReadAllBytesAsync(_fixture.SettingsPath);
    var result = await _reader.ReadAsync(_fixture.WacsPath, CancellationToken.None);
    var after = await File.ReadAllBytesAsync(_fixture.SettingsPath);

    result.ConfigurationPath.Should().Be(@"D:\AcmeConfig");
    after.Should().Equal(before);
}

[Fact]
public async Task Unknown_plugin_is_visible_but_not_editable()
{
    var result = await _reader.ReadAsync(_fixture.UnknownPluginRenewal, CancellationToken.None);
    result.IsReadable.Should().BeTrue();
    result.IsEditable.Should().BeFalse();
    result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.plugin.unknown");
}

[Fact]
public async Task Malformed_renewal_returns_diagnostic_instead_of_throwing()
{
    var result = await _reader.ReadAsync(_fixture.MalformedRenewal, CancellationToken.None);
    result.IsReadable.Should().BeFalse();
    result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.json.invalid");
}
```

- [ ] **Step 2: Run targeted tests and verify red**

Run: `dotnet test tests/WinAcmeGui.Infrastructure.Tests --filter "Configuration|RenewalDocument"`

Expected: compilation fails for missing readers.

- [ ] **Step 3: Implement JSON parsing without writes**

Use `JsonDocument` to extract known fields while keeping a cloned root element for diagnostics and future rendering:

```csharp
public async Task<RenewalReadResult> ReadAsync(string path, CancellationToken cancellationToken)
{
    try
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return _mapper.Map(path, document.RootElement);
    }
    catch (JsonException ex)
    {
        return RenewalReadResult.Invalid(path,
            Diagnostic.Error("renewal.json.invalid", ex.Message));
    }
}
```

Resolve a null `ConfigurationPath` from `%ProgramData%\{ClientName}\{BaseUriHost}` and JSON-decode explicit paths. Do not normalize by writing `settings.json`.

- [ ] **Step 4: Test source immutability and all fixtures**

Run: `dotnet test tests/WinAcmeGui.Infrastructure.Tests --filter "Configuration|RenewalDocument" --configuration Release`

Expected: all tests pass; fixture byte hashes are unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/WinAcmeGui.Application src/WinAcmeGui.Infrastructure tests/WinAcmeGui.Infrastructure.Tests tests/Fixtures
git commit -m "feat: read existing win-acme configuration safely"
```

---

### Task 3: Installation Discovery and Isolation

**Files:**
- Create: `src/WinAcmeGui.Application/Discovery/IInstallationCandidateSource.cs`
- Create: `src/WinAcmeGui.Application/Discovery/DiscoverInstallations.cs`
- Create: `src/WinAcmeGui.Infrastructure/Discovery/ScheduledTaskCandidateSource.cs`
- Create: `src/WinAcmeGui.Infrastructure/Discovery/ProcessCandidateSource.cs`
- Create: `src/WinAcmeGui.Infrastructure/Discovery/PathCandidateSource.cs`
- Create: `src/WinAcmeGui.Infrastructure/Discovery/KnownLocationCandidateSource.cs`
- Create: `src/WinAcmeGui.Infrastructure/Discovery/InstallationValidator.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Discovery/DiscoverInstallationsTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Discovery/InstallationValidatorTests.cs`
- Create: `tests/Fixtures/FakeWacs/FakeWacs.csproj`
- Create: `tests/Fixtures/FakeWacs/Program.cs`

**Interfaces:**
- Produces: `DiscoverInstallations.ExecuteAsync(IProgress<DiscoveryProgress>, CancellationToken)` returning `DiscoveryResult` with isolated `InstallationInventory` items.
- Consumes: `IWinAcmeConfigurationReader` and Task 1 models.

- [ ] **Step 1: Write failing discovery tests**

```csharp
[Fact]
public async Task Deduplicates_candidates_by_canonical_executable_path()
{
    var sources = new[]
    {
        CandidateSource.Returning(@"C:\Tools\wacs.exe"),
        CandidateSource.Returning(@"C:\Tools\.\wacs.exe")
    };
    var result = await CreateUseCase(sources).ExecuteAsync(null, CancellationToken.None);
    result.Installations.Should().ContainSingle();
}

[Fact]
public async Task Keeps_same_renewal_name_isolated_between_installations()
{
    var result = await CreateUseCase(TwoValidInstallationsWithRenewal("example.com"))
        .ExecuteAsync(null, CancellationToken.None);
    result.Installations.Should().HaveCount(2);
    result.Installations.Should().OnlyContain(i => i.Renewals.Single().FriendlyName == "example.com");
}

[Fact]
public async Task Returns_partial_results_when_one_source_fails()
{
    var result = await CreateUseCase(CandidateSource.Throwing(), CandidateSource.Returning(ValidWacs))
        .ExecuteAsync(null, CancellationToken.None);
    result.Installations.Should().ContainSingle();
    result.Diagnostics.Should().Contain(x => x.Code == "discovery.source.failed");
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Application.Tests --filter DiscoverInstallations`

Expected: compilation fails because discovery ports and use case are absent.

- [ ] **Step 3: Implement bounded concurrent discovery**

```csharp
public async Task<DiscoveryResult> ExecuteAsync(IProgress<DiscoveryProgress>? progress,
    CancellationToken cancellationToken)
{
    var sourceResults = await Task.WhenAll(_sources.Select(source =>
        ReadSourceSafely(source, progress, cancellationToken)));
    var paths = sourceResults.SelectMany(x => x.Paths)
        .Distinct(_canonicalPathComparer);
    var validations = await Task.WhenAll(paths.Select(path =>
        _validator.ValidateAsync(path, cancellationToken)));
    return DiscoveryResult.From(sourceResults, validations);
}
```

Known locations must be explicit and depth-bounded. Do not recursively scan a drive. Validate candidates with `--version`, a 10-second timeout, and captured output.

- [ ] **Step 4: Implement deterministic FakeWacs modes**

`FakeWacs` accepts `--fake-mode version-ok|version-invalid|timeout|exit-error` before ordinary arguments. For `version-ok`, print `win-acme.v2.2.9.1` and exit `0`; for timeout, wait until cancellation/termination; for exit-error, write a fixed message to stderr and exit `7`.

- [ ] **Step 5: Verify discovery tests**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Discovery|InstallationValidator" --configuration Release`

Expected: all tests pass, including partial results and timeout termination.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: discover and isolate win-acme installations"
```

---

### Task 4: Typed Command Builder, Runner, and Secret Redaction

**Files:**
- Create: `src/WinAcmeGui.Application/Operations/IWinAcmeRunner.cs`
- Create: `src/WinAcmeGui.Application/Operations/WinAcmeCommandFactory.cs`
- Create: `src/WinAcmeGui.Infrastructure/Operations/WinAcmeProcessRunner.cs`
- Create: `src/WinAcmeGui.Infrastructure/Operations/OutputRedactor.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Operations/WinAcmeCommandFactoryTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Operations/WinAcmeProcessRunnerTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Operations/OutputRedactorTests.cs`

**Interfaces:**
- Produces: `WinAcmeCommandFactory.CreateRenew/Create/Cancel/Revoke/List`, `IWinAcmeRunner.RunAsync(WinAcmeCommand, IProgress<OperationOutput>, CancellationToken)`.
- Consumes: `SensitiveArgument`, `OperationResult`, and a validated installation.

- [ ] **Step 1: Write failing command and redaction tests**

```csharp
[Fact]
public void Forced_renewal_uses_tokens_not_shell_text()
{
    var command = _factory.CreateRenew("renewal-id", force: true);
    command.Arguments.Select(x => x.Value).Should().Equal("--renew", "--id", "renewal-id", "--force");
}

[Fact]
public void Preview_masks_secret_and_preserves_non_secret_arguments()
{
    var command = new WinAcmeCommand(new[] {
        SensitiveArgument.Plain("--source", "manual"),
        SensitiveArgument.Secret("--pfxpassword", "S3cret!") });
    command.DisplayText.Should().Contain("--source manual").And.Contain("--pfxpassword ••••••••");
    command.DisplayText.Should().NotContain("S3cret!");
}

[Fact]
public async Task Cancellation_kills_process_tree_and_returns_cancelled()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
    var result = await _runner.RunAsync(FakeWacs.Command("timeout"), null, cts.Token);
    result.Status.Should().Be(OperationStatus.Cancelled);
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Command|Runner|Redactor"`

Expected: missing type failures.

- [ ] **Step 3: Implement shell-free execution and streaming**

```csharp
var startInfo = new ProcessStartInfo(command.ExecutablePath)
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
    WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath)!
};
foreach (var argument in command.Arguments)
{
    startInfo.ArgumentList.Add(argument.Name);
    if (argument.Value.Length > 0) startInfo.ArgumentList.Add(argument.Value);
}
```

Read stdout and stderr asynchronously, publish timestamped lines, apply redaction before storing any line, enforce operation timeout, and call `process.Kill(entireProcessTree: true)` on cancellation.

- [ ] **Step 4: Verify all secret surfaces and process outcomes**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Command|Runner|Redactor" --configuration Release`

Expected: success, non-zero exit, timeout, cancellation, and secret-leak tests pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: execute typed win-acme commands safely"
```

---

### Task 5: Inventory Correlation with Windows State

**Files:**
- Create: `src/WinAcmeGui.Application/Inventory/InventoryService.cs`
- Create: `src/WinAcmeGui.Application/System/IWindowsSystemProbe.cs`
- Create: `src/WinAcmeGui.Infrastructure/Windows/ScheduledTaskProbe.cs`
- Create: `src/WinAcmeGui.Infrastructure/Windows/CertificateStoreProbe.cs`
- Create: `src/WinAcmeGui.Infrastructure/Windows/IisProbe.cs`
- Create: `src/WinAcmeGui.Infrastructure/Logs/WinAcmeLogReader.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Inventory/InventoryServiceTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Logs/WinAcmeLogReaderTests.cs`
- Create: `tests/Fixtures/Logs/renewal-success.log`
- Create: `tests/Fixtures/Logs/renewal-failure.log`

**Interfaces:**
- Produces: `InventoryService.LoadAsync(WinAcmeInstallation, CancellationToken)` returning `InstallationInventory`; `IWindowsSystemProbe` returning `ScheduledTaskHealth`, `CertificateSummary`, and `IisSummary`.
- Consumes: configuration readers, renewal readers, log reader, and Windows probe ports.

- [ ] **Step 1: Write failing status-correlation tests**

```csharp
[Theory]
[InlineData(40, false, RenewalStatus.Healthy)]
[InlineData(5, false, RenewalStatus.DueSoon)]
[InlineData(-1, false, RenewalStatus.Expired)]
[InlineData(40, true, RenewalStatus.Failed)]
public async Task Computes_actionable_status(int daysRemaining, bool lastAttemptFailed, RenewalStatus expected)
{
    var inventory = await CreateService(daysRemaining, lastAttemptFailed)
        .LoadAsync(TestInstallation, CancellationToken.None);
    inventory.Renewals.Single().Status.Should().Be(expected);
}

[Fact]
public async Task Loading_inventory_does_not_invoke_mutating_probe_methods()
{
    var probe = new RecordingSystemProbe();
    await CreateService(probe).LoadAsync(TestInstallation, CancellationToken.None);
    probe.Mutations.Should().BeEmpty();
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Application.Tests --filter InventoryService`

Expected: compile failure for missing inventory service.

- [ ] **Step 3: Implement read-only probes and status policy**

Use `Microsoft.Win32.TaskScheduler` only if its license and supported Windows range are accepted; otherwise invoke Task Scheduler COM interfaces from a focused adapter. Read `LocalMachine` certificate stores with `OpenFlags.ReadOnly`. Detect IIS through `Microsoft.Web.Administration` loaded only on Windows with IIS installed. Return `Unavailable` diagnostics rather than throwing when an optional subsystem is absent.

```csharp
var status = lastFailure is not null ? RenewalStatus.Failed
    : certificate.NotAfter <= now ? RenewalStatus.Expired
    : certificate.NotAfter <= now.AddDays(policy.DueSoonDays) ? RenewalStatus.DueSoon
    : RenewalStatus.Healthy;
```

- [ ] **Step 4: Verify adapters on fixtures and application correlation**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Inventory|LogReader" --configuration Release`

Expected: all tests pass; optional IIS absence is not a test failure.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: correlate renewals with Windows system health"
```

---

### Task 6: Safe Settings Writes, Backups, and Diagnostics Export

**Files:**
- Create: `src/WinAcmeGui.Application/Configuration/UpdateSettings.cs`
- Create: `src/WinAcmeGui.Application/Diagnostics/ExportDiagnostics.cs`
- Create: `src/WinAcmeGui.Infrastructure/Backups/BackupService.cs`
- Create: `src/WinAcmeGui.Infrastructure/Configuration/WinAcmeSettingsWriter.cs`
- Create: `src/WinAcmeGui.Infrastructure/Diagnostics/DiagnosticExporter.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Configuration/UpdateSettingsTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Backups/BackupServiceTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Diagnostics/DiagnosticExporterTests.cs`

**Interfaces:**
- Produces: `UpdateSettings.ExecuteAsync(SettingsPatch, Confirmation, CancellationToken)`, `IBackupService.CreateAsync`, `ExportDiagnostics.ExecuteAsync`.
- Consumes: settings snapshot, redaction service, filesystem abstraction.

- [ ] **Step 1: Write failing preservation, atomicity, and leak tests**

```csharp
[Fact]
public async Task Update_preserves_unknown_json_properties_and_creates_backup_first()
{
    var result = await _useCase.ExecuteAsync(new SettingsPatch { PageSize = 100 }, Confirmed, None);
    result.BackupPath.Should().NotBeNull();
    var updated = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath))!.AsObject();
    updated["FutureOption"].Should().NotBeNull();
    updated["UI"]!["PageSize"]!.GetValue<int>().Should().Be(100);
}

[Fact]
public async Task Diagnostic_zip_contains_no_known_secret_or_private_key()
{
    var zip = await _exporter.ExportAsync(_requestContainingSecrets, None);
    var allText = await ZipTestReader.ReadAllText(zip);
    allText.Should().NotContain("correct horse").And.NotContain("PRIVATE KEY");
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Settings|Backup|Diagnostic"`

Expected: missing use cases and adapters.

- [ ] **Step 3: Implement backup-first atomic settings update**

Write a manifest containing source path, SHA-256 before/after, timestamp, GUI version, and operation ID. Write updated JSON to a sibling temporary file, flush it, then use `File.Replace` when available or a same-volume rename sequence. On failure, leave the original and backup intact.

Patch only allowlisted paths such as `UI.PageSize`, `UI.DateFormat`, scheduled-task timing, logging, and notification non-secret fields. Reject renewal files and secret-property writes at this layer.

- [ ] **Step 4: Implement diagnostic allowlist**

Include only manifest, sanitized installation summary, sanitized settings summary, selected redacted log slices, and task/IIS/certificate metadata without private key material. Reject files outside the allowlist even if requested by the caller.

- [ ] **Step 5: Verify green and rollback behavior**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Settings|Backup|Diagnostic" --configuration Release`

Expected: preservation, backup ordering, simulated write failure, restore, and no-secret assertions pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: protect settings changes and diagnostic exports"
```

---

### Task 7: Secure Official Download and Extraction

**Files:**
- Create: `src/WinAcmeGui.Application/Installations/DownloadWinAcme.cs`
- Create: `src/WinAcmeGui.Infrastructure/Downloads/OfficialReleaseClient.cs`
- Create: `src/WinAcmeGui.Infrastructure/Downloads/PackageVerifier.cs`
- Create: `src/WinAcmeGui.Infrastructure/Downloads/SafeZipExtractor.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Installations/DownloadWinAcmeTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Downloads/SafeZipExtractorTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Downloads/PackageVerifierTests.cs`
- Create: `tests/Fixtures/Packages/Create-MaliciousFixtures.ps1`

**Interfaces:**
- Produces: `DownloadWinAcme.GetReleaseOptionsAsync`, `DownloadWinAcme.ExecuteAsync(ApprovedDownload, IProgress<DownloadProgress>, CancellationToken)`.
- Consumes: official release client, verifier, extractor, and installation validator.

- [ ] **Step 1: Write failing host and extraction safety tests**

```csharp
[Theory]
[InlineData("../escape.exe")]
[InlineData("/absolute/wacs.exe")]
[InlineData("C:\\outside\\wacs.exe")]
public async Task Rejects_entries_outside_destination(string entryName)
{
    var archive = ZipFixture.WithEntry(entryName, "payload");
    var act = () => _extractor.ExtractAsync(archive, _destination, None);
    await act.Should().ThrowAsync<UnsafeArchiveException>();
}

[Fact]
public async Task Rejects_download_redirect_to_non_allowlisted_host()
{
    var act = () => _client.DownloadAsync(new Uri("https://evil.example/wacs.zip"), _target, None);
    await act.Should().ThrowAsync<ReleaseSourceException>();
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Infrastructure.Tests --filter "Download|Package|Zip"`

Expected: missing downloader components.

- [ ] **Step 3: Implement allowlisted release metadata and verification**

Keep the source host allowlist in a versioned policy class and test it explicitly. Require HTTPS, cap redirects, download into a unique temp directory, calculate SHA-256, validate central-directory limits, and verify Authenticode for signed executable files using `WinVerifyTrust` on Windows. Record exactly which integrity checks passed.

- [ ] **Step 4: Implement safe extraction and destination rules**

For each entry, calculate `Path.GetFullPath(Path.Combine(destination, entry.FullName))` and require it to start with the normalized destination plus directory separator using `OrdinalIgnoreCase`. Reject rooted paths, `..`, links/reparse points, excessive uncompressed size, duplicate canonical paths, and extraction into a non-empty directory unless the approved request names a new versioned subdirectory.

- [ ] **Step 5: Verify green including corrupt and oversized archives**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.Infrastructure.Tests --filter "Download|Package|Zip" --configuration Release`

Expected: all release-policy and malicious-archive tests pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: download and extract official win-acme safely"
```

---

### Task 8: Elevated Worker with Allowlisted IPC

**Files:**
- Create: `src/WinAcmeGui.Application/Elevation/IElevatedOperationClient.cs`
- Create: `src/WinAcmeGui.ElevatedWorker/WinAcmeGui.ElevatedWorker.csproj`
- Create: `src/WinAcmeGui.ElevatedWorker/Program.cs`
- Create: `src/WinAcmeGui.ElevatedWorker/Ipc/ElevatedProtocol.cs`
- Create: `src/WinAcmeGui.ElevatedWorker/Ipc/NamedPipeServer.cs`
- Create: `src/WinAcmeGui.ElevatedWorker/Operations/AllowlistedOperationDispatcher.cs`
- Create: `src/WinAcmeGui.Infrastructure/Elevation/ElevatedOperationClient.cs`
- Create: `tests/WinAcmeGui.ElevatedWorker.Tests/Operations/AllowlistedOperationDispatcherTests.cs`
- Create: `tests/WinAcmeGui.Infrastructure.Tests/Elevation/ElevatedOperationClientTests.cs`

**Interfaces:**
- Produces: `IElevatedOperationClient.ExecuteAsync(ElevatedRequest, IProgress<OperationOutput>, CancellationToken)` and protocol version `1`.
- Consumes: typed win-acme commands and fixed system-operation DTOs.

- [ ] **Step 1: Write failing allowlist and authentication tests**

```csharp
[Fact]
public async Task Rejects_arbitrary_executable_request()
{
    var request = ElevatedRequest.RawProcess("cmd.exe", new[] { "/c", "whoami" });
    var result = await _dispatcher.DispatchAsync(request, None);
    result.ErrorCode.Should().Be("elevation.operation.not_allowed");
}

[Fact]
public async Task Rejects_wrong_session_token_before_dispatch()
{
    var result = await _server.AcceptForTestAsync(token: "wrong", ValidRequest, None);
    result.ErrorCode.Should().Be("elevation.authentication.failed");
    _dispatcher.Requests.Should().BeEmpty();
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.ElevatedWorker.Tests tests/WinAcmeGui.Infrastructure.Tests --filter Elevat`

Expected: missing worker and client.

- [ ] **Step 3: Implement one-operation authenticated named pipe protocol**

Generate a 256-bit random token in the unelevated process, restrict the named pipe ACL to the current user and Administrators, pass only pipe name and token through worker startup, require protocol version `1`, accept one request, stream results, and terminate. Bound message size and operation duration.

The dispatcher accepts only these discriminated request types: `RunValidatedWinAcme`, `RecreateWinAcmeScheduledTask`, and `RestoreGuiBackup`. `RunValidatedWinAcme` requires an executable path previously validated by the main process plus a permitted operation enum and typed tokens; it does not accept a raw executable field from the UI.

- [ ] **Step 4: Verify protocol rejection and successful fake operation**

Run: `dotnet test tests/WinAcmeGui.ElevatedWorker.Tests tests/WinAcmeGui.Infrastructure.Tests --filter Elevat --configuration Release`

Expected: authentication, version, size, allowlist, cancellation, and success tests pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add constrained UAC operation worker"
```

---

### Task 9: WPF Shell, Navigation, Themes, and Complete Localization

**Files:**
- Create: `src/WinAcmeGui.App/WinAcmeGui.App.csproj`
- Create: `src/WinAcmeGui.App/App.xaml`
- Create: `src/WinAcmeGui.App/App.xaml.cs`
- Create: `src/WinAcmeGui.App/Shell/MainWindow.xaml`
- Create: `src/WinAcmeGui.App/Shell/MainWindowViewModel.cs`
- Create: `src/WinAcmeGui.App/Navigation/NavigationService.cs`
- Create: `src/WinAcmeGui.App/Localization/Strings.resx`
- Create: `src/WinAcmeGui.App/Localization/Strings.pt-BR.resx`
- Create: `src/WinAcmeGui.App/Localization/Strings.en-US.resx`
- Create: `src/WinAcmeGui.App/Localization/CultureService.cs`
- Create: `src/WinAcmeGui.App/Theming/ThemeService.cs`
- Create: `src/WinAcmeGui.App/Preferences/GuiPreferencesStore.cs`
- Create: `tests/WinAcmeGui.App.Tests/Localization/LocalizationCompletenessTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Localization/CultureServiceTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Navigation/MainWindowViewModelTests.cs`

**Interfaces:**
- Produces: `INavigationService`, `ICultureService`, `IThemeService`, shell navigation for Home, Renewals, New Certificate, Installation, System, Settings, Logs, and About.
- Consumes: application use cases registered through dependency injection.

- [ ] **Step 1: Write failing localization and navigation tests**

```csharp
[Fact]
public void Portuguese_and_english_have_identical_resource_keys()
{
    ResourceKeys.For("pt-BR").Should().BeEquivalentTo(ResourceKeys.For("en-US"));
}

[Theory]
[InlineData("pt-PT", "pt-BR")]
[InlineData("pt-BR", "pt-BR")]
[InlineData("en-GB", "en-US")]
[InlineData("de-DE", "en-US")]
public void Chooses_supported_initial_culture(string windowsCulture, string expected) =>
    CultureService.ChooseInitial(windowsCulture).Name.Should().Be(expected);

[Fact]
public void Shell_exposes_all_approved_navigation_items() =>
    _viewModel.Items.Select(x => x.Id).Should().Equal(
        "home", "renewals", "new", "installation", "system", "settings", "logs", "about");
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.App.Tests --filter "Localization|Culture|MainWindow"`

Expected: missing WPF application services.

- [ ] **Step 3: Implement accessible administration-center shell**

Create a persistent left navigation rail, top installation/context selector, content frame, global progress/operation region, and non-modal notification region. Every button must have `AutomationProperties.Name`, keyboard focus styling, and localized tooltip. Use system brushes for high-contrast compatibility. Follow Windows theme by default and persist `System|Light|Dark` override.

- [ ] **Step 4: Implement runtime culture switch and portable preferences fallback**

Store only GUI culture, theme, selected installation path, and recent safe folders. First attempt `gui.settings.json` beside the executable; if the directory is not writable, use `%LocalAppData%\WinAcmeGui\gui.settings.json`. Change culture by replacing the merged resource dictionary and raising localized property notifications; no app restart is required.

- [ ] **Step 5: Verify resource parity and view-model behavior**

Run: `dotnet test tests/WinAcmeGui.App.Tests --configuration Release`

Expected: exact resource-key parity, culture selection, persistence fallback, and navigation tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WinAcmeGui.App tests/WinAcmeGui.App.Tests WinAcmeGui.sln
git commit -m "feat: add bilingual accessible WPF shell"
```

---

### Task 10: Home, Renewals, and Operation Confirmation UI

**Files:**
- Create: `src/WinAcmeGui.App/Features/Home/HomeView.xaml`
- Create: `src/WinAcmeGui.App/Features/Home/HomeViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Renewals/RenewalsView.xaml`
- Create: `src/WinAcmeGui.App/Features/Renewals/RenewalsViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Renewals/RenewalDetailsView.xaml`
- Create: `src/WinAcmeGui.App/Features/Renewals/RenewalDetailsViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Operations/ConfirmationDialog.xaml`
- Create: `src/WinAcmeGui.App/Features/Operations/OperationConsoleView.xaml`
- Create: `src/WinAcmeGui.Application/Renewals/ManageRenewal.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/HomeViewModelTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/RenewalsViewModelTests.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Renewals/ManageRenewalTests.cs`

**Interfaces:**
- Produces: dashboard and renewal view models; `ManageRenewal.RenewAsync/CancelAsync/RevokeAsync/CloneAsync`.
- Consumes: inventory service, command factory, runner/elevated client, confirmation policy, and navigation.

- [ ] **Step 1: Write failing dashboard, filtering, and confirmation tests**

```csharp
[Fact]
public async Task Search_matches_domain_case_insensitively()
{
    await _vm.LoadAsync();
    _vm.SearchText = "API.EXAMPLE";
    _vm.VisibleRenewals.Should().ContainSingle(x => x.Domains.Contains("api.example.com"));
}

[Fact]
public async Task Revoke_requires_exact_friendly_name_confirmation()
{
    var result = await _manager.RevokeAsync(TestRenewal, new ConfirmationInput("wrong"), None);
    result.ErrorCode.Should().Be("confirmation.name.mismatch");
    _runner.Commands.Should().BeEmpty();
}

[Fact]
public async Task Failed_replacement_never_cancels_original()
{
    _runner.FailNextCreate();
    await _manager.ReplaceAsync(TestRenewal, ValidReplacement, Confirmed, None);
    _runner.Commands.Should().NotContain(x => x.Operation == WinAcmeOperation.Cancel);
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.App.Tests tests/WinAcmeGui.Application.Tests --filter "Home|Renewal|ManageRenewal"`

Expected: missing pages and use case.

- [ ] **Step 3: Implement reactive view models and safe operation orchestration**

Expose cancellation-aware async commands, immutable filtered collections, explicit loading/empty/error states, and selected-installation context. Normal renewal emits `--renew --id`; forced renewal adds `--force`; cancel and revoke use distinct operations. Rescan inventory only after the command reaches a terminal result, including failure.

- [ ] **Step 4: Implement UI and operation console**

Home shows health cards, scheduled-task status, recent failures, expiring certificates, and recommended actions. Renewals provides search/filter/sort, detail tabs, raw-file location, exact masked command preview, live redacted output, exit code, duration, and log link. Disable edit for unknown formats with a localized diagnostic while retaining safe renew action when capability policy permits it.

- [ ] **Step 5: Verify behavior**

Run: `dotnet test tests/WinAcmeGui.App.Tests tests/WinAcmeGui.Application.Tests --filter "Home|Renewal|ManageRenewal" --configuration Release`

Expected: dashboard, filters, replacement ordering, revoke friction, and operation output tests pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: manage renewal health and lifecycle"
```

---

### Task 11: Capability-Driven Certificate and Edit Assistants

**Files:**
- Create: `src/WinAcmeGui.Application/Certificates/CapabilityCatalog.cs`
- Create: `src/WinAcmeGui.Application/Certificates/CertificateDraft.cs`
- Create: `src/WinAcmeGui.Application/Certificates/CertificateDraftValidator.cs`
- Create: `src/WinAcmeGui.App/Features/Certificates/NewCertificateView.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/NewCertificateViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/SourceStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/OrderStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/KeyStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/ValidationStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/StoreStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/InstallationStep.xaml`
- Create: `src/WinAcmeGui.App/Features/Certificates/AssistantSteps/ReviewStep.xaml`
- Create: `tests/WinAcmeGui.Application.Tests/Certificates/CertificateDraftValidatorTests.cs`
- Create: `tests/WinAcmeGui.Application.Tests/Certificates/CapabilityCatalogTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/NewCertificateViewModelTests.cs`

**Interfaces:**
- Produces: `CapabilityCatalog.For(WinAcmeVersion, DistributionKind)`, `CertificateDraftValidator.Validate`, and final `WinAcmeCommand` generation.
- Consumes: installation capabilities, command runner, confirmation policy, and inventory refresh.

- [ ] **Step 1: Write failing capability and dependency tests**

```csharp
[Fact]
public void Trimmed_distribution_does_not_offer_external_dns_plugin() =>
    _catalog.For(V2_2_9_1, DistributionKind.Trimmed).ValidationPlugins
        .Should().NotContain(x => x.Id == "cloudflare");

[Fact]
public void Manual_source_requires_at_least_one_valid_dns_name()
{
    var errors = _validator.Validate(new CertificateDraft { Source = "manual", Domains = [] });
    errors.Should().ContainSingle(x => x.Code == "certificate.domains.required");
}

[Fact]
public void Review_command_masks_dns_credentials()
{
    var command = _factory.Create(Drafts.Cloudflare("token-value"));
    command.DisplayText.Should().NotContain("token-value");
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.App.Tests --filter "Certificate|Capability"`

Expected: missing catalog, validator, and assistant.

- [ ] **Step 3: Implement versioned capability catalog and validation**

Represent source, order, CSR, validation, store, and installation plugins as typed descriptors with required fields and compatibility predicates. Seed the 2.2.x built-in catalog from the official CLI/plugin documentation. Merge only capabilities positively detected from the selected installation; unknown plugins remain descriptive/read-only.

- [ ] **Step 4: Implement seven assistant steps**

Implement Source, Order, Key, Validation, Store, Installation, and Review/Execute. Preserve the draft while navigating backward. Display production versus staging prominently. The Execute button stays disabled until validation passes and the user confirms the masked command and endpoint.

- [ ] **Step 5: Reuse assistant for safe clone/edit**

Clone populates a new draft with a new identity. Edit populates a replacement draft, creates the new renewal first, verifies success and inventory presence, then separately asks whether to cancel the original. A failed create never issues cancel.

- [ ] **Step 6: Verify all supported plugin-chain fixtures**

Run: `dotnet test tests/WinAcmeGui.Application.Tests tests/WinAcmeGui.App.Tests --filter "Certificate|Capability|Assistant" --configuration Release`

Expected: dependency validation, secret masking, navigation persistence, staging warning, and replacement safety pass.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat: guide certificate creation and safe editing"
```

---

### Task 12: Installation, System, Settings, Logs, and About Pages

**Files:**
- Create: `src/WinAcmeGui.App/Features/Installation/InstallationView.xaml`
- Create: `src/WinAcmeGui.App/Features/Installation/InstallationViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/System/SystemView.xaml`
- Create: `src/WinAcmeGui.App/Features/System/SystemViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Settings/SettingsView.xaml`
- Create: `src/WinAcmeGui.App/Features/Settings/SettingsViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/Logs/LogsView.xaml`
- Create: `src/WinAcmeGui.App/Features/Logs/LogsViewModel.cs`
- Create: `src/WinAcmeGui.App/Features/About/AboutView.xaml`
- Create: `src/WinAcmeGui.App/Features/About/AboutViewModel.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/InstallationViewModelTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/SystemViewModelTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/SettingsViewModelTests.cs`
- Create: `tests/WinAcmeGui.App.Tests/Features/LogsViewModelTests.cs`

**Interfaces:**
- Produces: all remaining approved navigation destinations and their commands.
- Consumes: discovery, download, Windows probes, settings update, log reader, diagnostics export, preferences, culture, and theme services.

- [ ] **Step 1: Write failing view-model behavior tests**

```csharp
[Fact]
public async Task Selecting_installation_replaces_context_instead_of_merging()
{
    await _vm.SelectInstallationCommand.ExecuteAsync(InstallationB);
    _context.Active.Should().Be(InstallationB);
    _context.Inventory.Renewals.Should().OnlyContain(x => x.InstallationId == InstallationB.Id);
}

[Fact]
public async Task Unknown_settings_properties_survive_save_from_view_model()
{
    await _vm.LoadAsync();
    _vm.PageSize = 75;
    await _vm.SaveCommand.ExecuteAsync(null);
    _writer.LastDocument!["FutureOption"].Should().NotBeNull();
}

[Fact]
public async Task Logs_search_never_returns_unredacted_secret()
{
    await _vm.LoadAsync();
    _vm.VisibleLines.Should().NotContain(x => x.Text.Contains("known-secret"));
}
```

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/WinAcmeGui.App.Tests --filter "Installation|System|Settings|Logs"`

Expected: pages and view models missing.

- [ ] **Step 3: Implement installation and system pages**

Show every installation independently with version, architecture, endpoint, configuration path, origin, and diagnostics. Provide Select, Browse, Download, Check updates, and Open folder. System shows IIS, certificate store, task health, ACME account/endpoint, permissions, task run/recreation, and exact confirmation for mutations.

- [ ] **Step 4: Implement settings, logs, diagnostics, and about pages**

Settings exposes only allowlisted structured settings plus GUI language/theme. Logs supports source/date/severity/text filters and tail refresh without locking files. Diagnostic export shows its exact allowlist before writing. About shows both app and active win-acme versions, licenses, and official HTTPS documentation links.

- [ ] **Step 5: Verify remaining page behavior and resource parity**

Run: `dotnet test tests/WinAcmeGui.App.Tests --configuration Release`

Expected: page behavior and `pt-BR`/`en-US` resource parity pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: complete administration center pages"
```

---

### Task 13: Portable Packaging, Documentation, and Windows Acceptance Harness

**Files:**
- Create: `scripts/Publish-Portable.ps1`
- Create: `scripts/Smoke-Test.ps1`
- Create: `tests/WinAcmeGui.Acceptance.Tests/WinAcmeGui.Acceptance.Tests.csproj`
- Create: `tests/WinAcmeGui.Acceptance.Tests/DiscoveryReadOnlyTests.cs`
- Create: `tests/WinAcmeGui.Acceptance.Tests/LifecycleStagingTests.cs`
- Create: `tests/WinAcmeGui.Acceptance.Tests/LocalizationTests.cs`
- Create: `docs/user-guide.pt-BR.md`
- Create: `docs/user-guide.en-US.md`
- Create: `docs/troubleshooting.pt-BR.md`
- Create: `docs/troubleshooting.en-US.md`
- Create: `docs/compatibility.md`
- Create: `THIRD-PARTY-NOTICES.md`
- Modify: `README.md`

**Interfaces:**
- Produces: `artifacts/WinAcmeGui-<version>-win-x64.zip` and opt-in Windows acceptance test suite.
- Consumes: the completed application and elevated worker.

- [ ] **Step 1: Write failing read-only discovery acceptance test**

```csharp
[WindowsFact]
public async Task Discovery_does_not_change_existing_configuration()
{
    var before = await DirectorySnapshot.CreateAsync(_realConfigPath);
    await _appDriver.StartAndWaitForInventoryAsync();
    var after = await DirectorySnapshot.CreateAsync(_realConfigPath);
    after.Should().BeEquivalentTo(before, options => options
        .ComparingByMembers<DirectorySnapshot>());
}
```

Gate real lifecycle tests behind `WINACME_GUI_ACCEPTANCE=1`, require staging endpoint, and refuse to run lifecycle tests against production.

- [ ] **Step 2: Verify the acceptance test fails before harness implementation**

Run on Windows: `dotnet test tests/WinAcmeGui.Acceptance.Tests --filter DiscoveryReadOnly`

Expected: compilation fails for missing driver and snapshot harness.

- [ ] **Step 3: Implement portable publish script**

`scripts/Publish-Portable.ps1` must:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts/publish/win-x64'
dotnet test (Join-Path $root 'WinAcmeGui.sln') --configuration Release
dotnet publish (Join-Path $root 'src/WinAcmeGui.App/WinAcmeGui.App.csproj') `
  -c Release -r win-x64 --self-contained true -o $publish `
  -p:PublishSingleFile=false -p:DebugType=None
dotnet publish (Join-Path $root 'src/WinAcmeGui.ElevatedWorker/WinAcmeGui.ElevatedWorker.csproj') `
  -c Release -r win-x64 --self-contained true -o (Join-Path $publish 'worker') `
  -p:PublishSingleFile=false -p:DebugType=None
```

Then copy localized guides/notices, generate SHA-256 manifest, assert no `.pdb`, test secrets, fixture keys, or source configuration are present, create a versioned ZIP, and print its path and hash.

- [ ] **Step 4: Implement smoke and acceptance harness**

Smoke test starts the published executable with `--self-test --culture pt-BR` and `--culture en-US`, validates dependency loading, resource completeness, writable preference fallback, worker protocol version, and clean exit. The acceptance driver uses a dedicated fixture installation or explicit operator-supplied win-acme path and never discovers outside its declared test roots during automated lifecycle tests.

- [ ] **Step 5: Write bilingual operational documentation**

Both user guides must cover startup discovery, installation selection, official download/manual selection, creation assistant, staging, renew/edit/clone/cancel/revoke semantics, settings backups/restore, UAC, logs, diagnostics, language/theme, and portable updates. Both troubleshooting guides must map every stable error code to cause, evidence to collect, and recovery. `docs/compatibility.md` must list win-acme 2.2.x plugin capabilities and read-only policy for unknown versions.

- [ ] **Step 6: Run full verification**

Run on a development machine:

```powershell
pwsh ./scripts/Test.ps1
pwsh ./scripts/Publish-Portable.ps1
```

Run on each supported Windows test image:

```powershell
pwsh ./scripts/Smoke-Test.ps1 -Package ./artifacts/WinAcmeGui-*-win-x64.zip
$env:WINACME_GUI_ACCEPTANCE = '1'
dotnet test ./tests/WinAcmeGui.Acceptance.Tests -c Release
```

Expected: all unit/integration tests pass; portable smoke tests pass for `pt-BR` and `en-US`; Windows 10, Windows 11, and Windows Server 2016+ acceptance results are recorded; staging lifecycle passes; discovery snapshots are byte-for-byte unchanged.

- [ ] **Step 7: Commit**

```bash
git add README.md THIRD-PARTY-NOTICES.md docs scripts tests/WinAcmeGui.Acceptance.Tests
git commit -m "docs: package and validate portable Windows release"
```

---

### Task 14: Final Security and Release Verification

**Files:**
- Modify only files implicated by verification failures.
- Create: `docs/release-checklist.md`

**Interfaces:**
- Produces: a release candidate with auditable verification evidence.
- Consumes: all prior tasks.

- [ ] **Step 1: Run automated repository verification from a clean checkout**

```powershell
git status --short
pwsh ./scripts/Test.ps1
pwsh ./scripts/Publish-Portable.ps1
```

Expected: clean worktree before the run, zero compiler warnings, all tests pass, and package hash is emitted.

- [ ] **Step 2: Audit the artifact for forbidden content**

Extract the ZIP to a new temporary directory and search binary strings and text files for fixture secrets, `PRIVATE KEY`, machine-specific absolute paths, `.renewal.json`, account keys, source `.cs`, and `.pdb`. Expected: no forbidden content. Confirm the package contains both localized resources, worker, guides, licenses, and hash manifest.

- [ ] **Step 3: Execute supported Windows matrix**

Run smoke tests on Windows 10 x64, Windows 11 x64, and Windows Server 2016 or newer x64. On at least one server with IIS and one machine without IIS, run discovery and staging issuance. Exercise standard-user startup, accepted/rejected UAC, multiple installations, custom `ConfigurationPath`, task recreation, settings backup/restore, diagnostic export, and both cultures.

- [ ] **Step 4: Record exact evidence**

In `docs/release-checklist.md`, record OS build, win-acme version/distribution, IIS state, test command, pass/fail, package SHA-256, and tester/date for every matrix row. Do not claim support for a row without executed evidence.

- [ ] **Step 5: Verify the final worktree and commit evidence**

```powershell
pwsh ./scripts/Test.ps1
git status --short
git add docs/release-checklist.md
git commit -m "chore: record release verification evidence"
```

Expected: full suite passes and only intentionally ignored artifacts remain untracked.
