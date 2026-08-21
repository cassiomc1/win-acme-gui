using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class PackageSignatureVerifierTests
{
    [Fact]
    public void Recognizes_the_official_win_acme_self_signed_publisher_identity()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=WACS, E=win.acme.simple@gmail.com",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        WindowsAuthenticodeSignatureVerifier.IsApprovedWinAcmeIdentity(certificate)
            .Should().BeTrue();
    }
}
