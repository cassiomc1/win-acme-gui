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
        var executable = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("ComSpec")!
            : "/bin/sh";
        var command = new WinAcmeCommand(
            executable,
            OperatingSystem.IsWindows()
                ? [SensitiveArgument.Plain("/c", "echo ok")]
                : [SensitiveArgument.Plain("-c", "printf ok")]);

        var result = await runner.RunAsync(command, null, CancellationToken.None);

        result.Status.Should().Be(OperationStatus.Succeeded);
        result.Output.Should().Contain(x => x.Contains("ok", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Captures_stdout_and_stderr_without_losing_lines()
    {
        var runner = new WinAcmeProcessRunner();
        var executable = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("ComSpec")!
            : "/bin/sh";
        var command = new WinAcmeCommand(
            executable,
            OperatingSystem.IsWindows()
                ? [SensitiveArgument.Plain("/c", "echo out&echo err 1>&2")]
                : [SensitiveArgument.Plain("-c", "printf 'out\\n'; printf 'err\\n' 1>&2")]);

        var result = await runner.RunAsync(command, null, CancellationToken.None);

        result.Output.Should().Contain("out").And.Contain("err");
    }

    [Fact]
    public async Task Rejects_relative_executable_paths()
    {
        var runner = new WinAcmeProcessRunner();
        var command = new WinAcmeCommand("sh", [SensitiveArgument.Plain("-c", "printf ok")]);

        var act = () => runner.RunAsync(command, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Cancellation_terminates_long_running_child()
    {
        var runner = new WinAcmeProcessRunner(TimeSpan.FromSeconds(5));
        var executable = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("ComSpec")!
            : "/bin/sh";
        var command = new WinAcmeCommand(
            executable,
            OperatingSystem.IsWindows()
                ? [SensitiveArgument.Plain("/c", "ping -n 30 127.0.0.1 > nul")]
                : [SensitiveArgument.Plain("-c", "sleep 30")]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var result = await runner.RunAsync(command, null, cancellation.Token);

        result.Status.Should().Be(OperationStatus.Cancelled);
        result.ErrorCode.Should().Be("operation.cancelled");
    }
}
