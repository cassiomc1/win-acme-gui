using WinAcmeGui.App.Localization;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Presentation;

public sealed partial class ShellViewModel
{
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value)) return;
            Raise(nameof(CanMutateSelectedRenewal), nameof(IsIdle), nameof(BusyText));
            RefreshCommandStates();
        }
    }

    public bool IsIdle => !IsBusy;

    public string BusyText => IsBusy ? Culture["StatusBusy"] : Culture["StatusIdle"];

    public NavigationItem SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (ReferenceEquals(_selectedSection, value) || value is null) return;
            _selectedSection.IsSelected = false;
            _selectedSection = value;
            _selectedSection.IsSelected = true;
            Raise(nameof(SelectedSection), nameof(SelectedSectionId), nameof(PageTitle), nameof(PageDescription));
            Raise(nameof(IsHome), nameof(IsRenewals), nameof(IsInstallation), nameof(IsSystem));
            Raise(nameof(IsLogs), nameof(IsSettings), nameof(IsAbout), nameof(IsNewCertificate));
        }
    }

    public string SelectedSectionId => _selectedSection.Id;
    public string PageTitle => _selectedSection.Title;
    public string PageDescription => _selectedSection.Description;

    public bool IsHome => SelectedSectionId == "home";
    public bool IsRenewals => SelectedSectionId == "renewals";
    public bool IsNewCertificate => SelectedSectionId == "new";
    public bool IsInstallation => SelectedSectionId == "installation";
    public bool IsSystem => SelectedSectionId == "system";
    public bool IsLogs => SelectedSectionId == "logs";
    public bool IsSettings => SelectedSectionId == "settings";
    public bool IsAbout => SelectedSectionId == "about";

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            // Always reassert the checked state: selector controls bind OneWay, and a click on an
            // already-active option must snap their local visual state back to the real value.
            var changed = SetField(ref _isDarkTheme, value);
            Raise(nameof(IsDarkTheme));
            if (!changed) return;
            Raise(nameof(ThemeName));
            ThemeChanged?.Invoke(this, value);
        }
    }

    /// <summary>Raised when the theme toggles; the window swaps the palette dictionary in response.</summary>
    public event EventHandler<bool>? ThemeChanged;

    public string ThemeName => Culture[IsDarkTheme ? "ThemeDark" : "ThemeLight"];

    public InstallationCandidate? ActiveCandidate
    {
        get => _activeCandidate;
        private set
        {
            if (!SetField(ref _activeCandidate, value)) return;
            Raise(nameof(ActiveExecutablePath), nameof(ActiveVersion), nameof(ActiveConfigurationPath));
            Raise(nameof(HasActiveInstallation), nameof(HasNoInstallation), nameof(HealthText), nameof(HealthBrushKey));
            Raise(nameof(CanMutateSelectedRenewal));
            foreach (var row in Installations)
                row.IsActive = value is not null && PathsMatch(row.ExecutablePath, value.ExecutablePath);
            RefreshCommandStates();
        }
    }

    public bool HasActiveInstallation => ActiveCandidate is not null;
    public bool HasNoInstallation => ActiveCandidate is null;

    public string ActiveExecutablePath => ActiveCandidate?.ExecutablePath ?? Dash;
    public string ActiveVersion => ActiveCandidate?.VersionText ?? Dash;
    public string ActiveConfigurationPath => ActiveCandidate?.ConfigurationPath ?? Dash;
    public string ActiveEndpoint => _activeEndpoint;
    public string SettingsPath => _settingsPath ?? Dash;

    public string EndpointKindText => !_endpointKnown
        ? Culture["EndpointUnknown"]
        : Culture[_endpointIsProduction ? "EndpointProduction" : "EndpointStaging"];

    public string EndpointBrushKey => !_endpointKnown
        ? "StatusNeutralBrush"
        : _endpointIsProduction ? "StatusHealthyBrush" : "StatusWarningBrush";

    public string HealthText => Culture[HasActiveInstallation ? "ReadyToOperate" : "NotOperational"];

    public string HealthBrushKey => HasActiveInstallation ? "StatusHealthyBrush" : "StatusNeutralBrush";

    public string ExecutionModeText => Culture[_services.UsesElevatedWorker ? "ElevationWorker" : "ElevationDirect"];

    public string PlatformText => System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    public string GuiVersionText =>
        typeof(ShellViewModel).Assembly.GetName().Version?.ToString() ?? Dash;

    public int TotalRenewalCount => Renewals.Count;
    public int HealthyRenewalCount => Renewals.Count(x => x.Status == RenewalStatus.Healthy);
    public int DueSoonRenewalCount => Renewals.Count(x => x.Status == RenewalStatus.DueSoon);

    /// <summary>Failures, expired certificates and unreadable documents — everything an operator must look at.</summary>
    public int AttentionRenewalCount => Renewals.Count(x =>
        x.Status is RenewalStatus.Failed or RenewalStatus.Expired or RenewalStatus.Unreadable);

    public string RenewalSummary => $"{Renewals.Count} {Culture["RenewalCount"]}";

    public bool HasRenewals => Renewals.Count > 0;
    public bool HasFilteredRenewals => FilteredRenewals.Count > 0;

    /// <summary>Empty grid because a filter excluded everything, as opposed to nothing being loaded.</summary>
    public bool ShowNoMatches => HasRenewals && !HasFilteredRenewals;
    public bool ShowNoRenewals => !HasRenewals;

    public bool HasActivity => Activity.Count > 0;
    public bool HasDiscoveryDiagnostics => DiscoveryDiagnostics.Count > 0;
    public bool HasInstallations => Installations.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            ApplyRenewalFilter();
            Raise(nameof(HasActiveFilters));
            ClearFiltersCommand.RaiseCanExecuteChanged();
        }
    }

    public RenewalStatusOption SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (value is null || ReferenceEquals(_selectedStatusFilter, value)) return;
            _selectedStatusFilter = value;
            Raise(nameof(SelectedStatusFilter), nameof(HasActiveFilters));
            ApplyRenewalFilter();
            ClearFiltersCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchText) || SelectedStatusFilter.Status is not null;

    public RenewalRow? SelectedRenewal
    {
        get => _selectedRenewal;
        set
        {
            if (!SetField(ref _selectedRenewal, value)) return;
            Raise(nameof(CanMutateSelectedRenewal), nameof(HasSelectedRenewal));
            RefreshCommandStates();
        }
    }

    public bool HasSelectedRenewal => SelectedRenewal is not null;

    /// <summary>Mutations require an editable row, an operational installation and no operation in flight.</summary>
    public bool CanMutateSelectedRenewal =>
        SelectedRenewal?.IsEditable == true && ActiveCandidate is not null && !IsBusy;

    public bool CanCancelOperation => _activeOperationCancellation is not null;

    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = StatusFilters[0];
    }

    public void SelectSection(string id)
    {
        var match = Sections.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (match is not null) SelectedSection = match;
    }

    public void SetCulture(string name)
    {
        Culture.SetCulture(name);
    }

    private void OnCultureChanged()
    {
        Raise(nameof(PageTitle), nameof(PageDescription), nameof(RenewalSummary), nameof(HealthText));
        Raise(nameof(ThemeName), nameof(EndpointKindText), nameof(ExecutionModeText), nameof(BusyText));
        // Rebuild rows so their cached localized status text follows the new language.
        var selectedId = SelectedRenewal?.Id;
        var rows = Renewals.Select(x => new RenewalRow(x.Renewal, Culture)).ToArray();
        Renewals.Clear();
        foreach (var row in rows) Renewals.Add(row);
        ApplyRenewalFilter();
        SelectedRenewal = FilteredRenewals.FirstOrDefault(x => x.Id == selectedId);
    }

    private void RefreshCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
        SelectExecutableCommand.RaiseCanExecuteChanged();
        UseInstallationCommand.RaiseCanExecuteChanged();
        RenewCommand.RaiseCanExecuteChanged();
        ForceRenewCommand.RaiseCanExecuteChanged();
        CancelRenewalCommand.RaiseCanExecuteChanged();
        RevokeRenewalCommand.RaiseCanExecuteChanged();
        CancelOperationCommand.RaiseCanExecuteChanged();
        ClearActivityCommand.RaiseCanExecuteChanged();
        CopyActivityCommand.RaiseCanExecuteChanged();
    }

    private static bool PathsMatch(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);
}
