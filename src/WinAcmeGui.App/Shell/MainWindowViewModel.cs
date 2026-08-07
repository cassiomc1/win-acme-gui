using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Renewals;
using WinAcmeGui.Infrastructure.Configuration;
using WinAcmeGui.Infrastructure.Discovery;
using WinAcmeGui.Infrastructure.Operations;

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
    private RenewalRow? _selectedRenewal;
    private readonly ManageRenewal _renewalManager;

    public MainWindowViewModel(CultureService culture)
    {
        _culture = culture;
        var versionProbe = new ProcessWinAcmeVersionProbe();
        _validator = new InstallationValidator(versionProbe);
        _discovery = new DiscoverInstallations(
            [new ScheduledTaskCandidateSource(), new PathCandidateSource(), new KnownLocationCandidateSource(AppContext.BaseDirectory), new ProcessCandidateSource()],
            _validator);
        _inventory = new InventoryService(
            new WinAcmeConfigurationReader(versionProbe),
            new RenewalDocumentReader());
        _renewalManager = new ManageRenewal(new WinAcmeProcessRunner(), new WinAcmeCommandFactory());
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
    public AsyncRelayCommand RefreshCommand { get; }

    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string SelectedSection { get => _selectedSection; set => SetField(ref _selectedSection, value); }
    public InstallationCandidate? ActiveCandidate { get => _activeCandidate; private set => SetField(ref _activeCandidate, value); }
    public string ActiveVersion => ActiveCandidate?.VersionText ?? "—";
    public string ActiveConfigurationPath => ActiveCandidate?.ConfigurationPath ?? "—";
    public string ActiveEndpoint => _activeEndpoint;
    public string RenewalSummary => $"{Renewals.Count} {_culture["RenewalCount"]}";
    public string PageTitle => Sections.FirstOrDefault(x => x.Id == SelectedSection)?.Title ?? _culture["Home"];
    public string NoInstallationText => _culture["NoInstallation"];
    public string ScanningText => _culture["Scanning"];
    public RenewalRow? SelectedRenewal { get => _selectedRenewal; set => SetField(ref _selectedRenewal, value); }

    public async Task LoadAsync()
    {
        IsBusy = true;
        Status = _culture["Scanning"];
        Installations.Clear();
        Renewals.Clear();
        try
        {
            var result = await _discovery.ExecuteAsync(new Progress<string>(path => Status = path), CancellationToken.None);
            foreach (var candidate in result.Installations) Installations.Add(candidate);
            ActiveCandidate = Installations.FirstOrDefault();
            if (ActiveCandidate is not null)
            {
                var version = ParseVersion(ActiveCandidate.VersionText);
                var endpoint = ActiveCandidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase)
                    ? AcmeEndpoint.Staging : AcmeEndpoint.Production;
                var installation = WinAcmeInstallation.Create(ActiveCandidate.ExecutablePath, version, ActiveCandidate.ConfigurationPath, endpoint);
                var inventory = await _inventory.LoadAsync(installation, CancellationToken.None);
                foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal));
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
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally { IsBusy = false; }
    }

    public void SelectSection(string section)
    {
        SelectedSection = section;
        Notify(nameof(PageTitle));
    }

    public async Task<bool> UseExecutableAsync(string executablePath)
    {
        var candidate = await _validator.ValidateAsync(executablePath, CancellationToken.None);
        if (candidate is null)
        {
            Status = "The selected file is not a valid win-acme executable.";
            return false;
        }
        Installations.Clear();
        Installations.Add(candidate);
        ActiveCandidate = candidate;
        try
        {
            IsBusy = true;
            Renewals.Clear();
            var endpoint = candidate.ConfigurationPath.Contains("staging", StringComparison.OrdinalIgnoreCase) ? AcmeEndpoint.Staging : AcmeEndpoint.Production;
            var installation = WinAcmeInstallation.Create(candidate.ExecutablePath, ParseVersion(candidate.VersionText), candidate.ConfigurationPath, endpoint);
            var inventory = await _inventory.LoadAsync(installation, CancellationToken.None);
            foreach (var renewal in inventory.Renewals) Renewals.Add(new RenewalRow(renewal));
            _activeEndpoint = inventory.Configuration.Endpoint.BaseUri.ToString();
            Status = $"{candidate.ExecutablePath} · {candidate.VersionText}";
            Notify(nameof(ActiveEndpoint));
            Notify(nameof(RenewalSummary));
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return false;
        }
        finally { IsBusy = false; }
        return true;
    }

    public void RefreshLabels()
    {
        foreach (var section in Sections) section.NotifyTitleChanged();
        Notify(nameof(PageTitle));
        Notify(nameof(RenewalSummary));
        Notify(nameof(NoInstallationText));
        Notify(nameof(ScanningText));
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> RenewSelectedAsync(bool force)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        return RunRenewalAsync(force);
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> CancelSelectedAsync(string confirmation)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        return _renewalManager.CancelAsync(_activeCandidate.ExecutablePath, SelectedRenewal.Renewal, confirmation, CancellationToken.None);
    }

    public Task<WinAcmeGui.Domain.Operations.OperationResult?> RevokeSelectedAsync(string confirmation)
    {
        if (_activeCandidate is null || SelectedRenewal is null) return Task.FromResult<WinAcmeGui.Domain.Operations.OperationResult?>(null);
        return _renewalManager.RevokeAsync(_activeCandidate.ExecutablePath, SelectedRenewal.Renewal, confirmation, CancellationToken.None);
    }

    private async Task<WinAcmeGui.Domain.Operations.OperationResult?> RunRenewalAsync(bool force)
    {
        var result = await _renewalManager.RenewAsync(_activeCandidate!.ExecutablePath, SelectedRenewal!.Renewal, force, CancellationToken.None);
        Status = result.Status == WinAcmeGui.Domain.Operations.OperationStatus.Succeeded ? "Renewal completed." : result.ErrorCode ?? "Renewal failed.";
        return result;
    }

    private static WinAcmeVersion ParseVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        return new(parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1), parts.ElementAtOrDefault(2), parts.ElementAtOrDefault(3));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
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
