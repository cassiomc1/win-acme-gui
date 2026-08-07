using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Application.Certificates;

namespace WinAcmeGui.Application.Operations;

public sealed class WinAcmeCommandFactory
{
    public WinAcmeCommand CreateRenew(string executablePath, string renewalId, bool force)
    {
        var arguments = new List<SensitiveArgument>
        {
            SensitiveArgument.Plain("--renew", string.Empty),
            SensitiveArgument.Plain("--id", renewalId)
        };
        if (force) arguments.Add(SensitiveArgument.Plain("--force", string.Empty));
        return new(executablePath, arguments);
    }

    public WinAcmeCommand CreateList(string executablePath) =>
        new(executablePath, [SensitiveArgument.Plain("--list", string.Empty)]);

    public WinAcmeCommand CreateCancel(string executablePath, string renewalId) =>
        new(executablePath, [SensitiveArgument.Plain("--cancel", string.Empty), SensitiveArgument.Plain("--id", renewalId)]);

    public WinAcmeCommand CreateRevoke(string executablePath, string renewalId) =>
        new(executablePath, [SensitiveArgument.Plain("--revoke", string.Empty), SensitiveArgument.Plain("--id", renewalId)]);

    public WinAcmeCommand CreateCertificate(string executablePath, CertificateDraft draft, bool staging)
    {
        var arguments = new List<SensitiveArgument>
        {
            SensitiveArgument.Plain("--source", draft.Source),
            SensitiveArgument.Plain("--host", string.Join(',', draft.Domains)),
            SensitiveArgument.Plain("--validation", draft.Validation),
            SensitiveArgument.Plain("--store", draft.Store),
            SensitiveArgument.Plain("--csr", draft.KeyType)
        };
        if (staging) arguments.Add(SensitiveArgument.Plain("--test", string.Empty));
        return new(executablePath, arguments);
    }
}
