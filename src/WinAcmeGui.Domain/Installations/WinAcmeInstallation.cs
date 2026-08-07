namespace WinAcmeGui.Domain.Installations;

public sealed record WinAcmeVersion(int Major, int Minor, int Build, int Revision)
{
    public override string ToString() => $"{Major}.{Minor}.{Build}.{Revision}";
}

public sealed record AcmeEndpoint(Uri BaseUri, bool IsProduction)
{
    public static AcmeEndpoint Production { get; } =
        new(new Uri("https://acme-v02.api.letsencrypt.org/"), true);

    public static AcmeEndpoint Staging { get; } =
        new(new Uri("https://acme-staging-v02.api.letsencrypt.org/"), false);
}

public sealed record WinAcmeInstallation(
    string ExecutablePath,
    WinAcmeVersion Version,
    string ConfigurationPath,
    AcmeEndpoint Endpoint)
{
    public static WinAcmeInstallation Create(
        string executablePath,
        WinAcmeVersion version,
        string configurationPath,
        AcmeEndpoint endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        if (!Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("Executable path must be absolute.", nameof(executablePath));

        if (!Path.IsPathFullyQualified(configurationPath))
            throw new ArgumentException("Configuration path must be absolute.", nameof(configurationPath));

        return new(
            Path.GetFullPath(executablePath),
            version,
            Path.GetFullPath(configurationPath),
            endpoint);
    }
}
