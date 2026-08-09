using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Infrastructure.Operations;
using WinAcmeGui.Infrastructure.Downloads;
using WinAcmeGui.ElevatedWorker.Operations;

namespace WinAcmeGui.ElevatedWorker;

internal static class Program
{
    private const string ProtocolVersion = "1";

    private static async Task<int> Main(string[] args)
    {
        if (!TryReadOption(args, "--pipe", out var pipeName)
            || !TryReadOption(args, "--token", out var expectedToken))
            return 2;

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await client.ConnectAsync(30_000);
            using var reader = new StreamReader(client, leaveOpen: true);
            await using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            var line = await reader.ReadLineAsync();
            var response = await ProcessRequestAsync(line, expectedToken, client);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            return response.Result.Status == OperationStatus.Succeeded ? 0 : 1;
        }
        catch (IOException)
        {
            return 3;
        }
        catch (TimeoutException)
        {
            return 4;
        }
    }

    private static async Task<ElevatedPipeResponse> ProcessRequestAsync(
        string? line,
        string expectedToken,
        NamedPipeClientStream client)
    {
        if (string.IsNullOrWhiteSpace(line))
            return Failure("elevation.protocol.empty_request");

        ElevatedPipeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ElevatedPipeRequest>(line);
        }
        catch (JsonException)
        {
            return Failure("elevation.protocol.invalid_request");
        }
        catch (NotSupportedException)
        {
            return Failure("elevation.protocol.invalid_request");
        }

        if (request is null
            || request.ProtocolVersion != ProtocolVersion
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.Operation)
            || request.Arguments is null)
            return Failure("elevation.protocol.invalid_request");
        if (!SecureEquals(request.Token, expectedToken))
            return Failure("elevation.protocol.invalid_token");
        if (!Enum.TryParse<WinAcmeOperation>(request.Operation, true, out var operation))
            return Failure("elevation.operation.not_allowed");
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            return Failure("elevation.operation.not_allowed");
        var trustVerifier = new WindowsAuthenticodeSignatureVerifier();
        if (!await trustVerifier.IsTrustedAsync(request.ExecutablePath, CancellationToken.None))
            return Failure("elevation.executable.untrusted");

        var arguments = Flatten(request.Arguments);
        var dispatchRequest = new ElevatedRequest(
            "validated-win-acme",
            request.ExecutablePath,
            operation,
            arguments);
        var dispatcher = new AllowlistedOperationDispatcher();
        var dispatch = await dispatcher.DispatchAsync(dispatchRequest, CancellationToken.None);
        if (dispatch.ErrorCode is not null)
            return Failure(dispatch.ErrorCode);

        using var operationCts = new CancellationTokenSource();
        var disconnectMonitor = MonitorDisconnectAsync(client, operationCts);
        var runner = new WinAcmeProcessRunner();
        var command = new WinAcmeCommand(request.ExecutablePath, request.Arguments);
        var result = await runner.RunAsync(command, output: null, operationCts.Token);
        operationCts.Cancel();
        await disconnectMonitor;
        return new ElevatedPipeResponse(ProtocolVersion, result);
    }

    private static async Task MonitorDisconnectAsync(NamedPipeClientStream client, CancellationTokenSource cancellation)
    {
        try
        {
            while (client.IsConnected && !cancellation.IsCancellationRequested)
                await Task.Delay(250, cancellation.Token);
            if (!client.IsConnected) cancellation.Cancel();
        }
        catch (OperationCanceledException) { }
    }

    private static IReadOnlyList<string> Flatten(IReadOnlyList<SensitiveArgument> arguments)
    {
        var flattened = new List<string>(arguments.Count * 2);
        foreach (var argument in arguments)
        {
            flattened.Add(argument.Name);
            if (argument.Value.Length > 0) flattened.Add(argument.Value);
        }
        return flattened;
    }

    private static ElevatedPipeResponse Failure(string errorCode) =>
        new(ProtocolVersion, new OperationResult(OperationStatus.Failed, null, TimeSpan.Zero, [], errorCode));

    private static bool SecureEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static bool TryReadOption(IReadOnlyList<string> args, string option, out string value)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (!args[index].Equals(option, StringComparison.Ordinal)) continue;
            value = args[index + 1];
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }
}
