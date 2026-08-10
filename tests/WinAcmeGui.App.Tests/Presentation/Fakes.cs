using WinAcmeGui.App.Presentation;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Tests.Presentation;

/// <summary>Records what the shell asked the UI to do, and answers with scripted responses.</summary>
public sealed class FakeInteraction : IShellInteraction
{
    public List<string> Messages { get; } = [];
    public List<string> Confirmations { get; } = [];
    public List<string> Prompts { get; } = [];
    public string? Clipboard { get; private set; }
    public string? OpenedTarget { get; private set; }

    public bool ConfirmResult { get; set; }
    public string? PromptResult { get; set; }
    public string? ExecutableToPick { get; set; }

    public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information) =>
        Messages.Add($"{title}: {message}");

    public bool Confirm(string title, string message, DialogSeverity severity = DialogSeverity.Warning)
    {
        Confirmations.Add(title);
        return ConfirmResult;
    }

    public string? PromptForConfirmationText(string title, string message, string expectedAnswer)
    {
        Prompts.Add(title);
        return PromptResult;
    }

    public string? PickExecutable(string title) => ExecutableToPick;

    public void CopyToClipboard(string text) => Clipboard = text;

    public void OpenExternal(string target) => OpenedTarget = target;
}

public sealed class FakeCandidateSource(params string[] paths) : IInstallationCandidateSource
{
    public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>(paths);
}

/// <summary>Validates only the paths it was given, so discovery results are deterministic.</summary>
public sealed class FakeValidator(params InstallationCandidate[] candidates) : IInstallationValidator
{
    public Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken) =>
        Task.FromResult(candidates.FirstOrDefault(x =>
            x.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase)));
}

public sealed class FakeConfigurationReader(ConfigurationSnapshot snapshot) : IWinAcmeConfigurationReader
{
    public Task<ConfigurationSnapshot> ReadAsync(string executablePath, CancellationToken cancellationToken) =>
        Task.FromResult(snapshot);
}

public sealed class FakeRenewalReader(params Renewal[] renewals) : IRenewalReader
{
    public Task<IReadOnlyList<RenewalReadResult>> ReadDirectoryAsync(string configurationPath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RenewalReadResult>>(
            renewals.Select(x => new RenewalReadResult(x.SourcePath, true, x.IsEditable, x, x.Diagnostics)).ToArray());
}

/// <summary>Captures each command it is asked to run and returns a scripted result.</summary>
public sealed class RecordingRunner : IWinAcmeRunner
{
    public List<WinAcmeCommand> Commands { get; } = [];
    public OperationResult Result { get; set; } = new(OperationStatus.Succeeded, 0, TimeSpan.Zero, [], null);
    public Exception? Throws { get; set; }

    public Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        if (Throws is not null) return Task.FromException<OperationResult>(Throws);
        return Task.FromResult(Result);
    }
}

public sealed class FakeInstaller : IWinAcmeInstaller
{
    public InstalledPackage Package { get; set; } = new("2.2.9", "/tmp/win-acme");
    public Exception? Throws { get; set; }
    public int Calls { get; private set; }

    public Task<InstalledPackage> InstallLatestAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throws is not null) return Task.FromException<InstalledPackage>(Throws);
        return Task.FromResult(Package);
    }
}
