using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Presentation;

public sealed partial class ShellViewModel
{
    /// <summary>Normal renewal runs directly; a forced renewal asks for one extra confirmation first.</summary>
    public async Task<OperationResult?> RenewSelectedAsync(bool force)
    {
        var candidate = ActiveCandidate;
        var row = SelectedRenewal;
        if (candidate is null || row is null)
        {
            _interaction.ShowMessage(Culture["Information"], Culture["SelectRenewalFirst"]);
            return null;
        }

        var operationKey = force ? "OperationForceRenew" : "OperationRenew";
        if (force && !_interaction.Confirm(Culture["ConfirmForceTitle"], Culture["ConfirmForceMessage"]))
            return null;

        return await RunMutationAsync(
            token => _services.Renewals.RenewAsync(candidate.ExecutablePath, row.Renewal, force, token),
            operationKey);
    }

    public Task<OperationResult?> CancelSelectedAsync() =>
        RunConfirmedMutationAsync(
            "OperationCancel",
            "ConfirmCancelTitle",
            "ConfirmCancelMessage",
            (candidate, renewal, confirmation, token) =>
                _services.Renewals.CancelAsync(candidate.ExecutablePath, renewal, confirmation, token));

    public Task<OperationResult?> RevokeSelectedAsync() =>
        RunConfirmedMutationAsync(
            "OperationRevoke",
            "ConfirmRevokeTitle",
            "ConfirmRevokeMessage",
            (candidate, renewal, confirmation, token) =>
                _services.Renewals.RevokeAsync(candidate.ExecutablePath, renewal, confirmation, token));

    /// <summary>Cancel and Revoke both require the operator to retype the renewal's friendly name.</summary>
    private async Task<OperationResult?> RunConfirmedMutationAsync(
        string operationKey,
        string titleKey,
        string messageKey,
        Func<Application.Discovery.InstallationCandidate, Domain.Renewals.Renewal, string, CancellationToken, Task<OperationResult>> operation)
    {
        var candidate = ActiveCandidate;
        var row = SelectedRenewal;
        if (candidate is null || row is null)
        {
            _interaction.ShowMessage(Culture["Information"], Culture["SelectRenewalFirst"]);
            return null;
        }

        var typed = _interaction.PromptForConfirmationText(
            Culture[titleKey],
            $"{Culture[messageKey]}\n\n{row.FriendlyName}",
            row.FriendlyName);
        if (typed is null) return null;
        if (!typed.Equals(row.FriendlyName, StringComparison.Ordinal))
        {
            Status = Culture["ConfirmationMismatch"];
            Log(operationKey, ActivityOutcome.Failed, Status);
            _interaction.ShowMessage(Culture["Warning"], Culture["ConfirmationMismatch"], DialogSeverity.Warning);
            return null;
        }

        return await RunMutationAsync(
            token => operation(candidate, row.Renewal, typed, token),
            operationKey);
    }

    private async Task<OperationResult?> RunMutationAsync(
        Func<CancellationToken, Task<OperationResult>> operation,
        string operationKey)
    {
        if (IsBusy) return null;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        try
        {
            var result = await operation(cancellation.Token);
            var succeeded = result.Status == OperationStatus.Succeeded;
            Status = succeeded
                ? Describe(operationKey, "OperationSucceeded")
                : result.ErrorCode ?? Describe(operationKey, "OperationFailed");
            Log(operationKey, ActivityEntry.FromStatus(result.Status), Status);

            if (succeeded)
            {
                try
                {
                    await RefreshActiveInventoryAsync(cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Status = $"{Describe(operationKey, "OperationSucceeded")} {Culture["RefreshFailed"]} {ex.Message}";
                    Log(operationKey, ActivityOutcome.Information, Status);
                }
            }
            else
            {
                _interaction.ShowMessage(Culture["Error"], Status, DialogSeverity.Error);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = Describe(operationKey, "OperationCancelled");
            Log(operationKey, ActivityOutcome.Cancelled, Status);
            return new(OperationStatus.Cancelled, null, TimeSpan.Zero, [], "operation.cancelled");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            Log(operationKey, ActivityOutcome.Failed, ex.Message);
            _interaction.ShowMessage(Culture["Error"], ex.Message, DialogSeverity.Error);
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "operation.exception");
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
            Raise(nameof(CanMutateSelectedRenewal));
        }
    }

    private async Task RefreshActiveInventoryAsync(CancellationToken cancellationToken)
    {
        var candidate = ActiveCandidate;
        if (candidate is null) return;
        SelectedRenewal = null;
        await LoadInventoryAsync(candidate, cancellationToken);
    }

    private CancellationTokenSource BeginCancellableOperation()
    {
        var cancellation = new CancellationTokenSource();
        _activeOperationCancellation = cancellation;
        Raise(nameof(CanCancelOperation));
        CancelOperationCommand.RaiseCanExecuteChanged();
        return cancellation;
    }

    private void EndCancellableOperation(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_activeOperationCancellation, cancellation)) return;
        _activeOperationCancellation = null;
        Raise(nameof(CanCancelOperation));
        CancelOperationCommand.RaiseCanExecuteChanged();
    }

    private string Describe(string operationKey, string outcomeKey) => $"{Culture[operationKey]} {Culture[outcomeKey]}";

    private void Log(string operationKey, ActivityOutcome outcome, string detail)
    {
        Activity.Insert(0, new ActivityEntry(_clock(), operationKey, outcome, detail, Culture));
        while (Activity.Count > 200) Activity.RemoveAt(Activity.Count - 1);
        Raise(nameof(HasActivity));
        ClearActivityCommand.RaiseCanExecuteChanged();
        CopyActivityCommand.RaiseCanExecuteChanged();
    }
}
