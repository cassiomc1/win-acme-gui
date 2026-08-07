namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class PackageVerifier(IEnumerable<string> approvedHosts)
{
    private readonly HashSet<string> _approvedHosts = new(approvedHosts, StringComparer.OrdinalIgnoreCase);

    public bool IsApproved(Uri uri) => uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && _approvedHosts.Contains(uri.Host);
}
