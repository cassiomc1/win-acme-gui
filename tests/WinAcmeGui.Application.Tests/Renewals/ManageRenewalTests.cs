using FluentAssertions;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Tests.Renewals;

public sealed class ManageRenewalTests
{
    private static Renewal Renewal => new("id", "example.com", ["example.com"], RenewalStatus.Healthy, true, "renewal.json", []);

    [Fact]
    public async Task Revoke_requires_exact_friendly_name_confirmation()
    {
        var runner = new StubRunner();
        var result = await new ManageRenewal(runner, new WinAcmeCommandFactory()).RevokeAsync(
            @"C:\wacs.exe", Renewal, "wrong", CancellationToken.None);

        result.ErrorCode.Should().Be("confirmation.name.mismatch");
        runner.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Forced_renewal_runs_official_command()
    {
        var runner = new StubRunner();
        var result = await new ManageRenewal(runner, new WinAcmeCommandFactory()).RenewAsync(
            @"C:\wacs.exe", Renewal, true, CancellationToken.None);

        result.Status.Should().Be(OperationStatus.Succeeded);
        runner.Commands.Single().DisplayText.Should().Contain("--force");
    }

    private sealed class StubRunner : IWinAcmeRunner
    {
        public List<WinAcmeCommand> Commands { get; } = [];
        public Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new OperationResult(OperationStatus.Succeeded, 0, TimeSpan.Zero, [], null));
        }
    }
}
