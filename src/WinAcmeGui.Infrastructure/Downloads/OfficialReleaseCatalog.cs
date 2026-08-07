using System.Text.Json;

namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class OfficialReleaseCatalog(HttpClient httpClient, PackageVerifier verifier)
{
    private static readonly Uri LatestUri = new("https://api.github.com/repos/win-acme/win-acme/releases/latest");

    public async Task<ReleaseAsset> GetLatestAsync(CancellationToken cancellationToken)
    {
        if (!verifier.IsApproved(LatestUri)) throw new InvalidOperationException("Release API is not approved.");
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("win-acme-gui/1.0");
        using var response = await httpClient.GetAsync(LatestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var version = root.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("Release has no tag.");
        var assets = root.GetProperty("assets").EnumerateArray()
            .Select(asset =>
            {
                var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
                return (Name: asset.GetProperty("name").GetString()!, Uri: new Uri(asset.GetProperty("browser_download_url").GetString()!), Sha256: digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest[7..] : digest);
            })
            .Where(x => x.Name.Contains("x64", StringComparison.OrdinalIgnoreCase) && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Name.Contains("gnutls", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("plugin", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("mscordbi", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Name.Contains("trimmed", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var selected = assets.FirstOrDefault();
        if (selected == default) throw new InvalidDataException("No x64 win-acme asset was published.");
        if (!verifier.IsApproved(selected.Uri)) throw new InvalidDataException("Release asset host is not approved.");
        var distribution = selected.Name.Contains("pluggable", StringComparison.OrdinalIgnoreCase) ? "pluggable" : "trimmed";
        return new(version, "x64", distribution, selected.Uri, selected.Sha256);
    }
}
