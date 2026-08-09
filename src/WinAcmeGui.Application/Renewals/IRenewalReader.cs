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
        new(
            path,
            false,
            false,
            new Renewal(
                GetFallbackId(path),
                GetFallbackId(path),
                [],
                RenewalStatus.Unreadable,
                false,
                path,
                diagnostics),
            diagnostics);

    public Renewal ToDisplayRenewal() => Renewal ?? new Renewal(
        GetFallbackId(SourcePath),
        GetFallbackId(SourcePath),
        [],
        RenewalStatus.Unreadable,
        false,
        SourcePath,
        Diagnostics);

    private static string GetFallbackId(string path) =>
        Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
}
