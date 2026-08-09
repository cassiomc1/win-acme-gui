using FluentAssertions;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;

namespace WinAcmeGui.Application.Tests.Certificates;

public sealed class CertificateCommandFactoryTests
{
    [Fact]
    public void Creates_masked_staging_command_from_validated_draft()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "certificatestore", AcceptTerms: true);

        var command = new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, true);

        command.DisplayText.Should().Contain("--source manual")
            .And.Contain("--test")
            .And.Contain("--host example.com")
            .And.Contain("--validation selfhosting")
            .And.Contain("--validationmode http-01")
            .And.Contain("--accepttos")
            .And.NotContain("--validation http-01");
    }

    [Fact]
    public void Creates_tls_alpn_command_with_selfhosting_plugin()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "tls-alpn-01", "ec", "certificatestore", AcceptTerms: true);

        var command = new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, false);

        command.DisplayText.Should().Contain("--validation selfhosting")
            .And.Contain("--validationmode tls-alpn-01")
            .And.Contain("--csr ec")
            .And.NotContain("--test");
    }

    [Fact]
    public void Refuses_certificate_command_without_terms_acceptance()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "certificatestore");

        var act = () => new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adds_the_required_PEM_output_switch()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "pemfiles", AcceptTerms: true, StoragePath: @"C:\certificates");

        var command = new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, false);

        command.DisplayText.Should().Contain("--pemfilespath").And.Contain(@"C:\certificates");
    }

    [Fact]
    public void Adds_the_required_PFX_output_switch()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "pfxfile", AcceptTerms: true, StoragePath: @"C:\certificates");

        var command = new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, false);

        command.DisplayText.Should().Contain("--pfxfilepath").And.Contain(@"C:\certificates");
    }
}
