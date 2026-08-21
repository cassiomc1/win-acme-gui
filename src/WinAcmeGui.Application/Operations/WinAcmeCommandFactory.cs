using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Application.Certificates;

namespace WinAcmeGui.Application.Operations;

public sealed class WinAcmeCommandFactory
{
    public WinAcmeCommand CreateRenew(string executablePath, string renewalId, bool force)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renewalId);
        var arguments = new List<SensitiveArgument>
        {
            SensitiveArgument.Plain("--renew", string.Empty),
            SensitiveArgument.Plain("--id", renewalId)
        };
        if (force) arguments.Add(SensitiveArgument.Plain("--force", string.Empty));
        return new(executablePath, arguments);
    }

    public WinAcmeCommand CreateCancel(string executablePath, string renewalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renewalId);
        return new(executablePath, [SensitiveArgument.Plain("--cancel", string.Empty), SensitiveArgument.Plain("--id", renewalId)]);
    }

    public WinAcmeCommand CreateRevoke(string executablePath, string renewalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renewalId);
        return new(executablePath, [SensitiveArgument.Plain("--revoke", string.Empty), SensitiveArgument.Plain("--id", renewalId)]);
    }

    public WinAcmeCommand CreateCertificate(string executablePath, CertificateDraft draft, bool staging)
    {
        if (!draft.AcceptTerms) throw new ArgumentException("Explicit terms acceptance is required.", nameof(draft));
        var validationMode = draft.Validation switch
        {
            "http-01" or "tls-alpn-01" => draft.Validation,
            _ => throw new ArgumentException("Unsupported validation mode.", nameof(draft))
        };
        if (draft.Store is not ("certificatestore" or "pemfiles" or "pfxfile"))
            throw new ArgumentException("Unsupported certificate store.", nameof(draft));
        if (draft.Store is "pemfiles" or "pfxfile"
            && !IsAbsolutePath(draft.StoragePath))
            throw new ArgumentException("PEM/PFX storage requires an absolute output path.", nameof(draft));
        var arguments = new List<SensitiveArgument>
        {
            SensitiveArgument.Plain("--source", draft.Source),
            SensitiveArgument.Plain("--host", string.Join(',', draft.Domains)),
            // The selfhosting plugin serves both HTTP-01 and TLS-ALPN-01; the mode is chosen by --validationmode.
            SensitiveArgument.Plain("--validation", "selfhosting"),
            SensitiveArgument.Plain("--validationmode", validationMode),
            SensitiveArgument.Plain("--store", draft.Store),
            SensitiveArgument.Plain("--csr", draft.KeyType)
        };
        if (staging) arguments.Add(SensitiveArgument.Plain("--test", string.Empty));
        if (!string.IsNullOrWhiteSpace(draft.EmailAddress)) arguments.Add(SensitiveArgument.Plain("--emailaddress", draft.EmailAddress));
        if (draft.Store.Equals("pemfiles", StringComparison.OrdinalIgnoreCase))
            arguments.Add(SensitiveArgument.Plain("--pemfilespath", draft.StoragePath));
        if (draft.Store.Equals("pfxfile", StringComparison.OrdinalIgnoreCase))
            arguments.Add(SensitiveArgument.Plain("--pfxfilepath", draft.StoragePath));
        // Guarded above: reaching this point implies AcceptTerms.
        arguments.Add(SensitiveArgument.Plain("--accepttos", string.Empty));
        return new(executablePath, arguments);
    }

    private static bool IsAbsolutePath(string value) =>
        Path.IsPathFullyQualified(value)
        || value.Length >= 3
        && char.IsLetter(value[0])
        && value[1] == ':'
        && (value[2] == '\\' || value[2] == '/');
}
