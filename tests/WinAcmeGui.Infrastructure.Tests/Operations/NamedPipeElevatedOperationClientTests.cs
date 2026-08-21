using System.Text.Json;
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
    public void Protocol_request_round_trips_through_json_with_secret_metadata_intact()
    {
        var request = new ElevatedPipeRequest(
            "2",
            Guid.NewGuid().ToString("N"),
            "Create",
            @"C:\Program Files\win-acme\wacs.exe",
            [SensitiveArgument.Secret("--pfxpassword", "s3cret!"), SensitiveArgument.Plain("--host", "example.org")]);

        var restored = JsonSerializer.Deserialize<ElevatedPipeRequest>(JsonSerializer.Serialize(request));

        restored.Should().BeEquivalentTo(request);
        restored!.Arguments.Single(x => x.IsSecret).Value.Should().Be("s3cret!");
    }

    [Fact]
    public void Protocol_records_never_render_secret_values_or_tokens()
    {
        var request = new ElevatedPipeRequest(
            "2",
            "op-1",
            "Create",
            @"C:\wacs\wacs.exe",
            [SensitiveArgument.Secret("--pfxpassword", "s3cret!")]);

        var rendered = string.Concat(request.ToString(), new WinAcmeCommand(request.ExecutablePath, request.Arguments));

        rendered.Should().NotContain("s3cret!");
        rendered.Should().Contain("--pfxpassword=••••••••");
    }
}
