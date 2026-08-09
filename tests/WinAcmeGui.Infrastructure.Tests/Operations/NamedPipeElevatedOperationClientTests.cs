using FluentAssertions;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Infrastructure.Operations;

namespace WinAcmeGui.Infrastructure.Tests.Operations;

public sealed class NamedPipeElevatedOperationClientTests
{
    [Fact]
    public async Task Does_not_attempt_elevation_on_non_windows_hosts()
    {
        if (OperatingSystem.IsWindows()) return;
        var client = new NamedPipeElevatedOperationClient("/missing/WinAcmeGui.ElevatedWorker.exe");
        var command = new WinAcmeCommand(
            "/tmp/wacs",
            [SensitiveArgument.Plain("--renew", string.Empty), SensitiveArgument.Plain("--id", "renewal")]);

        var result = await client.RunAsync(command, null, CancellationToken.None);

        result.Status.Should().Be(OperationStatus.Failed);
        result.ErrorCode.Should().Be("elevation.windows.required");
    }

    [Fact]
    public void Protocol_request_keeps_secret_metadata_available_to_the_worker()
    {
        var request = new ElevatedPipeRequest(
            "1",
            "token",
            "operation",
            "Create",
            @"C:\Program Files\win-acme\wacs.exe",
            [SensitiveArgument.Secret("--pfxpassword", "secret")]);

        request.Arguments.Single().IsSecret.Should().BeTrue();
        request.ProtocolVersion.Should().Be("1");
    }
}
