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
        var request = ElevatedRequest.RunValidatedWinAcme(@"C:\wacs.exe", WinAcmeOperation.Renew, "--renew", "--id", "renewal-id");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().BeNull();
        result.AcceptedOperation.Should().Be(WinAcmeOperation.Renew);
    }

    [Theory]
    [InlineData("/renew")]
    [InlineData("@C:\\temp\\evil.rsp")]
    [InlineData("-p\nwhoami")]
    public async Task Rejects_values_that_look_like_switches_response_files_or_control_payloads(string smuggled)
    {
        var request = ElevatedRequest.RunValidatedWinAcme(
            @"C:\wacs.exe",
            WinAcmeOperation.Renew,
            "--renew",
            "--id",
            smuggled);

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
        result.AcceptedOperation.Should().BeNull();
    }

    [Fact]
    public async Task Rejects_relative_wacs_path()
    {
        var request = ElevatedRequest.RunValidatedWinAcme("wacs.exe", WinAcmeOperation.Renew, "--renew", "--id", "renewal-id");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }

    [Fact]
    public async Task Rejects_unbound_positional_arguments()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(
            @"C:\wacs.exe",
            WinAcmeOperation.Renew,
            "--renew",
            "--id",
            "renewal-id",
            "whoami");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }

    [Fact]
    public async Task Rejects_operation_without_its_required_switch()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(@"C:\wacs.exe", WinAcmeOperation.Renew, "--id", "renewal-id");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }

    [Fact]
    public async Task Allows_the_non_interactive_certificate_switches()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(
            @"C:\wacs.exe",
            WinAcmeOperation.Create,
            "--source", "manual",
            "--host", "example.com",
            "--validation", "selfhosting",
            "--validationmode", "http-01",
            "--store", "certificatestore",
            "--csr", "rsa",
            "--emailaddress", "admin@example.com",
            "--accepttos",
            "--test");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().BeNull();
        result.AcceptedOperation.Should().Be(WinAcmeOperation.Create);
    }

    [Fact]
    public async Task Allows_explicit_PEM_storage_path()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(
            @"C:\wacs.exe",
            WinAcmeOperation.Create,
            "--source", "manual",
            "--host", "example.com",
            "--validation", "selfhosting",
            "--validationmode", "http-01",
            "--store", "pemfiles",
            "--pemfilespath", @"C:\certificates",
            "--csr", "rsa",
            "--accepttos");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task Rejects_arguments_for_a_different_operation()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(
            @"C:\wacs.exe",
            WinAcmeOperation.Renew,
            "--revoke",
            "--id",
            "renewal-id");

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }

    [Fact]
    public async Task Rejects_reserved_operations_until_their_dispatcher_contract_exists()
    {
        var request = ElevatedRequest.RunValidatedWinAcme(@"C:\wacs.exe", WinAcmeOperation.RestoreBackup);

        var result = await new AllowlistedOperationDispatcher().DispatchAsync(request, CancellationToken.None);

        result.ErrorCode.Should().Be("elevation.operation.not_allowed");
    }
}
