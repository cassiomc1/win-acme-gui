using FluentAssertions;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Tests.Presentation;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task Load_selects_the_operational_installation_and_reports_counters()
    {
        using var harness = ShellHarness.Create(
        [
            ShellHarness.Renewal("a", "Production", RenewalStatus.Healthy),
            ShellHarness.Renewal("b", "Portal", RenewalStatus.DueSoon),
            ShellHarness.Renewal("c", "Legacy", RenewalStatus.Failed),
            ShellHarness.Renewal("d", "Broken", RenewalStatus.Unreadable, editable: false)
        ]);

        await harness.ViewModel.LoadAsync();

        harness.ViewModel.HasActiveInstallation.Should().BeTrue();
        harness.ViewModel.ActiveExecutablePath.Should().Be(harness.ExecutablePath);
        harness.ViewModel.TotalRenewalCount.Should().Be(4);
        harness.ViewModel.HealthyRenewalCount.Should().Be(1);
        harness.ViewModel.DueSoonRenewalCount.Should().Be(1);
        harness.ViewModel.AttentionRenewalCount.Should().Be(2);
        harness.ViewModel.Installations.Should().ContainSingle(x => x.IsActive);
    }

    [Fact]
    public async Task Load_without_any_installation_reports_the_empty_state()
    {
        using var harness = ShellHarness.Create(discoverInstallation: false);

        await harness.ViewModel.LoadAsync();

        harness.ViewModel.HasNoInstallation.Should().BeTrue();
        harness.ViewModel.ShowNoRenewals.Should().BeTrue();
        harness.ViewModel.ActiveEndpoint.Should().Be("—");
        harness.ViewModel.Status.Should().Be(LocalizationTable.PortugueseBrazil["NoInstallation"]);
        harness.ViewModel.Activity.Should().ContainSingle();
    }

    [Fact]
    public async Task A_read_only_installation_is_listed_but_never_becomes_active()
    {
        using var harness = ShellHarness.Create(operational: false);

        await harness.ViewModel.LoadAsync();

        harness.ViewModel.HasInstallations.Should().BeTrue();
        harness.ViewModel.HasActiveInstallation.Should().BeFalse();
        harness.ViewModel.Installations.Single().HasBadge.Should().BeTrue();
        harness.ViewModel.CanMutateSelectedRenewal.Should().BeFalse();
    }

    [Fact]
    public async Task Search_and_status_filters_combine_and_reset_together()
    {
        using var harness = ShellHarness.Create(
        [
            ShellHarness.Renewal("a", "Production", RenewalStatus.Healthy, domains: "shop.example.com"),
            ShellHarness.Renewal("b", "Portal", RenewalStatus.DueSoon, domains: "portal.example.com"),
            ShellHarness.Renewal("c", "Portal legacy", RenewalStatus.DueSoon, domains: "old.example.com")
        ]);
        await harness.ViewModel.LoadAsync();

        harness.ViewModel.SearchText = "portal";
        harness.ViewModel.FilteredRenewals.Should().HaveCount(2);

        harness.ViewModel.SelectedStatusFilter = harness.ViewModel.StatusFilters
            .Single(x => x.Status == RenewalStatus.DueSoon);
        harness.ViewModel.FilteredRenewals.Should().HaveCount(2);

        harness.ViewModel.SearchText = "shop";
        harness.ViewModel.FilteredRenewals.Should().BeEmpty();
        harness.ViewModel.ShowNoMatches.Should().BeTrue();
        harness.ViewModel.HasActiveFilters.Should().BeTrue();

        harness.ViewModel.ClearFiltersCommand.Execute(null);
        harness.ViewModel.FilteredRenewals.Should().HaveCount(3);
        harness.ViewModel.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public async Task Filtering_out_the_selected_row_clears_the_selection_and_disables_mutations()
    {
        using var harness = ShellHarness.Create(
        [
            ShellHarness.Renewal("a", "Production"),
            ShellHarness.Renewal("b", "Portal")
        ]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.SelectedRenewal = harness.ViewModel.FilteredRenewals.Single(x => x.Id == "a");
        harness.ViewModel.CanMutateSelectedRenewal.Should().BeTrue();

        harness.ViewModel.SearchText = "Portal";

        harness.ViewModel.SelectedRenewal.Should().BeNull();
        harness.ViewModel.CanMutateSelectedRenewal.Should().BeFalse();
    }

    [Fact]
    public async Task Switching_language_relabels_navigation_status_and_rows()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production", RenewalStatus.DueSoon)]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.FilteredRenewals.Single().StatusText.Should().Be("Renovar em breve");

        harness.ViewModel.SetEnglishCommand.Execute(null);

        harness.ViewModel.Sections.First().Title.Should().Be("Home");
        harness.ViewModel.PageTitle.Should().Be("Home");
        harness.ViewModel.StatusFilters.First().Label.Should().Be("All");
        harness.ViewModel.FilteredRenewals.Single().StatusText.Should().Be("Due soon");
    }

    [Fact]
    public async Task Language_switch_keeps_the_selected_row_selected()
    {
        using var harness = ShellHarness.Create(
        [
            ShellHarness.Renewal("a", "Production"),
            ShellHarness.Renewal("b", "Portal")
        ]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.SelectedRenewal = harness.ViewModel.FilteredRenewals.Single(x => x.Id == "b");

        harness.ViewModel.SetEnglishCommand.Execute(null);

        harness.ViewModel.SelectedRenewal.Should().NotBeNull();
        harness.ViewModel.SelectedRenewal!.Id.Should().Be("b");
    }

    [Fact]
    public void Navigation_updates_the_page_flags_and_selection_state()
    {
        using var harness = ShellHarness.Create();

        harness.ViewModel.SelectSection("renewals");

        harness.ViewModel.IsRenewals.Should().BeTrue();
        harness.ViewModel.IsHome.Should().BeFalse();
        harness.ViewModel.Sections.Single(x => x.Id == "renewals").IsSelected.Should().BeTrue();
        harness.ViewModel.Sections.Single(x => x.Id == "home").IsSelected.Should().BeFalse();
        harness.ViewModel.PageTitle.Should().Be("Renovações");
    }

    [Fact]
    public void Theme_toggle_flips_the_flag_and_raises_the_swap_event()
    {
        using var harness = ShellHarness.Create();
        var observed = new List<bool>();
        harness.ViewModel.ThemeChanged += (_, isDark) => observed.Add(isDark);

        harness.ViewModel.ToggleThemeCommand.Execute(null);
        harness.ViewModel.ToggleThemeCommand.Execute(null);

        observed.Should().Equal(true, false);
        harness.ViewModel.IsDarkTheme.Should().BeFalse();
    }
}
