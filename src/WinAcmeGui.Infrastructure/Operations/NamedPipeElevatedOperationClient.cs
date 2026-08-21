using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Operations;

public sealed class NamedPipeElevatedOperationClient(
    string workerPath,
    TimeSpan? timeout = null,
    IExecutableTrustVerifier? executableTrustVerifier = null,
    IExecutableTrustVerifier? workerTrustVerifier = null) : IElevatedOperationClient
{
    private const string ProtocolVersion = "2";
    private const int MaxResponseLength = 1024 * 1024;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(30);

    public async Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.windows.required");
        if (!Path.IsPathFullyQualified(workerPath) || !File.Exists(workerPath))
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.worker.missing");
        var trustVerifier = executableTrustVerifier ?? new WindowsAuthenticodeSignatureVerifier();
        var workerVerifier = workerTrustVerifier ?? new WindowsAuthenticodeSignatureVerifier(requireTrustedPublisher: false);
        if (!await workerVerifier.IsTrustedAsync(workerPath, cancellationToken))
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.worker.untrusted");
        if (workerVerifier is WindowsAuthenticodeSignatureVerifier authenticodeVerifier
            && !await authenticodeVerifier.HasSameSignerAsync(Environment.ProcessPath ?? string.Empty, workerPath, cancellationToken))
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.worker.publisher.mismatch");
        if (!await trustVerifier.IsTrustedAsync(command.ExecutablePath, cancellationToken))
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.executable.untrusted");

        var operation = GetOperation(command);
        if (operation is null)
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.operation.not_allowed");

        // Protocol: the token never touches the command line (world-readable via WMI and audit
        // logs). It is written over the pipe only after the connected client process has been
        // proven to be the worker we spawned, and every response line is HMAC-authenticated with
        // it, so a spoofed peer cannot forge success/failure results.
        var pipeName = "win-acme-gui-" + Guid.NewGuid().ToString("N");
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        var operationId = Guid.NewGuid().ToString("N");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_timeout);
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        Process? worker = null;
        try
        {
            worker = ProcessStartInfoExtensions.StartElevatedWorker(workerPath, pipeName);
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            connectionCts.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await server.WaitForConnectionAsync(connectionCts.Token);
            }
            catch (OperationCanceledException) when (connectionCts.IsCancellationRequested && !linked.IsCancellationRequested)
            {
                await TerminateAsync(worker);
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.worker.timeout");
            }
            if (!IsConnectedToWorker(server, worker))
            {
                await TerminateAsync(worker);
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.untrusted_client");
            }
            using var reader = new StreamReader(server, leaveOpen: true);
            await using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(token);
            var request = new ElevatedPipeRequest(ProtocolVersion, operationId, operation, command.ExecutablePath, command.Arguments);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            var line = await reader.ReadLineAsync(linked.Token);
            if (string.IsNullOrWhiteSpace(line))
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.empty_response");
            if (line.Length > MaxResponseLength)
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.invalid_response");
            var macLine = await reader.ReadLineAsync(linked.Token);
            if (string.IsNullOrWhiteSpace(macLine) || !MacMatches(macLine, tokenBytes, line))
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.invalid_response");
            ElevatedPipeResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<ElevatedPipeResponse>(line);
            }
            catch (JsonException)
            {
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.invalid_response");
            }
            if (response is null
                || response.ProtocolVersion != ProtocolVersion
                || response.Result is null
                || response.Result.Output is null
                || !string.Equals(response.OperationId, operationId, StringComparison.Ordinal))
                return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.protocol.invalid_response");
            foreach (var item in response.Result.Output) output?.Report(item);
            return response.Result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            await TerminateAsync(worker);
            return new(
                cancellationToken.IsCancellationRequested ? OperationStatus.Cancelled : OperationStatus.TimedOut,
                null,
                TimeSpan.Zero,
                [],
                cancellationToken.IsCancellationRequested ? "operation.cancelled" : "operation.timeout");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            await TerminateAsync(worker);
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.uac.rejected");
        }
        catch (InvalidOperationException)
        {
            await TerminateAsync(worker);
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], "elevation.worker.start.failed");
        }
        catch (IOException ex)
        {
            await TerminateAsync(worker);
            return new(OperationStatus.Failed, null, TimeSpan.Zero, [], ex.Message);
        }
        finally { worker?.Dispose(); }
    }

    private static bool IsConnectedToWorker(NamedPipeServerStream server, Process worker)
    {
        // Only the process we spawned may receive the token and the request. PipeOptions.CurrentUserOnly
        // restricts by account; this check pins the exact process identity.
        try
        {
            return GetNamedPipeClientProcessId(server.SafePipeHandle, out var clientProcessId)
                && clientProcessId == (uint)worker.Id;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool MacMatches(string macLine, byte[] tokenBytes, string payload)
    {
        var provided = new byte[32];
        if (!Convert.TryFromBase64String(macLine, provided, out var written) || written != 32) return false;
        using var hmac = new HMACSHA256(tokenBytes);
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static string? GetOperation(WinAcmeCommand command)
    {
        var switches = command.Arguments.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (switches.Contains("--renew")) return "Renew";
        if (switches.Contains("--cancel")) return "Cancel";
        if (switches.Contains("--revoke")) return "Revoke";
        if (switches.Contains("--source")) return "Create";
        return null;
    }

    private static async Task TerminateAsync(Process? worker)
    {
        if (worker is null) return;
        try
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        try
        {
            using var waitCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.WaitForExitAsync(waitCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        Microsoft.Win32.SafeHandles.SafePipeHandle handle,
        out uint clientProcessId);
}

internal static class ProcessStartInfoExtensions
{
    public static Process StartElevatedWorker(string workerPath, string pipeName)
    {
        var info = new ProcessStartInfo(workerPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"--pipe {Quote(pipeName)}"
        };
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start the elevated worker.");
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
