using FluentAssertions;
using WinAcmeGui.App.Presentation;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Tests.Presentation;

/// <summary>Download, manual selection and the System page's copy action.</summary>
public sealed class ShellCommandTests
{
    [Fact]
    public async Task A_successful_download_reports_the_destination_and_rediscovers()
    {
        using var harness = ShellHarness.Create(discoverInstallation: false);
        harness.Installer.Package = new("2.2.9.1701", @"C:\portable\win-acme-downloads\2.2.9.1701");

        await harness.ViewModel.DownloadLatestAsync();

        harness.Installer.Calls.Should().Be(1);
        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("2.2.9.1701"));
        harness.ViewModel.Activity.Should().Contain(x => x.Outcome == ActivityOutcome.Succeeded);
        harness.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_download_is_reported_without_touching_the_active_installation()
    {
        using var harness = ShellHarness.Create();
        await harness.ViewModel.LoadAsync();
        harness.Installer.Throws = new InvalidOperationException("digest mismatch");

        await harness.ViewModel.DownloadLatestAsync();

        harness.ViewModel.Status.Should().Be("digest mismatch");
        harness.ViewModel.Activity.First().Outcome.Should().Be(ActivityOutcome.Failed);
        harness.ViewModel.HasActiveInstallation.Should().BeTrue();
        harness.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Dismissing_the_file_picker_leaves_the_shell_untouched()
    {
        using var harness = ShellHarness.Create();
        harness.Interaction.ExecutableToPick = null;

        await harness.ViewModel.SelectExecutableAsync();

        harness.ViewModel.HasActiveInstallation.Should().BeFalse();
        harness.ViewModel.Activity.Should().BeEmpty();
    }

    [Fact]
    public async Task Picking_a_valid_executable_makes_it_the_only_installation()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production")], discoverInstallation: false);
        harness.Interaction.ExecutableToPick = harness.ExecutablePath;

        await harness.ViewModel.SelectExecutableAsync();

        harness.ViewModel.HasActiveInstallation.Should().BeTrue();
        harness.ViewModel.Installations.Should().ContainSingle(x => x.IsActive);
        harness.ViewModel.TotalRenewalCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unrecognized_executable_is_rejected_with_an_error()
    {
        using var harness = ShellHarness.Create();
        harness.Interaction.ExecutableToPick = "/nonexistent/wacs.exe";

        await harness.ViewModel.SelectExecutableAsync();

        harness.ViewModel.HasActiveInstallation.Should().BeFalse();
        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("não é um wacs.exe válido"));
        harness.ViewModel.Activity.First().Outcome.Should().Be(ActivityOutcome.Failed);
    }

    [Fact]
    public async Task Using_a_read_only_installation_warns_instead_of_switching()
    {
        using var harness = ShellHarness.Create(operational: false);
        await harness.ViewModel.LoadAsync();
        var row = harness.ViewModel.Installations.Single();

        await harness.ViewModel.UseInstallationAsync(row);

        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("Shared configuration directory."));
        harness.ViewModel.HasActiveInstallation.Should().BeFalse();
    }

    [Fact]
    public async Task Copying_system_details_includes_the_installation_facts()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production")]);
        await harness.ViewModel.LoadAsync();

        harness.ViewModel.CopySystemDetailsCommand.Execute(null);

        harness.Interaction.Clipboard.Should().NotBeNull();
        harness.Interaction.Clipboard!.Should()
            .Contain(harness.ExecutablePath)
            .And.Contain("acme-v02.api.letsencrypt.org")
            .And.Contain("2.2.9.1701");
    }

    [Fact]
    public void Opening_the_upstream_site_uses_https()
    {
        using var harness = ShellHarness.Create();

        harness.ViewModel.OpenWinAcmeSiteCommand.Execute(null);

        harness.Interaction.OpenedTarget.Should().Be("https://www.win-acme.com/");
    }

    [Fact]
    public async Task The_certificate_wizard_requires_an_installation_before_opening()
    {
        using var harness = ShellHarness.Create(discoverInstallation: false);
        var launched = 0;
        harness.ViewModel.CertificateWizardLauncher = () =>
        {
            launched++;
            return Task.FromResult<OperationResult?>(null);
        };
        await harness.ViewModel.LoadAsync();

        await harness.ViewModel.OpenCertificateWizardCommand.ExecuteAsync();

        launched.Should().Be(0);
        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("Selecione ou descubra"));
    }

    [Fact]
    public async Task The_certificate_wizard_opens_once_an_installation_is_active()
    {
        using var harness = ShellHarness.Create();
        var launched = 0;
        harness.ViewModel.CertificateWizardLauncher = () =>
        {
            launched++;
            return Task.FromResult<OperationResult?>(null);
        };
        await harness.ViewModel.LoadAsync();

        await harness.ViewModel.OpenCertificateWizardCommand.ExecuteAsync();

        launched.Should().Be(1);
    }

    [Fact]
    public async Task A_certificate_wizard_result_is_added_to_the_session_activity_log()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production")]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.CertificateWizardLauncher = () => Task.FromResult<OperationResult?>(
            new(OperationStatus.Failed, 1, TimeSpan.Zero, [], "wacs.exit.1"));

        await harness.ViewModel.OpenCertificateWizardCommand.ExecuteAsync();

        harness.ViewModel.Activity.Should().Contain(x =>
            x.OperationKey == "OperationCertificate" && x.Outcome == ActivityOutcome.Failed);
    }
}
