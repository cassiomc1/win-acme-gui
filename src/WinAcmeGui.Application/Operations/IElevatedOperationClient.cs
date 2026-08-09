using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Operations;

public interface IElevatedOperationClient : IWinAcmeRunner
{
}

public sealed record ElevatedPipeRequest(
    string ProtocolVersion,
    string Token,
    string OperationId,
    string Operation,
    string ExecutablePath,
    IReadOnlyList<SensitiveArgument> Arguments);

public sealed record ElevatedPipeResponse(
    string ProtocolVersion,
    OperationResult Result);
