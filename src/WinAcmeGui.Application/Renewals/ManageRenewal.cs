using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Renewals;

public sealed class ManageRenewal(IWinAcmeRunner runner, WinAcmeCommandFactory commandFactory)
{
    public Task<OperationResult> RenewAsync(string executablePath, Renewal renewal, bool force, CancellationToken cancellationToken) =>
        renewal.IsEditable
            ? runner.RunAsync(commandFactory.CreateRenew(executablePath, renewal.Id, force), null, cancellationToken)
            : Task.FromResult(ReadOnlyRenewal());

    public Task<OperationResult> CancelAsync(string executablePath, Renewal renewal, string confirmation, CancellationToken cancellationToken) =>
        !renewal.IsEditable ? Task.FromResult(ReadOnlyRenewal())
        : string.IsNullOrEmpty(confirmation) ? Task.FromResult(RejectedConfirmation())
        : string.Equals(confirmation, renewal.FriendlyName, StringComparison.Ordinal) ? runner.RunAsync(commandFactory.CreateCancel(executablePath, renewal.Id), null, cancellationToken) : Task.FromResult(RejectedConfirmation());

    public Task<OperationResult> RevokeAsync(string executablePath, Renewal renewal, string confirmation, CancellationToken cancellationToken) =>
        !renewal.IsEditable ? Task.FromResult(ReadOnlyRenewal())
        : string.IsNullOrEmpty(confirmation) ? Task.FromResult(RejectedConfirmation())
        : string.Equals(confirmation, renewal.FriendlyName, StringComparison.Ordinal) ? runner.RunAsync(commandFactory.CreateRevoke(executablePath, renewal.Id), null, cancellationToken) : Task.FromResult(RejectedConfirmation());

    private static OperationResult RejectedConfirmation() =>
        new(OperationStatus.Failed, null, TimeSpan.Zero, [], "confirmation.name.mismatch");

    private static OperationResult ReadOnlyRenewal() =>
        new(OperationStatus.Failed, null, TimeSpan.Zero, [], "renewal.read_only");
}
