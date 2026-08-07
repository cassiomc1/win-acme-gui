using System.Net;

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
        if (draft.Validation is not ("http" or "dns" or "tls")) errors.Add(new("certificate.validation.invalid", "Choose HTTP, DNS or TLS validation."));
        if (draft.KeyType is not ("rsa" or "ec")) errors.Add(new("certificate.key.invalid", "Choose RSA or EC key type."));
        if (string.IsNullOrWhiteSpace(draft.Store)) errors.Add(new("certificate.store.required", "Choose a certificate store."));
        return errors;
    }

    private static bool IsDomain(string value) =>
        value.Split('.').Length >= 2 && value.Split('.').All(label => label.Length is > 0 and <= 63 && label.All(ch => char.IsLetterOrDigit(ch) || ch == '-')) && IPAddress.TryParse(value, out _) is false;
}
