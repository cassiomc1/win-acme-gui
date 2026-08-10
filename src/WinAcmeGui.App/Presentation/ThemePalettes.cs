namespace WinAcmeGui.App.Presentation;

/// <summary>
/// Palette dictionary locations. Declared outside the WPF-only code so a test can assert that both
/// files exist and that the light/dark switch points at real markup.
/// </summary>
public static class ThemePalettes
{
    public const string LightPath = "Theme/Palette.Light.xaml";
    public const string DarkPath = "Theme/Palette.Dark.xaml";

    public static string PathFor(bool isDark) => isDark ? DarkPath : LightPath;

    public static Uri SourceFor(bool isDark) => new(PathFor(isDark), UriKind.Relative);
}
