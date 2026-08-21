using FluentAssertions;
using WinAcmeGui.Application.Certificates;

namespace WinAcmeGui.Application.Tests.Certificates;

public sealed class CertificateDraftValidatorTests
{
    [Fact]
    public void Manual_source_requires_valid_domain()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", [], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().ContainSingle(x => x.Code == "certificate.domains.required");
    }

    [Fact]
    public void Rejects_invalid_domain()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["not a domain"], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().ContainSingle(x => x.Code == "certificate.domain.invalid");
    }

    [Fact]
    public void Accepts_http01_validation_mode()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().NotContain(x => x.Code == "certificate.validation.invalid");
    }

    [Fact]
    public void Rejects_generic_dns_without_a_provider_plugin()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["example.com"], "dns", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().ContainSingle(x => x.Code == "certificate.validation.invalid");
    }

    [Fact]
    public void Requires_explicit_terms_acceptance_for_unattended_creation()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "certificatestore"));

        errors.Should().Contain(x => x.Code == "certificate.terms.required");
    }

    [Fact]
    public void PEM_storage_requires_an_absolute_output_path()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft(
            "manual", ["example.com"], "http-01", "rsa", "pemfiles", AcceptTerms: true));

        errors.Should().Contain(x => x.Code == "certificate.storage.path.required");
    }

    [Fact]
    public void PFX_storage_accepts_an_absolute_output_path()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft(
            "manual", ["example.com"], "http-01", "rsa", "pfxfile", AcceptTerms: true, StoragePath: @"C:\certificates"));

        errors.Should().NotContain(x => x.Code.StartsWith("certificate.storage.path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-abc.example.com")]
    [InlineData("abc-.example.com")]
    [InlineData("12345.678")]
    public void Rejects_labels_with_edge_hyphens_and_numeric_only_names(string domain)
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", [domain], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().Contain(x => x.Code == "certificate.domain.invalid");
    }

    [Fact]
    public void Rejects_wildcard_domains_with_a_dns_specific_message()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["*.example.com"], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().ContainSingle(x => x.Code == "certificate.domain.wildcard");
    }

    [Fact]
    public void Rejects_duplicate_domains_case_insensitively()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft(
            "manual", ["Example.com", "example.com"], "http-01", "rsa", "certificatestore", AcceptTerms: true));

        errors.Should().Contain(x => x.Code == "certificate.domains.duplicate");
    }

    [Fact]
    public void Rejects_display_name_style_email_addresses()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft(
            "manual", ["example.com"], "http-01", "rsa", "certificatestore", EmailAddress: "Bob <bob@example.com>", AcceptTerms: true));

        errors.Should().Contain(x => x.Code == "certificate.email.invalid");
    }
}
