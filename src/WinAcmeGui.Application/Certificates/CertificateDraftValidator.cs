using System.Net;
using System.Net.Mail;

namespace WinAcmeGui.Application.Certificates;

public sealed class CertificateDraftValidator
{
    public IReadOnlyList<CertificateValidationError> Validate(CertificateDraft draft)
    {
        var errors = new List<CertificateValidationError>();
        if (!draft.Source.Equals("manual", StringComparison.OrdinalIgnoreCase))
            errors.Add(new("certificate.source.unsupported", "The selected source is not available in this installation."));
        if (draft.Domains.Count == 0)
            errors.Add(new("certificate.domains.required", "At least one domain is required."));
        foreach (var domain in draft.Domains)
        {
            if (domain.Length > 253 || domain.Contains(' ') || !IsDomain(domain))
                errors.Add(new("certificate.domain.invalid", $"'{domain}' is not a valid DNS name."));
        }
        if (draft.Validation is not ("http-01" or "tls-alpn-01"))
            errors.Add(new("certificate.validation.invalid", "Choose HTTP-01 or TLS-ALPN-01 validation."));
        if (draft.KeyType is not ("rsa" or "ec")) errors.Add(new("certificate.key.invalid", "Choose RSA or EC key type."));
        if (draft.Store is not ("certificatestore" or "pemfiles" or "pfxfile"))
            errors.Add(new("certificate.store.invalid", "Choose a supported certificate store."));
        if (draft.Store is "pemfiles" or "pfxfile")
        {
            if (string.IsNullOrWhiteSpace(draft.StoragePath))
                errors.Add(new("certificate.storage.path.required", "An absolute output path is required for PEM or PFX storage."));
            else if (!IsAbsolutePath(draft.StoragePath))
                errors.Add(new("certificate.storage.path.invalid", "The PEM or PFX output path must be absolute."));
        }
        if (!string.IsNullOrWhiteSpace(draft.EmailAddress) && !MailAddress.TryCreate(draft.EmailAddress, out _))
            errors.Add(new("certificate.email.invalid", "Enter a valid account email address or leave it empty to use the existing account."));
        if (!draft.AcceptTerms)
            errors.Add(new("certificate.terms.required", "Accept the Let's Encrypt terms before running an unattended certificate operation."));
        return errors;
    }

    private static bool IsDomain(string value) =>
        value.Split('.').Length >= 2 && value.Split('.').All(label => label.Length is > 0 and <= 63 && label.All(ch => char.IsLetterOrDigit(ch) || ch == '-')) && IPAddress.TryParse(value, out _) is false;

    private static bool IsAbsolutePath(string value) =>
        Path.IsPathFullyQualified(value)
        || value.Length >= 3
        && char.IsLetter(value[0])
        && value[1] == ':'
        && (value[2] == '\\' || value[2] == '/');
}
