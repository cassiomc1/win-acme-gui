namespace WinAcmeGui.ElevatedWorker.Operations;

public enum WinAcmeOperation
{
    Renew,
    Cancel,
    Revoke,
    Create,
    RecreateScheduledTask,
    RestoreBackup
}

public sealed record ElevatedRequest(
    string Kind,
    string? ExecutablePath,
    WinAcmeOperation? Operation,
    IReadOnlyList<string> Arguments)
{
    public static ElevatedRequest RawProcess(string executablePath, IReadOnlyList<string> arguments) =>
        new("raw-process", executablePath, null, arguments);

    public static ElevatedRequest RunValidatedWinAcme(string executablePath, WinAcmeOperation operation, params string[] arguments) =>
        new("validated-win-acme", executablePath, operation, arguments);
}

public sealed record ElevatedDispatchResult(string? ErrorCode, WinAcmeOperation? AcceptedOperation);

public sealed class AllowlistedOperationDispatcher
{
    private static readonly HashSet<WinAcmeOperation> Allowed = Enum.GetValues<WinAcmeOperation>().ToHashSet();

    public Task<ElevatedDispatchResult> DispatchAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.Kind.Equals("validated-win-acme", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.ExecutablePath)
            || !GetFileName(request.ExecutablePath).Equals("wacs.exe", StringComparison.OrdinalIgnoreCase) && !GetFileName(request.ExecutablePath).Equals("wacs", StringComparison.OrdinalIgnoreCase)
            || request.Operation is not { } operation
            || !Allowed.Contains(operation))
            return Task.FromResult(new ElevatedDispatchResult("elevation.operation.not_allowed", null));

        return Task.FromResult(new ElevatedDispatchResult(null, operation));
    }

    private static string GetFileName(string path) => path.Replace('\\', '/').Split('/').Last();
}
