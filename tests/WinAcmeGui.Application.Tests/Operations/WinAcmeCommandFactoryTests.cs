using FluentAssertions;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Tests.Operations;

public sealed class WinAcmeCommandFactoryTests
{
    [Fact]
    public void Forced_renewal_uses_tokens_not_shell_text()
    {
        var command = new WinAcmeCommandFactory().CreateRenew(@"C:\wacs.exe", "renewal-id", true);

        command.Arguments.SelectMany(x => string.IsNullOrEmpty(x.Value) ? new[] { x.Name } : new[] { x.Name, x.Value })
            .Should().Equal("--renew", "--id", "renewal-id", "--force");
    }

    [Fact]
    public void Preview_masks_secret_and_preserves_non_secret_arguments()
    {
        var command = new WinAcmeCommand(
            @"C:\wacs.exe",
            [SensitiveArgument.Plain("--source", "manual"), SensitiveArgument.Secret("--pfxpassword", "S3cret!")]);

        command.DisplayText.Should().Contain("--source manual").And.Contain("--pfxpassword ••••••••");
        command.DisplayText.Should().NotContain("S3cret!");
    }
}
