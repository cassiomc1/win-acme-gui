namespace WinAcmeGui.Domain.Renewals;

public enum RenewalStatus
{
    Healthy,
    DueSoon,
    Failed,
    Expired,
    Unreadable
}

public sealed record Renewal(
    string Id,
    string FriendlyName,
    IReadOnlyList<string> Domains,
    RenewalStatus Status,
    bool IsEditable,
    string SourcePath,
    IReadOnlyList<RenewalDiagnostic> Diagnostics);

public sealed record RenewalDiagnostic(string Code, string Message, bool IsError = false);
