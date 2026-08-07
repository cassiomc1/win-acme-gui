using FluentAssertions;
using WinAcmeGui.ElevatedWorker.Operations;

namespace WinAcmeGui.ElevatedWorker.Tests.Operations;

public sealed class AllowlistedOperationDispatcherTests
{
    [Fact]
    public async Task Rejects_arbitrary_executable_request()
    {
        var request = ElevatedRequest.RawProcess("cmd.exe", ["/c", "whoami"]);

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }

    [Fact]
    public async Task Allows_only_validated_win_acme_operation()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(@"C:\wacs.exe", WinAcmeOperation.Renew, "renewal-id");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().NotBe("elevation.operation.not_allowed");
        result.AcceptedOperation.Should().Be(WinAcmeOperation.Renew);
    }
}
