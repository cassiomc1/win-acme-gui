using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Operations;

public interface IElevatedOperationClient : IWinAcmeRunner
{
}

/// <summary>
/// Wire request for the elevated worker. The shared token is delivered as a separate handshake
/// line (never serialized here, never on the command line) and responses are HMAC-authenticated.
/// </summary>
public sealed record ElevatedPipeRequest(
    string ProtocolVersion,
    string OperationId,
    string Operation,
    string ExecutablePath,
    IReadOnlyList<SensitiveArgument> Arguments)
{
    public override string ToString() =>
        $"{nameof(ElevatedPipeRequest)} {{ {nameof(ProtocolVersion)}: {ProtocolVersion}, {nameof(OperationId)}: {OperationId}, {nameof(Operation)}: {Operation}, {nameof(ExecutablePath)}: {ExecutablePath}, Arguments: [{string.Join(", ", Arguments.Select(a => a.ToString()))}] }}";
}

public sealed record ElevatedPipeResponse(
    string ProtocolVersion,
    string? OperationId,
    OperationResult Result);
