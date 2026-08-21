namespace WinAcmeGui.Infrastructure.Downloads;

/// <summary>
/// Validates the package structure after the archive digest has been checked.
/// Authenticode validation is intentionally not part of the win-acme download flow.
/// </summary>
public sealed class PackageIntegrityVerifier : IPackageSignatureVerifier
{
    public Task VerifyAsync(string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.EnumerateFiles(destination, "wacs.exe", SearchOption.AllDirectories).Any())
            throw new InvalidDataException("Downloaded package does not contain wacs.exe.");
        return Task.CompletedTask;
    }
}
