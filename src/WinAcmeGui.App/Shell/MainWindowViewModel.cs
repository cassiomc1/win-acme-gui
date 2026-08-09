using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Renewals;
using WinAcmeGui.Infrastructure.Configuration;
using WinAcmeGui.Infrastructure.Discovery;
using WinAcmeGui.Infrastructure.Operations;
using WinAcmeGui.Infrastructure.Renewals;

namespace WinAcmeGui.App.Shell;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly CultureService _culture;
    private readonly DiscoverInstallations _discovery;
    private readonly InstallationValidator _validator;
    private readonly InventoryService _inventory;
    private string _status = string.Empty;
    private bool _isBusy;
    private string _selectedSection = "home";
    private InstallationCandidate? _activeCandidate;
    private string _activeEndpoint = "—";
    private string _searchText = string.Empty;
    private RenewalRow? _selectedRenewal;
    private CancellationTokenSource? _activeOperationCancellation;
    private readonly ManageRenewal _renewalManager;
    private readonly ManageCertificate _certificateManager;

    public MainWindowViewModel(CultureService culture)
    {
        _culture = culture;
        var versionProbe = new ProcessWinAcmeVersionProbe();
        var configurationReader = new WinAcmeConfigurationReader(versionProbe);
        _validator = new InstallationValidator(versionProbe, configurationReader);
        _discovery = new DiscoverInstallations(
            [new ScheduledTaskCandidateSource(), new PathCandidateSource(), new KnownLocationCandidateSource(AppContext.BaseDirectory), new ProcessCandidateSource()],
            _validator);
        _inventory = new InventoryService(
            configurationReader,
            new RenewalDocumentReader());
        var runner = CreateRunner();
        var commandFactory = new WinAcmeCommandFactory();
        _renewalManager = new ManageRenewal(runner, commandFactory);
        _certificateManager = new ManageCertificate(runner, new CertificateDraftValidator(), commandFactory);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        Sections = new ObservableCollection<NavigationItem>([
            new("home", () => _culture["Home"]),
            new("renewals", () => _culture["Renewals"]),
            new("new", () => _culture["NewCertificate"]),
            new("installation", () => _culture["Installation"]),
            new("system", () => _culture["System"]),
            new("settings", () => _culture["Settings"]),
            new("logs", () => _culture["Logs"]),
            new("about", () => _culture["About"])
        ]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<NavigationItem> Sections { get; }
    public ObservableCollection<InstallationCandidate> Installations { get; } = [];
    public ObservableCollection<RenewalRow> Renewals { get; } = [];
    public ObservableCollection<RenewalRow> FilteredRenewals { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }

    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value)) return;
            Notify(nameof(CanMutateSelectedRenewal));
        }
    }
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!SetField(ref _selectedSection, value)) return;
            Notify(nameof(PageTitle));
            Notify(nameof(PageDescription));
            Notify(nameof(IsDashboard));
            Notify(nameof(IsContextPage));
            Notify(nameof(HasContextAction));
            Notify(nameof(ContextActionText));
        }
    }
    public InstallationCandidate? ActiveCandidate { get => _activeCandidate; private set => SetField(ref _activeCandidate, value); }
    public string ActiveVersion => ActiveCandidate?.VersionText ?? "—";
    public string ActiveConfigurationPath => ActiveCandidate?.ConfigurationPath ?? "—";
    public string ActiveEndpoint => _activeEndpoint;
    public string RenewalSummary => $"{Renewals.Count} {_culture["RenewalCount"]}";
    public string PageTitle => Sections.FirstOrDefault(x => x.Id == SelectedSection)?.Title ?? _culture["Home"];
    public string PageDescription => _culture[SelectedSection switch
    {
        "home" => "HomeDescription",
        "renewals" => "RenewalsDescription",
        "new" => "NewDescription",
        "installation" => "InstallationDescription",
        "system" => "SystemDescription",
        "settings" => "SettingsDescription",
        "logs" => "LogsDescription",
        "about" => "AboutDescription",
        _ => "HomeDescription"
    }];
    public bool IsDashboard => SelectedSection is "home" or "renewals";
    public bool IsContextPage => !IsDashboard;
    public bool HasContextAction => SelectedSection is "new" or "installation";
    public string ContextActionText => SelectedSection == "new" ? _culture["OpenCertificate"] : _culture["ChooseInstallation"];
    public string NoInstallationText => _culture["NoInstallation"];
    public string ScanningText => _culture["Scanning"];
    public string GuiSubtitle => _culture["GuiSubtitle"];
    public string StatusLabel => _culture["Status"];
    public string DownloadText => _culture["Download"];
    public string UpdateText => _culture["Update"];
    public string ActiveInstallationLabel => _culture["ActiveInstallation"];
    public string ConfigurationPathLabel => _culture["ConfigurationPath"];
    public string EndpointLabel => _culture["Endpoint"];
    public string RenewalsLoadedText => _culture["RenewalsLoaded"];
    public string HealthLabel => _culture["Health"];
    public string ReadyToOperateText => _culture["ReadyToOperate"];
    public string ScheduledTaskLabel => _culture["ScheduledTask"];
    public string VerifyInSystemText => _culture["VerifyInSystem"];
    public string FilterRenewalsText => _culture["FilterRenewals"];
    public string FriendlyNameText => _culture["FriendlyName"];
    public string DomainsText => _culture["Domains"];
    public string StatusColumnText => _culture["StatusColumn"];
    public string DiagnosticsText => _culture["Diagnostics"];
    public string EditableText => _culture["Editable"];
    public string RenewText => _culture["Renew"];
    public string ForceText => _culture["Force"];
    public string CancelText => _culture["Cancel"];
    public string RevokeText => _culture["Revoke"];
    public string NewCertificateActionText => _culture["NewCertificateAction"];
    public string SelectExecutableActionText => _culture["SelectExecutableAction"];
    public string CancelOperationText => _culture["CancelOperation"];
    public bool CanCancelOperation => _activeOperationCancellation is not null;
    public bool CanMutateSelectedRenewal => SelectedRenewal?.IsEditable == true && _activeCandidate is not null && !IsBusy;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            ApplyRenewalFilter();
        }
    }
    public RenewalRow? SelectedRenewal
    {
        get => _selectedRenewal;
        set
        {
            if (!SetField(ref _selectedRenewal, value)) return;
            Notify(nameof(CanMutateSelectedRenewal));
        }
    }

    public void CancelActiveOperation() => _activeOperationCancellation?.Cancel();

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        Status = _culture["Scanning"];
        SelectedRenewal = null;
        Installations.Clear();
        Renewals.Clear();
        FilteredRenewals.Clear();
        try
        {
            var result = await _discovery.ExecuteAsync(new Progress<string>(path => Status = path), cancellation.Token);
            foreach (var candidate in result.Installations) Installations.Add(candidate);
            ActiveCandidate = Installations.FirstOrDefault(x => x.IsOperational);
            if (ActiveCandidate is not null)
            {
                var version = ParseVersion(ActiveCandidate.VersionText);
                var endpoint = ActiveCandidate.Endpoint
                    ?? (ActiveCandidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase)
                        ? AcmeEndpoint.Staging : AcmeEndpoint.Production);
                var installation = WinAcmeInstallation.Create(ActiveCandidate.ExecutablePath, version, ActiveCandidate.ConfigurationPath, endpoint);
                var inventory = await _inventory.LoadAsync(installation, ActiveCandidate.Configuration, cancellation.Token);
                foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal));
                ApplyRenewalFilter();
                _activeEndpoint = inventory.Configuration.Endpoint.BaseUri.ToString();
                Status = $"{ActiveCandidate.ExecutablePath} · {ActiveCandidate.VersionText}";
            }
            else
            {
                _activeEndpoint = "—";
                Status = _culture["NoInstallation"];
            }
            Notify(nameof(ActiveVersion));
            Notify(nameof(ActiveConfigurationPath));
            Notify(nameof(ActiveEndpoint));
            Notify(nameof(RenewalSummary));
            Notify(nameof(PageTitle));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = "operation.cancelled";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
        }
    }

    public void SelectSection(string section)
    {
        SelectedSection = section;
        Notify(nameof(PageTitle));
    }

    public async Task<bool> UseExecutableAsync(string executablePath)
    {
        if (IsBusy) return false;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        try
        {
            var candidate = await _validator.ValidateAsync(executablePath, cancellation.Token);
            if (candidate is null)
            {
                Status = "The selected file is not a valid win-acme executable.";
                return false;
            }
            Installations.Clear();
            Installations.Add(candidate);
            ActiveCandidate = candidate;
            SelectedRenewal = null;
            Renewals.Clear();
            FilteredRenewals.Clear();
            var endpoint = candidate.Endpoint
                ?? (candidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase)
                    ? AcmeEndpoint.Staging : AcmeEndpoint.Production);
            var installation = WinAcmeInstallation.Create(candidate.ExecutablePath, ParseVersion(candidate.VersionText), candidate.ConfigurationPath, endpoint);
            var inventory = await _inventory.LoadAsync(installation, candidate.Configuration, cancellation.Token);
            foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal));
            ApplyRenewalFilter();
            _activeEndpoint = inventory.Configuration.Endpoint.BaseUri.ToString();
            Status = $"{candidate.ExecutablePath} · {candidate.VersionText}";
            Notify(nameof(ActiveEndpoint));
            Notify(nameof(RenewalSummary));
            Notify(nameof(ActiveVersion));
            Notify(nameof(ActiveConfigurationPath));
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = "operation.cancelled";
            return false;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
        }
    }

    public void RefreshLabels()
    {
        foreach (var section in Sections) section.NotifyTitleChanged();
        Notify(nameof(PageTitle));
        Notify(nameof(PageDescription));
        Notify(nameof(ContextActionText));
        Notify(nameof(RenewalSummary));
        Notify(nameof(NoInstallationText));
        Notify(nameof(ScanningText));
        Notify(nameof(SearchText));
        Notify(nameof(GuiSubtitle));
        Notify(nameof(StatusLabel));
        Notify(nameof(DownloadText));
        Notify(nameof(UpdateText));
        Notify(nameof(ActiveInstallationLabel));
        Notify(nameof(ConfigurationPathLabel));
        Notify(nameof(EndpointLabel));
        Notify(nameof(RenewalsLoadedText));
        Notify(nameof(HealthLabel));
        Notify(nameof(ReadyToOperateText));
        Notify(nameof(ScheduledTaskLabel));
        Notify(nameof(VerifyInSystemText));
        Notify(nameof(FilterRenewalsText));
        Notify(nameof(FriendlyNameText));
        Notify(nameof(DomainsText));
        Notify(nameof(StatusColumnText));
        Notify(nameof(DiagnosticsText));
        Notify(nameof(EditableText));
        Notify(nameof(RenewText));
        Notify(nameof(ForceText));
        Notify(nameof(CancelText));
        Notify(nameof(RevokeText));
        Notify(nameof(NewCertificateActionText));
        Notify(nameof(SelectExecutableActionText));
        Notify(nameof(CancelOperationText));
        Notify(nameof(CanMutateSelectedRenewal));
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> RenewSelectedAsync(bool force)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        var executablePath = _activeCandidate.ExecutablePath;
        var renewal = SelectedRenewal.Renewal;
        return RunMutationAsync(
            token => _renewalManager.RenewAsync(executablePath, renewal, force, token),
            force ? "Forced renewal" : "Renewal");
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> CancelSelectedAsync(string confirmation)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        var executablePath = _activeCandidate.ExecutablePath;
        var renewal = SelectedRenewal.Renewal;
        return RunMutationAsync(
            token => _renewalManager.CancelAsync(executablePath, renewal, confirmation, token),
            "Cancellation");
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> RevokeSelectedAsync(string confirmation)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        var executablePath = _activeCandidate.ExecutablePath;
        var renewal = SelectedRenewal.Renewal;
        return RunMutationAsync(
            token => _renewalManager.RevokeAsync(executablePath, renewal, confirmation, token),
            "Revocation");
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult> CreateCertificateAsync(
        CertificateDraft draft,
        bool staging,
        CancellationToken cancellationToken)
    {
        if (_activeCandidate is null) throw new InvalidOperationException("No active win-acme installation.");
        return _certificateManager.CreateAsync(_activeCandidate.ExecutablePath, draft, staging, null, cancellationToken);
    }

    private async Task<WinAcmeGui.Domain.Operations.OperationResult?> RunMutationAsync(
        Func<CancellationToken, Task<WinAcmeGui.Domain.Operations.OperationResult>> operation,
        string operationName)
    {
        if (IsBusy) return null;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        try
        {
            var result = await operation(cancellation.Token);
            Status = result.Status == WinAcmeGui.Domain.Operations.OperationStatus.Succeeded
                ? $"{operationName} completed."
                : result.ErrorCode ?? $"{operationName} failed.";
            if (result.Status == WinAcmeGui.Domain.Operations.OperationStatus.Succeeded)
            {
                try
                {
                    await RefreshActiveInventoryAsync(cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Status = $"{operationName} completed; refresh failed: {ex.Message}";
                }
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = "operation.cancelled";
            return new(
                WinAcmeGui.Domain.Operations.OperationStatus.Cancelled,
                null,
                TimeSpan.Zero,
                [],
                "operation.cancelled");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return new(
                WinAcmeGui.Domain.Operations.OperationStatus.Failed,
                null,
                TimeSpan.Zero,
                [],
                "operation.exception");
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
            Notify(nameof(CanMutateSelectedRenewal));
        }
    }

    private CancellationTokenSource BeginCancellableOperation()
    {
        var cancellation = new CancellationTokenSource();
        _activeOperationCancellation = cancellation;
        Notify(nameof(CanCancelOperation));
        return cancellation;
    }

    private void EndCancellableOperation(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_activeOperationCancellation, cancellation)) return;
        _activeOperationCancellation = null;
        Notify(nameof(CanCancelOperation));
    }

    private async Task RefreshActiveInventoryAsync(CancellationToken cancellationToken)
    {
        if (ActiveCandidate is null) return;
        SelectedRenewal = null;
        var endpoint = ActiveCandidate.Endpoint
            ?? (ActiveCandidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase)
                ? AcmeEndpoint.Staging : AcmeEndpoint.Production);
        var installation = WinAcmeInstallation.Create(
            ActiveCandidate.ExecutablePath,
            ParseVersion(ActiveCandidate.VersionText),
            ActiveCandidate.ConfigurationPath,
            endpoint);
        var inventory = await _inventory.LoadAsync(installation, ActiveCandidate.Configuration, cancellationToken);
        Renewals.Clear();
        FilteredRenewals.Clear();
        foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal));
        ApplyRenewalFilter();
        _activeEndpoint = inventory.Configuration.Endpoint.BaseUri.ToString();
        Notify(nameof(ActiveEndpoint));
        Notify(nameof(RenewalSummary));
    }

    private static IWinAcmeRunner CreateRunner()
    {
        if (!OperatingSystem.IsWindows()) return new WinAcmeProcessRunner();
        var workerPath = Path.Combine(AppContext.BaseDirectory, "worker", "WinAcmeGui.ElevatedWorker.exe");
        return new NamedPipeElevatedOperationClient(workerPath);
    }

    private void ApplyRenewalFilter()
    {
        FilteredRenewals.Clear();
        foreach (var renewal in RenewalFilter.Apply(Renewals.Select(x => x.Renewal), SearchText))
            FilteredRenewals.Add(new RenewalRow(renewal));
        Notify(nameof(RenewalSummary));
    }

    private static WinAcmeVersion ParseVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        return new(parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1), parts.ElementAtOrDefault(2), parts.ElementAtOrDefault(3));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }

    private void Notify(string? name) => PropertyChanged?.Invoke(this, new(name));
}

public sealed record NavigationItem(string Id, Func<string> TitleFactory)
{
    public string Title => TitleFactory();
    public event EventHandler? TitleChanged;
    public void NotifyTitleChanged() => TitleChanged?.Invoke(this, EventArgs.Empty);
}

public sealed record RenewalRow(Renewal Renewal)
{
    public string FriendlyName => Renewal.FriendlyName;
    public string Domains => string.Join(", ", Renewal.Domains);
    public string Status => Renewal.Status.ToString();
    public string Diagnostics => string.Join("; ", Renewal.Diagnostics.Select(x => x.Message));
    public bool IsEditable => Renewal.IsEditable;
}

public sealed class AsyncRelayCommand(Func<Task> execute) : System.Windows.Input.ICommand
{
    private bool _isExecuting;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting;
    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); }
        finally { _isExecuting = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
