namespace WinAcmeGui.Infrastructure.Operations;

public sealed class OutputRedactor(IEnumerable<string> secrets)
{
    private readonly string[] _secrets = secrets.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).ToArray();

    public string Redact(string value)
    {
        foreach (var secret in _secrets)
            value = value.Replace(secret, "••••••••", StringComparison.Ordinal);
        return value;
    }
}
