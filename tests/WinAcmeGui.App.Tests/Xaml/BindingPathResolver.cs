using System.Collections;
using System.Reflection;
using System.Xml.Linq;

namespace WinAcmeGui.App.Tests.Xaml;

/// <summary>
/// Resolves a XAML binding path against a CLR type using reflection, following nested properties and
/// string indexers (<c>Culture[Renewals]</c>).
/// </summary>
public static class BindingPathResolver
{
    public static bool TryResolve(Type root, string path, out string? failure)
    {
        failure = null;
        var current = root;
        foreach (var rawSegment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0) continue;

            var indexerStart = segment.IndexOf('[', StringComparison.Ordinal);
            var propertyName = indexerStart >= 0 ? segment[..indexerStart] : segment;

            if (propertyName.Length > 0)
            {
                var property = FindProperty(current, propertyName);
                if (property is null)
                {
                    failure = $"{current.Name} has no public property '{propertyName}'";
                    return false;
                }
                current = property.PropertyType;
            }

            if (indexerStart >= 0)
            {
                var indexer = FindIndexer(current);
                if (indexer is null)
                {
                    failure = $"{current.Name} has no indexer for '{segment}'";
                    return false;
                }
                current = indexer.PropertyType;
            }
        }
        return true;
    }

    /// <summary>Element type behind an <c>ItemsSource</c>, so a DataTemplate can be checked against its item.</summary>
    public static Type? GetEnumerableItemType(Type type)
    {
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var argument = type.GetGenericArguments().FirstOrDefault();
            if (argument is not null) return argument;
        }
        var enumerable = type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    public static PropertyInfo? FindProperty(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

    private static PropertyInfo? FindIndexer(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .FirstOrDefault(property => property.GetIndexParameters().Length == 1);
}

/// <summary>Maps a XAML file to the type its root DataContext is expected to have.</summary>
public static class XamlDataContextMap
{
    private static readonly Assembly AppAssembly = typeof(WinAcmeGui.App.Presentation.ShellViewModel).Assembly;

    public static Type ShellViewModel => typeof(WinAcmeGui.App.Presentation.ShellViewModel);
    public static Type WizardViewModel => typeof(WinAcmeGui.App.Presentation.CertificateWizardViewModel);

    /// <summary>Null means "no view model context", so only DataTemplate bodies are checked in that file.</summary>
    public static Type? Resolve(string file)
    {
        var name = Path.GetFileName(file);
        return name switch
        {
            "MainWindow.xaml" => ShellViewModel,
            "CertificateWizardWindow.xaml" => WizardViewModel,
            "ActiveInstallationBar.xaml" => ShellViewModel,
            "HomePage.xaml" => ShellViewModel,
            "RenewalsPage.xaml" => ShellViewModel,
            "NewCertificatePage.xaml" => ShellViewModel,
            "InstallationPage.xaml" => ShellViewModel,
            "SystemPage.xaml" => ShellViewModel,
            "ActivityPage.xaml" => ShellViewModel,
            "SettingsPage.xaml" => ShellViewModel,
            "AboutPage.xaml" => ShellViewModel,
            _ => null
        };
    }

    /// <summary>Resolves a <c>{x:Type prefix:Name}</c> reference to a type in the app assembly.</summary>
    public static Type? ResolveTypeName(string value)
    {
        var name = value.Trim();
        if (name.StartsWith("{x:Type", StringComparison.Ordinal))
            name = name[7..].TrimEnd('}').Trim();
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0) name = name[(colon + 1)..];
        return AppAssembly.GetTypes().FirstOrDefault(type => type.Name == name);
    }

    /// <summary>
    /// Path of the code-behind file for a markup file, if it declares <c>x:Class</c>. Code-behind types
    /// are excluded from the non-Windows build, so the check is file- and text-based rather than
    /// reflective — it still catches a handler named in XAML that no method implements.
    /// </summary>
    public static string? ResolveCodeBehindFile(string xamlFile)
    {
        var document = XDocument.Load(xamlFile);
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        if (document.Root?.Attribute(xNamespace + "Class") is null) return null;
        var candidate = xamlFile + ".cs";
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>Declared <c>x:Class</c> value, e.g. <c>WinAcmeGui.App.Views.HomePage</c>.</summary>
    public static string? ResolveDeclaredClass(string xamlFile)
    {
        var document = XDocument.Load(xamlFile);
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        return document.Root?.Attribute(xNamespace + "Class")?.Value;
    }
}
