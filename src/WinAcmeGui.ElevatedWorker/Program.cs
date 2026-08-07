using WinAcmeGui.ElevatedWorker.Operations;

namespace WinAcmeGui.ElevatedWorker;

internal static class Program
{
    private static async Task Main()
    {
        // The production worker is launched per operation by the WPF host.
        // Keeping the process idle when started manually avoids executing arbitrary input.
        await Task.CompletedTask;
        _ = new AllowlistedOperationDispatcher();
    }
}
