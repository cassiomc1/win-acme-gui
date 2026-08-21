using System.Net;
using System.Net.Mail;

namespace WinAcmeGui.Application.Certificates;

public sealed class CertificateDraftValidator
{
    private const int MaxSansPerCertificate = 100;

    public IReadOnlyList<CertificateValidationError> Validate(CertificateDraft draft)
    {
        var errors = new List<CertificateValidationError>();
        ArgumentNullException.ThrowIfNull(draft);
        if (!string.Equals(draft.Source, "manual", StringComparison.OrdinalIgnoreCase))
            errors.Add(new("certificate.source.unsupported", "The selected source is not available in this installation."));
        if (draft.Domains is null || draft.Domains.Count == 0)
        {
            errors.Add(new("certificate.domains.required", "At least one domain is required."));
        }
        else
        {
            if (draft.Domains.Count > MaxSansPerCertificate)
                errors.Add(new("certificate.domains.limit", $"Let's Encrypt accepts at most {MaxSansPerCertificate} names per order."));
            if (draft.Domains.Distinct(StringComparer.OrdinalIgnoreCase).Count() != draft.Domains.Count)
                errors.Add(new("certificate.domains.duplicate", "Duplicate host names are not allowed."));
            foreach (var domain in draft.Domains)
            {
                if (domain is null) continue;
                if (domain.StartsWith('*'))
                {
                    errors.Add(new("certificate.domain.wildcard", $"'{domain}' requires DNS validation, which this tool does not support."));
                    continue;
                }
                if (domain.Length > 253 || domain.Contains(' ') || !IsDomain(domain))
                    errors.Add(new("certificate.domain.invalid", $"'{domain}' is not a valid DNS name."));
            }
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
            else if (StoragePathHasInvalidCharacters(draft.StoragePath))
                errors.Add(new("certificate.storage.path.invalid", "The output path contains characters that are not valid in a Windows folder name."));
        }
        // MailAddress also parses display-name forms ("Bob <bob@example.com>"); wacs expects the
        // plain address only, so reject anything with whitespace, angle brackets or multiple @.
        if (!string.IsNullOrWhiteSpace(draft.EmailAddress)
            && (!MailAddress.TryCreate(draft.EmailAddress, out _)
                || draft.EmailAddress.Contains('<')
                || draft.EmailAddress.Any(char.IsWhiteSpace)
                || draft.EmailAddress.Count(character => character == '@') != 1))
            errors.Add(new("certificate.email.invalid", "Enter a valid account email address or leave it empty to use the existing account."));
        if (!draft.AcceptTerms)
            errors.Add(new("certificate.terms.required", "Accept the Let's Encrypt terms before running an unattended certificate operation."));
        return errors;
    }

    private static bool IsDomain(string value)
    {
        if (IPAddress.TryParse(value, out _)) return false;
        var labels = value.Split('.');
        // A purely numeric TLD ("12345.678") is not a registrable DNS name.
        return labels.Length >= 2
            && labels[^1].Any(char.IsLetter)
            && labels.All(label =>
                label.Length is >= 1 and <= 63
                && label[0] != '-'
                && label[^1] != '-'
                && label.All(character => char.IsLetterOrDigit(character) || character == '-'));
    }

    private static bool StoragePathHasInvalidCharacters(string value)
    {
        try
        {
            Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return true;
        }
        // Separators (: \\ /) are legal inside an absolute path; these are the characters that are
        // invalid in any position of a Windows path.
        return value.IndexOfAny(['<', '>', '|', '"']) >= 0 || value.Any(char.IsControl);
    }

    private static bool IsAbsolutePath(string value) =>
        Path.IsPathFullyQualified(value)
        || value.Length >= 3
        && char.IsLetter(value[0])
        && value[1] == ':'
        && (value[2] == '\\' || value[2] == '/');
}
