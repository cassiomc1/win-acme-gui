using FluentAssertions;
using WinAcmeGui.App.Presentation;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Tests.Presentation;

/// <summary>Confirmation rules and activity logging for the four mutating operations.</summary>
public sealed class ShellMutationTests
{
    [Fact]
    public async Task Renew_runs_without_extra_confirmation_and_logs_success()
    {
        using var harness = await ArrangeAsync();

        var result = await harness.ViewModel.RenewSelectedAsync(force: false);

        result!.Status.Should().Be(OperationStatus.Succeeded);
        harness.Interaction.Confirmations.Should().BeEmpty();
        harness.Runner.Commands.Should().ContainSingle();
        harness.Runner.Commands[0].Arguments.Select(x => x.Name).Should().Contain("--renew").And.NotContain("--force");
        harness.ViewModel.Activity.First().Outcome.Should().Be(ActivityOutcome.Succeeded);
    }

    [Fact]
    public async Task Forced_renewal_requires_confirmation_before_running()
    {
        using var harness = await ArrangeAsync();
        harness.Interaction.ConfirmResult = false;

        var declined = await harness.ViewModel.RenewSelectedAsync(force: true);

        declined.Should().BeNull();
        harness.Runner.Commands.Should().BeEmpty();

        harness.Interaction.ConfirmResult = true;
        var accepted = await harness.ViewModel.RenewSelectedAsync(force: true);

        accepted!.Status.Should().Be(OperationStatus.Succeeded);
        harness.Runner.Commands.Single().Arguments.Select(x => x.Name).Should().Contain("--force");
    }

    [Fact]
    public async Task Cancel_requires_the_exact_friendly_name()
    {
        using var harness = await ArrangeAsync();
        harness.Interaction.PromptResult = "wrong name";

        var rejected = await harness.ViewModel.CancelSelectedAsync();

        rejected.Should().BeNull();
        harness.Runner.Commands.Should().BeEmpty();
        harness.ViewModel.Activity.First().Outcome.Should().Be(ActivityOutcome.Failed);

        harness.Interaction.PromptResult = "Production";
        var accepted = await harness.ViewModel.CancelSelectedAsync();

        accepted!.Status.Should().Be(OperationStatus.Succeeded);
        harness.Runner.Commands.Single().Arguments.Select(x => x.Name).Should().Contain("--cancel");
    }

    [Fact]
    public async Task Revoke_requires_the_exact_friendly_name()
    {
        using var harness = await ArrangeAsync();
        harness.Interaction.PromptResult = "Production";

        var result = await harness.ViewModel.RevokeSelectedAsync();

        result!.Status.Should().Be(OperationStatus.Succeeded);
        harness.Runner.Commands.Single().Arguments.Select(x => x.Name).Should().Contain("--revoke");
    }

    [Fact]
    public async Task Dismissing_the_confirmation_prompt_runs_nothing_and_logs_nothing()
    {
        using var harness = await ArrangeAsync();
        var activityBefore = harness.ViewModel.Activity.Count;
        harness.Interaction.PromptResult = null;

        var result = await harness.ViewModel.CancelSelectedAsync();

        result.Should().BeNull();
        harness.Runner.Commands.Should().BeEmpty();
        harness.ViewModel.Activity.Should().HaveCount(activityBefore);
    }

    [Fact]
    public async Task A_read_only_row_cannot_be_mutated()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Broken", RenewalStatus.Unreadable, editable: false)]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.SelectedRenewal = harness.ViewModel.FilteredRenewals.Single();

        harness.ViewModel.CanMutateSelectedRenewal.Should().BeFalse();
        harness.ViewModel.RenewCommand.CanExecute(null).Should().BeFalse();

        // ManageRenewal is the authority: even a direct call is refused for a read-only renewal.
        var result = await harness.ViewModel.RenewSelectedAsync(force: false);
        result!.ErrorCode.Should().Be("renewal.read_only");
        harness.Runner.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Operating_without_a_selection_asks_for_one_instead_of_running()
    {
        using var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production")]);
        await harness.ViewModel.LoadAsync();

        var result = await harness.ViewModel.RenewSelectedAsync(force: false);

        result.Should().BeNull();
        harness.Runner.Commands.Should().BeEmpty();
        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("Selecione uma renovação"));
    }

    [Fact]
    public async Task A_failed_operation_surfaces_the_error_code_and_an_error_dialog()
    {
        using var harness = await ArrangeAsync();
        harness.Runner.Result = new(OperationStatus.Failed, 1, TimeSpan.Zero, [], "wacs.exit.1");

        var result = await harness.ViewModel.RenewSelectedAsync(force: false);

        result!.Status.Should().Be(OperationStatus.Failed);
        harness.ViewModel.Status.Should().Be("wacs.exit.1");
        harness.ViewModel.Activity.First().Outcome.Should().Be(ActivityOutcome.Failed);
        harness.Interaction.Messages.Should().ContainSingle(x => x.Contains("wacs.exit.1"));
    }

    [Fact]
    public async Task An_exception_is_reported_without_leaving_the_shell_busy()
    {
        using var harness = await ArrangeAsync();
        harness.Runner.Throws = new InvalidOperationException("pipe closed");

        var result = await harness.ViewModel.RenewSelectedAsync(force: false);

        result!.ErrorCode.Should().Be("operation.exception");
        harness.ViewModel.Status.Should().Be("pipe closed");
        harness.ViewModel.IsBusy.Should().BeFalse();
        harness.ViewModel.CanCancelOperation.Should().BeFalse();
    }

    [Fact]
    public async Task The_activity_log_can_be_copied_and_cleared()
    {
        using var harness = await ArrangeAsync();
        await harness.ViewModel.RenewSelectedAsync(force: false);

        harness.ViewModel.CopyActivityCommand.Execute(null);
        harness.Interaction.Clipboard.Should().Contain("Renovação");

        harness.ViewModel.ClearActivityCommand.Execute(null);
        harness.ViewModel.HasActivity.Should().BeFalse();
        harness.ViewModel.ClearActivityCommand.CanExecute(null).Should().BeFalse();
    }

    private static async Task<ShellHarness> ArrangeAsync()
    {
        var harness = ShellHarness.Create([ShellHarness.Renewal("a", "Production")]);
        await harness.ViewModel.LoadAsync();
        harness.ViewModel.SelectedRenewal = harness.ViewModel.FilteredRenewals.Single();
        return harness;
    }
}
