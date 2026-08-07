using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class PackageVerifierTests
{
    [Fact]
    public void Rejects_non_https_or_unapproved_host()
    {
        var verifier = new PackageVerifier(["github.com"]);

        verifier.IsApproved(new Uri("http://github.com/wacs.zip")).Should().BeFalse();
        verifier.IsApproved(new Uri("https://evil.example/wacs.zip")).Should().BeFalse();
        verifier.IsApproved(new Uri("https://github.com/wacs.zip")).Should().BeTrue();
    }
}
