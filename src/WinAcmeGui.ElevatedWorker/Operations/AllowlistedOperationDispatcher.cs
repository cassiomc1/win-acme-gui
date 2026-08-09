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
    private static readonly HashSet<WinAcmeOperation> Allowed =
    [WinAcmeOperation.Renew, WinAcmeOperation.Cancel, WinAcmeOperation.Revoke, WinAcmeOperation.Create];

    public Task<ElevatedDispatchResult> DispatchAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Kind, "validated-win-acme", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.ExecutablePath)
            || request.Arguments is null
            || !IsAbsolutePath(request.ExecutablePath)
            || !GetFileName(request.ExecutablePath).Equals("wacs.exe", StringComparison.OrdinalIgnoreCase) && !GetFileName(request.ExecutablePath).Equals("wacs", StringComparison.OrdinalIgnoreCase)
            || request.Operation is not { } operation
            || !Allowed.Contains(operation)
            || !ArgumentsMatchOperation(operation, request.Arguments))
            return Task.FromResult(new ElevatedDispatchResult("elevation.operation.not_allowed", null));

        return Task.FromResult(new ElevatedDispatchResult(null, operation));
    }

    private static string GetFileName(string path) => path.Replace('\\', '/').Split('/').Last();

    private static bool IsAbsolutePath(string path) =>
        Path.IsPathFullyQualified(path)
        || path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');

    private static bool ArgumentsMatchOperation(WinAcmeOperation operation, IReadOnlyList<string> arguments)
    {
        var allowed = operation switch
        {
            WinAcmeOperation.Renew => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--renew", "--id", "--force", "--friendlyname" },
            WinAcmeOperation.Cancel => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--cancel", "--id", "--friendlyname" },
            WinAcmeOperation.Revoke => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--revoke", "--id", "--friendlyname" },
            WinAcmeOperation.Create => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--source", "--host", "--validation", "--validationmode", "--store", "--csr", "--emailaddress", "--pemfilespath", "--pfxfilepath", "--accepttos", "--test" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        var valueOptions = operation switch
        {
            WinAcmeOperation.Renew => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--id", "--friendlyname" },
            WinAcmeOperation.Cancel => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--id", "--friendlyname" },
            WinAcmeOperation.Revoke => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--id", "--friendlyname" },
            WinAcmeOperation.Create => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--source", "--host", "--validation", "--validationmode", "--store", "--csr", "--emailaddress", "--pemfilespath", "--pfxfilepath" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        var required = operation switch
        {
            WinAcmeOperation.Renew => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--renew", "--id" },
            WinAcmeOperation.Cancel => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--cancel", "--id" },
            WinAcmeOperation.Revoke => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--revoke", "--id" },
            WinAcmeOperation.Create => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--source", "--host", "--validation", "--validationmode", "--store", "--csr", "--accepttos" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingValue = null;

        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (pendingValue is not null) return false;
                if (!allowed.Contains(argument)) return false;
                if (!found.Add(argument)) return false;
                pendingValue = valueOptions.Contains(argument) ? argument : null;
                continue;
            }

            if (pendingValue is null || string.IsNullOrWhiteSpace(argument)) return false;
            values[pendingValue] = argument;
            pendingValue = null;
        }
        if (pendingValue is not null || !required.All(found.Contains)) return false;

        return operation switch
        {
            WinAcmeOperation.Renew or WinAcmeOperation.Cancel or WinAcmeOperation.Revoke =>
                values.TryGetValue("--id", out var id) && !string.IsNullOrWhiteSpace(id),
            WinAcmeOperation.Create => ValidateCreate(values, found),
            _ => false
        };
    }

    private static bool ValidateCreate(IReadOnlyDictionary<string, string> values, IReadOnlySet<string> found)
    {
        if (!values.TryGetValue("--source", out var source) || !source.Equals("manual", StringComparison.OrdinalIgnoreCase)) return false;
        if (!values.TryGetValue("--validation", out var validation) || !validation.Equals("selfhosting", StringComparison.OrdinalIgnoreCase)) return false;
        if (!values.TryGetValue("--validationmode", out var validationMode) || validationMode is not ("http-01" or "tls-alpn-01")) return false;
        if (!values.TryGetValue("--store", out var store) || store is not ("certificatestore" or "pemfiles" or "pfxfile")) return false;
        if (!values.TryGetValue("--csr", out var csr) || csr is not ("rsa" or "ec")) return false;
        var hasPemPath = found.Contains("--pemfilespath");
        var hasPfxPath = found.Contains("--pfxfilepath");
        if (store.Equals("pemfiles", StringComparison.OrdinalIgnoreCase)
            && (!hasPemPath || hasPfxPath || !IsAbsolutePath(values["--pemfilespath"]))) return false;
        if (store.Equals("pfxfile", StringComparison.OrdinalIgnoreCase)
            && (!hasPfxPath || hasPemPath || !IsAbsolutePath(values["--pfxfilepath"]))) return false;
        if (store.Equals("certificatestore", StringComparison.OrdinalIgnoreCase) && (hasPemPath || hasPfxPath)) return false;
        return true;
    }

}
