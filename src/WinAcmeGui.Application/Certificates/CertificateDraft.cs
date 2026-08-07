namespace WinAcmeGui.Application.Certificates;

public sealed record CertificateDraft(
    string Source,
    IReadOnlyList<string> Domains,
    string Validation,
    string KeyType,
    string Store);

public sealed record CertificateValidationError(string Code, string Message);
