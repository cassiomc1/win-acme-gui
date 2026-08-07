using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Renewals;

public interface IRenewalReader
{
    Task<IReadOnlyList<RenewalReadResult>> ReadDirectoryAsync(string configurationPath, CancellationToken cancellationToken);
}

public sealed record RenewalReadResult(
    string SourcePath,
    bool IsReadable,
    bool IsEditable,
    Renewal? Renewal,
    IReadOnlyList<RenewalDiagnostic> Diagnostics)
{
    public static RenewalReadResult Invalid(string path, params RenewalDiagnostic[] diagnostics) =>
        new(path, false, false, null, diagnostics);
}
