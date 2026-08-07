using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Operations;

public interface IWinAcmeRunner
{
    Task<OperationResult> RunAsync(
        WinAcmeCommand command,
        IProgress<string>? output,
        CancellationToken cancellationToken);
}
