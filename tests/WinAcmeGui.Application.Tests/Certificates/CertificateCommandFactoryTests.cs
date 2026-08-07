using FluentAssertions;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;

namespace WinAcmeGui.Application.Tests.Certificates;

public sealed class CertificateCommandFactoryTests
{
    [Fact]
    public void Creates_masked_staging_command_from_validated_draft()
    {
        var draft = new CertificateDraft("manual", ["example.com"], "http", "rsa", "certificatestore");

        var command = new WinAcmeCommandFactory().CreateCertificate(@"C:\wacs.exe", draft, true);

        command.DisplayText.Should().Contain("--source manual").And.Contain("--test").And.Contain("--host example.com");
    }
}
