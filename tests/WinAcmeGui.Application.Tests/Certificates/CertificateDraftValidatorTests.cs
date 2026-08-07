using FluentAssertions;
using WinAcmeGui.Application.Certificates;

namespace WinAcmeGui.Application.Tests.Certificates;

public sealed class CertificateDraftValidatorTests
{
    [Fact]
    public void Manual_source_requires_valid_domain()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", [], "http", "rsa", "certificatestore"));

        errors.Should().ContainSingle(x => x.Code == "certificate.domains.required");
    }

    [Fact]
    public void Rejects_invalid_domain()
    {
        var errors = new CertificateDraftValidator().Validate(new CertificateDraft("manual", ["not a domain"], "http", "rsa", "certificatestore"));

        errors.Should().ContainSingle(x => x.Code == "certificate.domain.invalid");
    }
}
