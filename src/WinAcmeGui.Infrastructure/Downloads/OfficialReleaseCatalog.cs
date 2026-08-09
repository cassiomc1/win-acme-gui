using System.Text.Json;

namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class OfficialReleaseCatalog
{
    private static readonly Uri LatestUri = new("https://api.github.com/repos/win-acme/win-acme/releases/latest");
    private static readonly IReadOnlyDictionary<string, string> PinnedDigests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["v2.2.9.1701/win-acme.v2.2.9.1701.x64.trimmed.zip"] = "f4dc3b144841ffdba391ce168c273d7a686d45a359075e30ee4bf4ee186857d6"
    };
    private readonly OfficialHttpTransport _transport;

    public OfficialReleaseCatalog(HttpClient httpClient, PackageVerifier verifier) => _transport = new(httpClient, verifier);

    public OfficialReleaseCatalog(PackageVerifier verifier) => _transport = new(verifier);

    public async Task<ReleaseAsset> GetLatestAsync(CancellationToken cancellationToken)
    {
        _transport.Client.DefaultRequestHeaders.UserAgent.ParseAdd("win-acme-gui/1.0");
        using var response = await _transport.GetAsync(LatestUri, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var version = root.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("Release has no tag.");
        var assets = root.GetProperty("assets").EnumerateArray()
            .Select(asset =>
            {
                var name = asset.GetProperty("name").GetString()!;
                var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
                if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true) digest = digest[7..];
                if (!PackageVerifier.IsSha256Digest(digest))
                    PinnedDigests.TryGetValue($"{version}/{name}", out digest);
                return (Name: name, Uri: new Uri(asset.GetProperty("browser_download_url").GetString()!), Sha256: digest);
            })
            .Where(x => x.Name.Contains("x64", StringComparison.OrdinalIgnoreCase) && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Name.Contains("gnutls", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("plugin", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("mscordbi", StringComparison.OrdinalIgnoreCase))
            .Where(x => PackageVerifier.IsSha256Digest(x.Sha256))
            .OrderByDescending(x => x.Name.Contains("trimmed", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var selected = assets.FirstOrDefault();
        if (selected == default) throw new InvalidDataException("No x64 win-acme asset was published.");
        if (!_transport.IsApproved(selected.Uri)) throw new InvalidDataException("Release asset host is not approved.");
        var distribution = selected.Name.Contains("pluggable", StringComparison.OrdinalIgnoreCase) ? "pluggable" : "trimmed";
        return new(version, "x64", distribution, selected.Uri, selected.Sha256!);
    }
}
