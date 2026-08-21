using System.Collections.ObjectModel;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Presentation;

/// <summary>
/// Shell state and orchestration. It owns no WPF type, so the whole navigation, filtering, KPI and
/// operation-confirmation behaviour is exercised by the cross-platform test suite.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ShellServices _services;
    private readonly IShellInteraction _interaction;
    private readonly IWinAcmeInstaller _installer;
    private readonly Func<DateTimeOffset> _clock;

    private string _status = string.Empty;
    private bool _isBusy;
    private NavigationItem _selectedSection;
    private InstallationCandidate? _activeCandidate;
    private string _activeEndpoint = Dash;
    private string? _settingsPath;
    private bool _endpointIsProduction;
    private bool _endpointKnown;
    private string _searchText = string.Empty;
    private RenewalStatusOption _selectedStatusFilter;
    private RenewalRow? _selectedRenewal;
    private CancellationTokenSource? _activeOperationCancellation;
    private bool _isDarkTheme;

    private const string Dash = "—";

    public ShellViewModel(
        CultureService culture,
        ShellServices? services = null,
        IShellInteraction? interaction = null,
        IWinAcmeInstaller? installer = null,
        Func<DateTimeOffset>? clock = null)
    {
        Culture = culture;
        _services = services ?? ShellServices.CreateDefault();
        _interaction = interaction ?? new NullShellInteraction();
        _installer = installer ?? new OfficialWinAcmeInstaller();
        _clock = clock ?? (() => DateTimeOffset.Now);

        Sections =
        [
            new(culture, "home", "Home", "HomeDescription", "■"),
            new(culture, "renewals", "Renewals", "RenewalsDescription", "↻"),
            new(culture, "new", "NewCertificate", "NewDescription", "＋"),
            new(culture, "installation", "Installation", "InstallationDescription", "⚙"),
            new(culture, "system", "System", "SystemDescription", "◎"),
            new(culture, "logs", "Logs", "LogsDescription", "≡"),
            new(culture, "settings", "Settings", "SettingsDescription", "⚒"),
            new(culture, "about", "About", "AboutDescription", "ℹ")
        ];
        _selectedSection = Sections[0];
        _selectedSection.IsSelected = true;

        StatusFilters =
        [
            new(culture, null, "FilterAll"),
            new(culture, RenewalStatus.Healthy, "StatusHealthy"),
            new(culture, RenewalStatus.DueSoon, "StatusDueSoon"),
            new(culture, RenewalStatus.Failed, "StatusFailed"),
            new(culture, RenewalStatus.Expired, "StatusExpired"),
            new(culture, RenewalStatus.Unreadable, "StatusUnreadable")
        ];
        _selectedStatusFilter = StatusFilters[0];

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        DownloadCommand = new AsyncRelayCommand(DownloadLatestAsync, () => !IsBusy);
        SelectExecutableCommand = new AsyncRelayCommand(SelectExecutableAsync, () => !IsBusy);
        UseInstallationCommand = new AsyncRelayCommand<InstallationRow>(UseInstallationAsync, row => row is not null && !IsBusy);
        RenewCommand = new AsyncRelayCommand(() => RenewSelectedAsync(false), () => CanMutateSelectedRenewal);
        ForceRenewCommand = new AsyncRelayCommand(() => RenewSelectedAsync(true), () => CanMutateSelectedRenewal);
        CancelRenewalCommand = new AsyncRelayCommand(CancelSelectedAsync, () => CanMutateSelectedRenewal);
        RevokeRenewalCommand = new AsyncRelayCommand(RevokeSelectedAsync, () => CanMutateSelectedRenewal);
        CancelOperationCommand = new RelayCommand(CancelActiveOperation, () => CanCancelOperation);
        NavigateCommand = new RelayCommand<NavigationItem>(item => { if (item is not null) SelectedSection = item; });
        ClearFiltersCommand = new RelayCommand(ClearFilters, () => HasActiveFilters);
        ToggleThemeCommand = new RelayCommand(() => IsDarkTheme = !IsDarkTheme);
        SetLightThemeCommand = new RelayCommand(() => IsDarkTheme = false);
        SetDarkThemeCommand = new RelayCommand(() => IsDarkTheme = true);
        SetPortugueseCommand = new RelayCommand(() => SetCulture(LocalizationTable.PortugueseBrazilCulture));
        SetEnglishCommand = new RelayCommand(() => SetCulture(LocalizationTable.EnglishCulture));
        ClearActivityCommand = new RelayCommand(() => { Activity.Clear(); Raise(nameof(HasActivity)); }, () => Activity.Count > 0);
        CopyActivityCommand = new RelayCommand(CopyActivity, () => Activity.Count > 0);
        CopySystemDetailsCommand = new RelayCommand(CopySystemDetails);
        OpenWinAcmeSiteCommand = new RelayCommand(() => _interaction.OpenExternal("https://www.win-acme.com/"));
        OpenCertificateWizardCommand = new AsyncRelayCommand(OpenCertificateWizardAsync, () => !IsBusy);

        _status = culture["StatusIdle"];
        culture.CultureChanged += (_, _) => OnCultureChanged();
    }

    public CultureService Culture { get; }

    public ObservableCollection<NavigationItem> Sections { get; }
    public ObservableCollection<RenewalStatusOption> StatusFilters { get; }
    public ObservableCollection<InstallationRow> Installations { get; } = [];
    public ObservableCollection<RenewalRow> Renewals { get; } = [];
    public ObservableCollection<RenewalRow> FilteredRenewals { get; } = [];
    public ObservableCollection<ActivityEntry> Activity { get; } = [];
    public ObservableCollection<string> DiscoveryDiagnostics { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand SelectExecutableCommand { get; }
    public AsyncRelayCommand<InstallationRow> UseInstallationCommand { get; }
    public AsyncRelayCommand RenewCommand { get; }
    public AsyncRelayCommand ForceRenewCommand { get; }
    public AsyncRelayCommand CancelRenewalCommand { get; }
    public AsyncRelayCommand RevokeRenewalCommand { get; }
    public RelayCommand CancelOperationCommand { get; }
    public RelayCommand<NavigationItem> NavigateCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SetLightThemeCommand { get; }
    public RelayCommand SetDarkThemeCommand { get; }
    public RelayCommand SetPortugueseCommand { get; }
    public RelayCommand SetEnglishCommand { get; }
    public RelayCommand ClearActivityCommand { get; }
    public RelayCommand CopyActivityCommand { get; }
    public RelayCommand CopySystemDetailsCommand { get; }
    public RelayCommand OpenWinAcmeSiteCommand { get; }

    /// <summary>
    /// Opens the certificate wizard. The shell view model cannot create a window, so the host assigns
    /// this hook; without a host the command is a no-op and the guard message is shown instead.
    /// </summary>
    public AsyncRelayCommand OpenCertificateWizardCommand { get; }

    /// <summary>Set by the window to display the wizard; returns after the dialog closes.</summary>
    public Func<Task<OperationResult?>>? CertificateWizardLauncher { get; set; }

    private async Task OpenCertificateWizardAsync()
    {
        if (ActiveCandidate is null)
        {
            _interaction.ShowMessage(Culture["Information"], Culture["SelectInstallationFirst"]);
            return;
        }
        if (CertificateWizardLauncher is null) return;
        var result = await CertificateWizardLauncher();
        if (result is not null) await NotifyCertificateCompletedAsync(result);
    }
}
