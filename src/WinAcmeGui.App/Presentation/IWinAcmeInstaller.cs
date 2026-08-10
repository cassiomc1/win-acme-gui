using System.IO;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.App.Presentation;

public sealed record InstalledPackage(string Version, string Destination);

/// <summary>
/// Fetches the official win-acme release. Behind an interface because the concrete implementation
/// performs network I/O and Authenticode checks that the shell tests must not trigger.
/// </summary>
public interface IWinAcmeInstaller
{
    Task<InstalledPackage> InstallLatestAsync(IProgress<double>? progress, CancellationToken cancellationToken);
}

/// <summary>
/// Production installer: approved GitHub hosts only, SHA-256 digest match, signature verification and
/// a safe ZIP extraction into a fresh directory.
/// </summary>
public sealed class OfficialWinAcmeInstaller : IWinAcmeInstaller
{
    private static readonly string[] ApprovedHosts =
    [
        "api.github.com",
        "github.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com"
    ];

    public async Task<InstalledPackage> InstallLatestAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var verifier = new PackageVerifier(ApprovedHosts);
        var asset = await new OfficialReleaseCatalog(verifier).GetLatestAsync(cancellationToken);
        var destination = ResolveDestination(asset.Version);
        var downloader = new WinAcmeDownloader(
            new OfficialReleaseClient(verifier),
            new SafeZipExtractor(),
            new WindowsAuthenticodeSignatureVerifier());
        await downloader.DownloadAndExtractAsync(asset, destination, progress, cancellationToken);
        return new(asset.Version, destination);
    }

    /// <summary>Prefers the portable directory and falls back to LocalAppData when it is not writable.</summary>
    private static string ResolveDestination(string version)
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "win-acme-downloads", version);
        try
        {
            Directory.CreateDirectory(portable);
            var probe = Path.Combine(portable, ".write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return portable;
        }
        catch (UnauthorizedAccessException)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinAcmeGui",
                "win-acme-downloads",
                version);
        }
        catch (IOException)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinAcmeGui",
                "win-acme-downloads",
                version);
        }
    }
}
