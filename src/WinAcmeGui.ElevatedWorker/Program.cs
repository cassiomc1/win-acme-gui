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
    private const string ProtocolVersion = "2";
    private const int MaxTokenLength = 512;
    private const int MaxRequestLength = 1024 * 1024;

    private static async Task<int> Main(string[] args)
    {
        if (!TryReadOption(args, "--pipe", out var pipeName))
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

            // The shared token arrives as the first line over the pipe - never via the command
            // line, which is world-readable to same-user processes and audit logs.
            using var handshakeCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var tokenLine = await ReadBoundedLineAsync(reader, MaxTokenLength, handshakeCts.Token);
            byte[] tokenBytes;
            try
            {
                tokenBytes = Convert.FromBase64String(tokenLine ?? string.Empty);
            }
            catch (FormatException)
            {
                return 6;
            }
            if (tokenBytes.Length != 32) return 6;

            var requestLine = await ReadBoundedLineAsync(reader, MaxRequestLength, handshakeCts.Token);
            var response = await ProcessRequestAsync(requestLine, client);
            var responseJson = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(responseJson);
            using var hmac = new HMACSHA256(tokenBytes);
            await writer.WriteLineAsync(Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(responseJson))));
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
        catch (Exception)
        {
            // A high-integrity process must never die with an unhandled exception dialog; any
            // unexpected failure exits with a distinct code the GUI can observe.
            return 5;
        }
    }

    private static async Task<ElevatedPipeResponse> ProcessRequestAsync(
        string? requestLine,
        NamedPipeClientStream client)
    {
        if (string.IsNullOrWhiteSpace(requestLine))
            return Failure("elevation.protocol.empty_request", null);

        ElevatedPipeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ElevatedPipeRequest>(requestLine);
        }
        catch (JsonException)
        {
            return Failure("elevation.protocol.invalid_request", null);
        }
        catch (NotSupportedException)
        {
            return Failure("elevation.protocol.invalid_request", null);
        }

        if (request is null
            || request.ProtocolVersion != ProtocolVersion
            || string.IsNullOrWhiteSpace(request.OperationId)
            || string.IsNullOrWhiteSpace(request.Operation)
            || string.IsNullOrWhiteSpace(request.ExecutablePath)
            || request.Arguments is null
            || request.Arguments.Any(argument => argument is null
                || string.IsNullOrWhiteSpace(argument.Name)
                || argument.Value is null))
            return Failure("elevation.protocol.invalid_request", request?.OperationId);
        if (!Enum.TryParse<WinAcmeOperation>(request.Operation, true, out var operation))
            return Failure("elevation.operation.not_allowed", request.OperationId);
        var trustVerifier = new WindowsAuthenticodeSignatureVerifier();
        if (!await trustVerifier.IsTrustedAsync(request.ExecutablePath, CancellationToken.None))
            return Failure("elevation.executable.untrusted", request.OperationId);

        var arguments = Flatten(request.Arguments);
        var dispatchRequest = new ElevatedRequest(
            "validated-win-acme",
            request.ExecutablePath,
            operation,
            arguments);
        var dispatcher = new AllowlistedOperationDispatcher();
        var dispatch = await dispatcher.DispatchAsync(dispatchRequest, CancellationToken.None);
        if (dispatch.ErrorCode is not null)
            return Failure(dispatch.ErrorCode, request.OperationId);

        using var operationCts = new CancellationTokenSource();
        var disconnectMonitor = MonitorDisconnectAsync(client, operationCts);
        var runner = new WinAcmeProcessRunner();
        var command = new WinAcmeCommand(request.ExecutablePath, request.Arguments);
        var result = await runner.RunAsync(command, output: null, operationCts.Token);
        operationCts.Cancel();
        await disconnectMonitor;
        return new ElevatedPipeResponse(ProtocolVersion, request.OperationId, result);
    }

    /// <summary>
    /// Reads a single newline-terminated line with a hard character cap so a hostile or wedged
    /// peer cannot grow this process's memory without bound.
    /// </summary>
    private static async Task<string?> ReadBoundedLineAsync(StreamReader reader, int maxLength, CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        var builder = new StringBuilder(Math.Min(maxLength, 4096));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) return builder.Length > 0 ? builder.ToString() : null;
            for (var index = 0; index < read; index++)
            {
                var character = buffer[index];
                if (character == '\n')
                {
                    var line = builder.ToString();
                    return line.EndsWith('\r') ? line[..^1] : line;
                }
                builder.Append(character);
                if (builder.Length > maxLength) throw new IOException("Pipe line exceeded the allowed length.");
            }
        }
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

    private static ElevatedPipeResponse Failure(string errorCode, string? operationId) =>
        new(ProtocolVersion, operationId, new OperationResult(OperationStatus.Failed, null, TimeSpan.Zero, [], errorCode));

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
