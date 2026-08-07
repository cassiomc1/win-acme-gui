using FluentAssertions;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Infrastructure.Operations;

namespace WinAcmeGui.Infrastructure.Tests.Operations;

public sealed class WinAcmeProcessRunnerTests
{
    [Fact]
    public async Task Runs_fake_process_and_captures_output()
    {
        var runner = new WinAcmeProcessRunner();
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var command = new WinAcmeCommand(
            executable,
            OperatingSystem.IsWindows()
                ? [SensitiveArgument.Plain("/c", "echo ok")]
                : [SensitiveArgument.Plain("-c", "printf ok")]);

        var result = await runner.RunAsync(command, null, CancellationToken.None);

        result.Status.Should().Be(OperationStatus.Succeeded);
        result.Output.Should().Contain(x => x.Contains("ok", StringComparison.Ordinal));
    }
}
