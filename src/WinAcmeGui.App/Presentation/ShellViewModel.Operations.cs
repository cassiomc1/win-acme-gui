using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Presentation;

public sealed partial class ShellViewModel
{
    /// <summary>Discovers installations, picks the first operational one and loads its renewal inventory.</summary>
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        Status = Culture["Scanning"];
        SelectedRenewal = null;
        Installations.Clear();
        DiscoveryDiagnostics.Clear();
        ClearRenewals();
        try
        {
            var result = await _services.Discovery.ExecuteAsync(
                new Progress<string>(path => Status = path),
                cancellation.Token);

            foreach (var diagnostic in result.Diagnostics) DiscoveryDiagnostics.Add(diagnostic.Message);
            var candidate = result.Installations.FirstOrDefault(x => x.IsOperational);
            foreach (var installation in result.Installations)
                Installations.Add(new InstallationRow(installation, Culture, candidate is not null && installation.ExecutablePath == candidate.ExecutablePath));
            Raise(nameof(HasInstallations), nameof(HasDiscoveryDiagnostics));

            ActiveCandidate = candidate;
            if (candidate is null)
            {
                ResetEndpoint();
                Status = Culture["NoInstallation"];
                Log("OperationDiscovery", ActivityOutcome.Information, Culture["NoInstallation"]);
                return;
            }

            await LoadInventoryAsync(candidate, cancellation.Token);
            Status = $"{candidate.ExecutablePath} · {candidate.VersionText}";
            Log("OperationDiscovery", ActivityOutcome.Succeeded, Status);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = Describe("OperationDiscovery", "OperationCancelled");
            Log("OperationDiscovery", ActivityOutcome.Cancelled, Status);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            Log("OperationDiscovery", ActivityOutcome.Failed, ex.Message);
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
        }
    }

    /// <summary>Validates a manually chosen executable and makes it the active installation.</summary>
    public async Task<bool> UseExecutableAsync(string executablePath)
    {
        if (IsBusy) return false;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        try
        {
            var candidate = await _services.Validator.ValidateAsync(executablePath, cancellation.Token);
            if (candidate is null)
            {
                Status = Culture["InvalidExecutable"];
                Log("OperationDiscovery", ActivityOutcome.Failed, Status);
                _interaction.ShowMessage(Culture["Error"], Status, DialogSeverity.Error);
                return false;
            }

            SelectedRenewal = null;
            ClearRenewals();
            Installations.Clear();
            Installations.Add(new InstallationRow(candidate, Culture, true));
            Raise(nameof(HasInstallations));
            ActiveCandidate = candidate;
            await LoadInventoryAsync(candidate, cancellation.Token);
            Status = $"{candidate.ExecutablePath} · {candidate.VersionText}";
            Log("OperationDiscovery", ActivityOutcome.Succeeded, Status);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = Describe("OperationDiscovery", "OperationCancelled");
            Log("OperationDiscovery", ActivityOutcome.Cancelled, Status);
            return false;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            Log("OperationDiscovery", ActivityOutcome.Failed, ex.Message);
            return false;
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
        }
    }

    public Task<OperationResult> CreateCertificateAsync(
        CertificateDraft draft,
        bool staging,
        IProgress<string>? output,
        CancellationToken cancellationToken)
    {
        var candidate = ActiveCandidate ?? throw new InvalidOperationException("No active win-acme installation.");
        return _services.Certificates.CreateAsync(candidate.ExecutablePath, draft, staging, output, cancellationToken);
    }

    /// <summary>Records the outcome of a wizard run and reloads the inventory when it succeeded.</summary>
    public async Task NotifyCertificateCompletedAsync(OperationResult result)
    {
        Log("OperationCertificate", ActivityEntry.FromStatus(result.Status), result.ErrorCode ?? Culture["OperationSucceeded"]);
        if (result.Status == OperationStatus.Succeeded) await LoadAsync();
    }

    public void CancelActiveOperation() => _activeOperationCancellation?.Cancel();

    private async Task LoadInventoryAsync(InstallationCandidate candidate, CancellationToken cancellationToken)
    {
        var installation = ToInstallation(candidate);
        var inventory = await _services.Inventory.LoadAsync(installation, candidate.Configuration, cancellationToken);
        ClearRenewals();
        foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal, Culture));
        ApplyRenewalFilter();
        _activeEndpoint = inventory.Configuration.Endpoint.BaseUri.ToString();
        _settingsPath = inventory.Configuration.SettingsPath;
        _endpointIsProduction = inventory.Configuration.Endpoint.IsProduction;
        _endpointKnown = true;
        foreach (var diagnostic in inventory.Diagnostics.Where(x => x.IsError))
            DiscoveryDiagnostics.Add(diagnostic.Message);
        Raise(nameof(ActiveEndpoint), nameof(SettingsPath), nameof(EndpointKindText), nameof(EndpointBrushKey));
        Raise(nameof(HasDiscoveryDiagnostics));
        RaiseRenewalCounters();
    }

    private static WinAcmeInstallation ToInstallation(InstallationCandidate candidate)
    {
        var endpoint = candidate.Endpoint
            ?? (candidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase)
                ? AcmeEndpoint.Staging
                : AcmeEndpoint.Production);
        return WinAcmeInstallation.Create(
            candidate.ExecutablePath,
            ParseVersion(candidate.VersionText),
            candidate.ConfigurationPath,
            endpoint);
    }

    private static WinAcmeVersion ParseVersion(string value)
    {
        var parts = value
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();
        return new(
            parts.ElementAtOrDefault(0),
            parts.ElementAtOrDefault(1),
            parts.ElementAtOrDefault(2),
            parts.ElementAtOrDefault(3));
    }

    private void ClearRenewals()
    {
        Renewals.Clear();
        FilteredRenewals.Clear();
        RaiseRenewalCounters();
    }

    private void ApplyRenewalFilter()
    {
        var selectedId = SelectedRenewal?.Id;
        FilteredRenewals.Clear();
        var matches = RenewalFilter.Apply(
            Renewals.Select(row => row.Renewal),
            SearchText,
            SelectedStatusFilter.Status);
        foreach (var renewal in matches)
            FilteredRenewals.Add(Renewals.First(row => ReferenceEquals(row.Renewal, renewal)));
        RaiseRenewalCounters();
        if (selectedId is not null && FilteredRenewals.All(row => row.Id != selectedId)) SelectedRenewal = null;
    }

    private void RaiseRenewalCounters()
    {
        Raise(nameof(TotalRenewalCount), nameof(HealthyRenewalCount), nameof(DueSoonRenewalCount));
        Raise(nameof(AttentionRenewalCount), nameof(RenewalSummary));
        Raise(nameof(HasRenewals), nameof(HasFilteredRenewals), nameof(ShowNoMatches), nameof(ShowNoRenewals));
    }

    private void ResetEndpoint()
    {
        _activeEndpoint = Dash;
        _settingsPath = null;
        _endpointKnown = false;
        _endpointIsProduction = false;
        Raise(nameof(ActiveEndpoint), nameof(SettingsPath), nameof(EndpointKindText), nameof(EndpointBrushKey));
    }
}
