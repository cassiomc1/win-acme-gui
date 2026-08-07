using FluentAssertions;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Domain.Tests.Installations;

public sealed class WinAcmeInstallationTests
{
    [Fact]
    public void Create_rejects_relative_executable_path()
    {
        var act = () => WinAcmeInstallation.Create(
            "wacs.exe",
            new WinAcmeVersion(2, 2, 9, 1),
            @"C:\ProgramData\win-acme",
            AcmeEndpoint.Production);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sensitive_argument_never_reveals_value_in_display_text()
    {
        var argument = SensitiveArgument.Secret("--pfxpassword", "correct horse");

        argument.DisplayValue.Should().Be("••••••••");
        argument.Value.Should().Be("correct horse");
    }
}
