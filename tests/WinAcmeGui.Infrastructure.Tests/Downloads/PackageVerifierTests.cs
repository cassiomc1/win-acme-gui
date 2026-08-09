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

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not-a-digest", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    public void Validates_sha256_digests(string? digest, bool expected) =>
        PackageVerifier.IsSha256Digest(digest).Should().Be(expected);
}
