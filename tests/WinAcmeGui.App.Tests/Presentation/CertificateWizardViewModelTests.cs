using FluentAssertions;
using WinAcmeGui.App.Localization;
using WinAcmeGui.App.Presentation;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Tests.Presentation;

public sealed class CertificateWizardViewModelTests
{
    [Fact]
    public void Preview_lists_validation_errors_until_the_draft_is_complete()
    {
        var wizard = Create(out _);

        wizard.UpdatePreview().Should().BeFalse();
        wizard.HasValidationErrors.Should().BeTrue();
        wizard.Preview.Should().Be(LocalizationTable.PortugueseBrazil["PreviewPlaceholder"]);

        wizard.Domains = "example.com, www.example.com";
        wizard.AcceptTerms = true;

        wizard.UpdatePreview().Should().BeTrue();
        wizard.HasValidationErrors.Should().BeFalse();
        wizard.Preview.Should().Contain("--source manual")
            .And.Contain("--host example.com,www.example.com")
            .And.Contain("--validationmode http-01")
            .And.Contain("--accepttos");
    }

    [Fact]
    public void Staging_is_the_default_and_appears_in_the_preview()
    {
        var wizard = Create(out _);
        wizard.Domains = "example.com";
        wizard.AcceptTerms = true;

        wizard.UseStaging.Should().BeTrue();
        wizard.UpdatePreview();
        wizard.Preview.Should().Contain("--test");

        wizard.UseStaging = false;
        wizard.UpdatePreview();
        wizard.Preview.Should().NotContain("--test");
    }

    [Fact]
    public void Choosing_pem_output_reveals_the_path_field_and_requires_an_absolute_path()
    {
        var wizard = Create(out _);
        wizard.Domains = "example.com";
        wizard.AcceptTerms = true;

        wizard.StoragePathVisible.Should().BeFalse();
        wizard.Store = wizard.StoreOptions.Single(x => x.Value == "pemfiles");
        wizard.StoragePathVisible.Should().BeTrue();

        wizard.UpdatePreview().Should().BeFalse();
        wizard.ValidationErrors.Should().ContainSingle(x => x.Contains("absolute"));

        wizard.StoragePath = @"C:\certificates";
        wizard.UpdatePreview().Should().BeTrue();
        wizard.Preview.Should().Contain("--pemfilespath");
    }

    [Fact]
    public void Leaving_pem_output_clears_the_path_so_it_cannot_leak_into_the_command()
    {
        var wizard = Create(out _);
        wizard.Store = wizard.StoreOptions.Single(x => x.Value == "pfxfile");
        wizard.StoragePath = @"C:\certificates";

        wizard.Store = wizard.StoreOptions.Single(x => x.Value == "certificatestore");

        wizard.StoragePath.Should().BeEmpty();
        wizard.StoragePathVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Execution_requires_confirmation_and_reports_success_once()
    {
        var wizard = Create(out var interaction, out var executions);
        wizard.Domains = "example.com";
        wizard.AcceptTerms = true;
        interaction.ConfirmResult = false;

        await wizard.RunAsync();
        executions.Should().BeEmpty();
        wizard.Completed.Should().BeFalse();

        interaction.ConfirmResult = true;
        var completions = 0;
        wizard.CompletedSuccessfully += (_, _) => completions++;

        await wizard.RunAsync();

        executions.Should().ContainSingle();
        wizard.Completed.Should().BeTrue();
        completions.Should().Be(1);
    }

    [Fact]
    public async Task An_invalid_draft_never_reaches_win_acme()
    {
        var wizard = Create(out var interaction, out var executions);
        interaction.ConfirmResult = true;

        await wizard.RunAsync();

        executions.Should().BeEmpty();
        interaction.Confirmations.Should().BeEmpty();
        wizard.HasValidationErrors.Should().BeTrue();
    }

    [Fact]
    public async Task A_failed_run_keeps_the_window_open_and_shows_the_output()
    {
        var wizard = Create(
            out var interaction,
            out _,
            new OperationResult(OperationStatus.Failed, 1, TimeSpan.Zero, ["order failed"], "wacs.exit.1"));
        wizard.Domains = "example.com";
        wizard.AcceptTerms = true;
        interaction.ConfirmResult = true;

        await wizard.RunAsync();

        wizard.Completed.Should().BeFalse();
        wizard.Output.Should().Contain("order failed").And.Contain("wacs.exit.1");
        wizard.IsRunning.Should().BeFalse();
    }

    private static CertificateWizardViewModel Create(out FakeInteraction interaction) =>
        Create(out interaction, out _);

    private static CertificateWizardViewModel Create(out FakeInteraction interaction, out List<CertificateDraft> executions) =>
        Create(out interaction, out executions, new OperationResult(OperationStatus.Succeeded, 0, TimeSpan.Zero, [], null));

    private static CertificateWizardViewModel Create(
        out FakeInteraction interaction,
        out List<CertificateDraft> executions,
        OperationResult result)
    {
        var captured = new List<CertificateDraft>();
        executions = captured;
        var fake = new FakeInteraction();
        interaction = fake;
        var culture = new CultureService();
        culture.SetCulture(LocalizationTable.PortugueseBrazilCulture);
        return new CertificateWizardViewModel(
            @"C:\win-acme\wacs.exe",
            (draft, _, _, _) =>
            {
                captured.Add(draft);
                return Task.FromResult(result);
            },
            culture,
            fake);
    }
}
