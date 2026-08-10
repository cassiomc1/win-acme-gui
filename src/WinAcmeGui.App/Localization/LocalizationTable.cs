namespace WinAcmeGui.App.Localization;

/// <summary>One translatable string, with every supported language supplied together.</summary>
public sealed record LocalizedEntry(string Key, string PortugueseBrazil, string English);

/// <summary>
/// Single source of truth for user-facing text. Declaring both languages in the same row makes
/// language parity a structural property instead of something a test has to hope for.
/// </summary>
public static partial class LocalizationTable
{
    public const string PortugueseBrazilCulture = "pt-BR";
    public const string EnglishCulture = "en-US";

    public static IReadOnlyList<LocalizedEntry> Entries { get; } = BuildEntries();

    public static IReadOnlyDictionary<string, string> PortugueseBrazil { get; } =
        Entries.ToDictionary(x => x.Key, x => x.PortugueseBrazil, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> English { get; } =
        Entries.ToDictionary(x => x.Key, x => x.English, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Keys { get; } = Entries.Select(x => x.Key).ToArray();

    public static IReadOnlyDictionary<string, string> For(string cultureName) =>
        Normalize(cultureName) == PortugueseBrazilCulture ? PortugueseBrazil : English;

    /// <summary>Maps any Windows culture onto one of the two shipped languages.</summary>
    public static string Normalize(string cultureName) =>
        cultureName.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? PortugueseBrazilCulture
            : EnglishCulture;

    private static LocalizedEntry[] BuildEntries() =>
    [
        .. Shell(),
        .. Navigation(),
        .. Dashboard(),
        .. RenewalOperations(),
        .. InstallationPage(),
        .. SystemPage(),
        .. LogsPage(),
        .. SettingsAndAbout(),
        .. CertificateWizard(),
        .. Common()
    ];
}
