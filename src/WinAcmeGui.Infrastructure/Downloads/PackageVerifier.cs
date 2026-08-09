namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class PackageVerifier(IEnumerable<string> approvedHosts)
{
    private readonly HashSet<string> _approvedHosts = new(approvedHosts, StringComparer.OrdinalIgnoreCase);

    public bool IsApproved(Uri uri) => uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && _approvedHosts.Contains(uri.Host);

    public static bool IsSha256Digest(string? digest) =>
        !string.IsNullOrWhiteSpace(digest)
        && digest.Length == 64
        && digest.All(Uri.IsHexDigit);
}
