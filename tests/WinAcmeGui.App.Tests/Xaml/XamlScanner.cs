using System.Text;
using System.Text.RegularExpressions;

namespace WinAcmeGui.App.Tests.Xaml;

/// <summary>A markup extension reference found in a XAML attribute, with the element that carried it.</summary>
public sealed record MarkupReference(string File, string Element, string Attribute, string Value);

/// <summary>
/// Minimal XAML scanner. The WPF project only compiles on Windows, so these tests parse the markup as
/// text/XML on every platform and assert the contracts a XAML compile would otherwise catch:
/// resource keys, localization keys, event handlers and view-model binding paths.
/// </summary>
public static class XamlScanner
{
    private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>Walks up from the test binaries to the directory holding WinAcmeGui.sln.</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string AppProjectDirectory => Path.Combine(RepositoryRoot, "src", "WinAcmeGui.App");

    public static IReadOnlyList<string> XamlFiles { get; } = Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "WinAcmeGui.App"), "*.xaml", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    public static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    /// <summary>Extracts every <c>{Extension Value}</c> occurrence, tolerating nested braces.</summary>
    public static IEnumerable<string> ExtractMarkupExtensions(string text, string extensionName)
    {
        var token = "{" + extensionName;
        var index = text.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            var after = index + token.Length;
            if (after < text.Length && (char.IsWhiteSpace(text[after]) || text[after] == '}'))
            {
                var depth = 0;
                var end = -1;
                for (var cursor = index; cursor < text.Length; cursor++)
                {
                    if (text[cursor] == '{') depth++;
                    else if (text[cursor] == '}')
                    {
                        depth--;
                        if (depth == 0) { end = cursor; break; }
                    }
                }
                if (end > index) yield return text[(after)..end].Trim();
            }
            index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
        }
    }

    /// <summary>Localization keys referenced anywhere as <c>Culture[Key]</c>.</summary>
    public static IEnumerable<string> ExtractLocalizationKeys(string text)
    {
        foreach (Match match in Regex.Matches(text, @"Culture\[(?<key>[A-Za-z0-9_]+)\]"))
            yield return match.Groups["key"].Value;
    }

    /// <summary>Keys referenced through StaticResource or DynamicResource.</summary>
    public static IEnumerable<string> ExtractResourceKeys(string text)
    {
        foreach (var extension in new[] { "StaticResource", "DynamicResource" })
        {
            foreach (var body in ExtractMarkupExtensions(text, extension))
            {
                var key = body.Split(',')[0].Trim();
                if (key.Length > 0) yield return key;
            }
        }
    }

    /// <summary>The <c>Path=</c> part of a binding, or null when the binding has no simple path.</summary>
    public static string? ExtractBindingPath(string bindingBody)
    {
        if (bindingBody.Length == 0) return null;
        var parts = SplitTopLevel(bindingBody);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Path=", StringComparison.Ordinal)) return trimmed[5..].Trim();
        }
        var first = parts[0].Trim();
        return first.Contains('=', StringComparison.Ordinal) ? null : first;
    }

    /// <summary>True when the binding resolves against something other than the page's view model.</summary>
    public static bool IsForeignContext(string bindingBody) =>
        bindingBody.Contains("ElementName=", StringComparison.Ordinal)
        || bindingBody.Contains("RelativeSource", StringComparison.Ordinal)
        || bindingBody.Contains("Source=", StringComparison.Ordinal);

    /// <summary>Splits a markup-extension body on commas that are not inside nested braces or quotes.</summary>
    public static IReadOnlyList<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var builder = new StringBuilder();
        var depth = 0;
        var quoted = false;
        foreach (var character in value)
        {
            switch (character)
            {
                case '\'': quoted = !quoted; builder.Append(character); break;
                case '{' when !quoted: depth++; builder.Append(character); break;
                case '}' when !quoted: depth--; builder.Append(character); break;
                case ',' when depth == 0 && !quoted:
                    parts.Add(builder.ToString());
                    builder.Clear();
                    break;
                default: builder.Append(character); break;
            }
        }
        parts.Add(builder.ToString());
        return parts;
    }

    public static bool IsSimpleIdentifier(string value) => Identifier.IsMatch(value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinAcmeGui.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
