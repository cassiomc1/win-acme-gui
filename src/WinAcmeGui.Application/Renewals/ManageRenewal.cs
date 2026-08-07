using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Renewals;

public sealed class ManageRenewal(IWinAcmeRunner runner, WinAcmeCommandFactory commandFactory)
{
    public Task<OperationResult> RenewAsync(string executablePath, Renewal renewal, bool force, CancellationToken cancellationToken) =>
        runner.RunAsync(commandFactory.CreateRenew(executablePath, renewal.Id, force), null, cancellationToken);

    public Task<OperationResult> CancelAsync(string executablePath, Renewal renewal, string confirmation, CancellationToken cancellationToken) =>
        confirmation.Equals(renewal.FriendlyName, StringComparison.Ordinal) ? runner.RunAsync(commandFactory.CreateCancel(executablePath, renewal.Id), null, cancellationToken) : Task.FromResult(RejectedConfirmation());

    public Task<OperationResult> RevokeAsync(string executablePath, Renewal renewal, string confirmation, CancellationToken cancellationToken) =>
        confirmation.Equals(renewal.FriendlyName, StringComparison.Ordinal) ? runner.RunAsync(commandFactory.CreateRevoke(executablePath, renewal.Id), null, cancellationToken) : Task.FromResult(RejectedConfirmation());

    private static OperationResult RejectedConfirmation() =>
        new(OperationStatus.Failed, null, TimeSpan.Zero, [], "confirmation.name.mismatch");
}
