using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Certificates;

public sealed class ManageCertificate(
    IWinAcmeRunner runner,
    CertificateDraftValidator validator,
    WinAcmeCommandFactory commandFactory)
{
    public Task<OperationResult> CreateAsync(
        string executablePath,
        CertificateDraft draft,
        bool staging,
        IProgress<string>? output,
        CancellationToken cancellationToken)
    {
        var errors = validator.Validate(draft);
        if (errors.Count > 0)
        {
            return Task.FromResult(new OperationResult(
                OperationStatus.Failed,
                null,
                TimeSpan.Zero,
                errors.Select(error => error.Message).ToArray(),
                "certificate.validation.invalid"));
        }

        return runner.RunAsync(commandFactory.CreateCertificate(executablePath, draft, staging), output, cancellationToken);
    }
}
