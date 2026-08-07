namespace WinAcmeGui.Domain.Operations;

public sealed record SensitiveArgument(string Name, string Value, bool IsSecret)
{
    public string DisplayValue => IsSecret ? "••••••••" : Value;

    public static SensitiveArgument Plain(string name, string value) => new(name, value, false);

    public static SensitiveArgument Secret(string name, string value) => new(name, value, true);
}

public enum OperationStatus
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public sealed record OperationRequest(
    string OperationId,
    string ExecutablePath,
    IReadOnlyList<SensitiveArgument> Arguments);

public sealed record OperationResult(
    OperationStatus Status,
    int? ExitCode,
    TimeSpan Duration,
    IReadOnlyList<string> Output,
    string? ErrorCode);
