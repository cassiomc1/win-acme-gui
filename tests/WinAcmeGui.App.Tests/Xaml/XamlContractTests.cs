using System.Xml.Linq;
using FluentAssertions;
using WinAcmeGui.App.Localization;

namespace WinAcmeGui.App.Tests.Xaml;

/// <summary>
/// Contract tests over the markup. They stand in for the XAML compiler on non-Windows CI: a typo in a
/// resource key, a localization key or a binding path fails here instead of at runtime on Windows.
/// </summary>
public sealed class XamlContractTests
{
    [Fact]
    public void Markup_files_are_discovered_and_well_formed()
    {
        XamlScanner.XamlFiles.Should().HaveCountGreaterThan(10);
        foreach (var file in XamlScanner.XamlFiles)
        {
            var act = () => XDocument.Load(file);
            act.Should().NotThrow($"{XamlScanner.Relative(file)} must be well-formed XML");
        }
    }

    [Fact]
    public void Every_localization_key_used_in_markup_exists_in_the_table()
    {
        var missing = new List<string>();
        foreach (var file in XamlScanner.XamlFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var key in XamlScanner.ExtractLocalizationKeys(text).Distinct(StringComparer.Ordinal))
            {
                if (!LocalizationTable.PortugueseBrazil.ContainsKey(key))
                    missing.Add($"{XamlScanner.Relative(file)} → Culture[{key}]");
            }
        }
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Every_localization_key_used_in_code_exists_in_the_table()
    {
        var missing = new List<string>();
        var sources = Directory
            .EnumerateFiles(XamlScanner.AppProjectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("LocalizationTable.cs", StringComparison.Ordinal))
            .Where(path => !path.Contains("LocalizationTable.", StringComparison.Ordinal));

        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(text, @"(?:Culture|_culture|culture)\[""(?<key>[A-Za-z0-9_]+)""\]"))
            {
                var key = match.Groups["key"].Value;
                if (!LocalizationTable.PortugueseBrazil.ContainsKey(key))
                    missing.Add($"{XamlScanner.Relative(file)} → \"{key}\"");
            }
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(text, @"Format\(""(?<key>[A-Za-z0-9_]+)"""))
            {
                var key = match.Groups["key"].Value;
                if (!LocalizationTable.PortugueseBrazil.ContainsKey(key))
                    missing.Add($"{XamlScanner.Relative(file)} → Format(\"{key}\")");
            }
        }
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Both_languages_define_the_same_keys_without_gaps()
    {
        LocalizationTable.Entries.Select(x => x.Key).Should().OnlyHaveUniqueItems();
        LocalizationTable.English.Keys.Should().BeEquivalentTo(LocalizationTable.PortugueseBrazil.Keys);
        foreach (var entry in LocalizationTable.Entries)
        {
            entry.PortugueseBrazil.Should().NotBeNullOrWhiteSpace(entry.Key);
            entry.English.Should().NotBeNullOrWhiteSpace(entry.Key);
        }
    }

    [Fact]
    public void Every_resource_key_referenced_in_markup_is_defined_somewhere()
    {
        var defined = CollectDefinedResourceKeys();
        var missing = new List<string>();
        foreach (var file in XamlScanner.XamlFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var key in XamlScanner.ExtractResourceKeys(text).Distinct(StringComparer.Ordinal))
            {
                if (!XamlScanner.IsSimpleIdentifier(key)) continue;
                if (!defined.Contains(key)) missing.Add($"{XamlScanner.Relative(file)} → {key}");
            }
        }
        missing.Should().BeEmpty();
    }

    [Fact]
    public void The_two_palettes_expose_exactly_the_same_keys()
    {
        var light = CollectKeys(Path.Combine(XamlScanner.AppProjectDirectory, "Theme", "Palette.Light.xaml"));
        var dark = CollectKeys(Path.Combine(XamlScanner.AppProjectDirectory, "Theme", "Palette.Dark.xaml"));
        light.Should().NotBeEmpty();
        dark.Should().BeEquivalentTo(light, "swapping the palette must not leave a DynamicResource unresolved");
    }

    private static HashSet<string> CollectDefinedResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in XamlScanner.XamlFiles)
            foreach (var key in CollectKeys(file))
                keys.Add(key);
        // Framework-provided brushes and metrics referenced by the templates.
        keys.Add("WindowBackground");
        return keys;
    }

    private static HashSet<string> CollectKeys(string file)
    {
        var document = XDocument.Load(file);
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        return document
            .Descendants()
            .Select(element => element.Attribute(xNamespace + "Key")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
