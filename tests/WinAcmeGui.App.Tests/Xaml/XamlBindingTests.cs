using System.Xml.Linq;
using FluentAssertions;

namespace WinAcmeGui.App.Tests.Xaml;

/// <summary>
/// Walks each markup file and checks every <c>{Binding}</c> path against the type that will actually be
/// the DataContext there, including inside DataTemplates, where the context is the item type behind the
/// owning <c>ItemsSource</c>.
/// </summary>
public sealed class XamlBindingTests
{
    private static readonly XNamespace Xaml = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

    [Fact]
    public void Every_binding_path_resolves_against_its_data_context()
    {
        var failures = new List<string>();
        foreach (var file in XamlScanner.XamlFiles)
        {
            var rootContext = XamlDataContextMap.Resolve(file);
            var document = XDocument.Load(file);
            if (document.Root is null) continue;
            Walk(document.Root, rootContext, rootContext, null, file, failures);
        }
        failures.Should().BeEmpty();
    }

    [Fact]
    public void Every_click_handler_has_a_method_in_the_code_behind()
    {
        var failures = new List<string>();
        foreach (var file in XamlScanner.XamlFiles)
        {
            var codeBehindFile = XamlDataContextMap.ResolveCodeBehindFile(file);
            var document = XDocument.Load(file);
            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (!attribute.Name.LocalName.EndsWith("Click", StringComparison.Ordinal)
                        && !attribute.Name.LocalName.EndsWith("Changed", StringComparison.Ordinal))
                        continue;
                    var value = attribute.Value;
                    if (value.StartsWith('{')) continue;
                    if (!XamlScanner.IsSimpleIdentifier(value)) continue;
                    if (codeBehindFile is null)
                    {
                        failures.Add($"{XamlScanner.Relative(file)} → handler '{value}' but no code-behind file");
                        continue;
                    }
                    // Code-behind is excluded from the non-Windows build, so match on the declaration text.
                    var source = File.ReadAllText(codeBehindFile);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(source, $@"\b(?:void|Task)\s+{value}\s*\("))
                        failures.Add($"{XamlScanner.Relative(file)} → {Path.GetFileName(codeBehindFile)} has no handler '{value}'");
                }
            }
        }
        failures.Should().BeEmpty();
    }

    [Fact]
    public void Every_view_declared_in_the_shell_has_markup_and_code_behind()
    {
        var shell = XamlScanner.XamlFiles.Single(x => Path.GetFileName(x) == "MainWindow.xaml");
        var text = File.ReadAllText(shell);
        var referenced = System.Text.RegularExpressions.Regex
            .Matches(text, @"<views:(?<name>[A-Za-z0-9_]+)")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        referenced.Should().NotBeEmpty();
        foreach (var name in referenced)
        {
            var markup = Path.Combine(XamlScanner.AppProjectDirectory, "Views", name + ".xaml");
            File.Exists(markup).Should().BeTrue($"<views:{name}> needs Views/{name}.xaml");
            File.Exists(markup + ".cs").Should().BeTrue($"<views:{name}> needs Views/{name}.xaml.cs");
            XamlDataContextMap.ResolveDeclaredClass(markup)
                .Should().Be($"WinAcmeGui.App.Views.{name}");
        }
    }

    [Fact]
    public void Every_markup_file_with_a_class_declares_a_matching_namespace_and_file_name()
    {
        foreach (var file in XamlScanner.XamlFiles)
        {
            var declared = XamlDataContextMap.ResolveDeclaredClass(file);
            if (declared is null) continue;
            declared.Split('.').Last().Should().Be(Path.GetFileNameWithoutExtension(file));
            File.Exists(file + ".cs").Should().BeTrue($"{XamlScanner.Relative(file)} needs a code-behind file");
        }
    }

    private static void Walk(
        XElement element,
        Type? context,
        Type? rootContext,
        Type? itemContext,
        string file,
        List<string> failures)
    {
        // An ItemsSource establishes the item type used by this element's templates and columns.
        var itemsSource = element.Attribute("ItemsSource")?.Value;
        if (itemsSource is not null && rootContext is not null)
            itemContext = ResolveItemContext(itemsSource, rootContext) ?? itemContext;

        var childContext = context;
        if (element.Name.LocalName == "DataTemplate")
            childContext = ResolveTemplateDataType(element) ?? itemContext ?? childContext;

        // DataGrid column bindings bind to the row item, not to the page's view model.
        var bindsToItem = element.Name.LocalName.StartsWith("DataGrid", StringComparison.Ordinal)
            && element.Name.LocalName.EndsWith("Column", StringComparison.Ordinal);

        foreach (var attribute in element.Attributes())
        {
            var attributeContext = bindsToItem && attribute.Name.LocalName == "Binding"
                ? itemContext
                : childContext;
            foreach (var body in XamlScanner.ExtractMarkupExtensions(attribute.Value, "Binding"))
                Check(body, attributeContext, rootContext, file, element.Name.LocalName, attribute.Name.LocalName, failures);
        }

        foreach (var child in element.Elements())
            Walk(child, childContext, rootContext, itemContext, file, failures);
    }

    private static void Check(
        string body,
        Type? context,
        Type? rootContext,
        string file,
        string element,
        string attribute,
        List<string> failures)
    {
        var path = XamlScanner.ExtractBindingPath(body);
        if (path is null || path.Length == 0) return;

        // {Binding DataContext.X, RelativeSource={RelativeSource AncestorType=UserControl}} reaches the page context.
        if (body.Contains("AncestorType=UserControl", StringComparison.Ordinal)
            && path.StartsWith("DataContext.", StringComparison.Ordinal))
        {
            if (rootContext is null) return;
            Resolve(rootContext, path["DataContext.".Length..], file, element, attribute, failures);
            return;
        }

        // {Binding Data.X, Source={StaticResource PageContext}} goes through the BindingProxy.
        if (body.Contains("Source={StaticResource PageContext}", StringComparison.Ordinal)
            && path.StartsWith("Data.", StringComparison.Ordinal))
        {
            if (rootContext is null) return;
            Resolve(rootContext, path["Data.".Length..], file, element, attribute, failures);
            return;
        }

        if (XamlScanner.IsForeignContext(body)) return;
        if (context is null) return;
        Resolve(context, path, file, element, attribute, failures);
    }

    private static void Resolve(Type context, string path, string file, string element, string attribute, List<string> failures)
    {
        if (!BindingPathResolver.TryResolve(context, path, out var failure))
            failures.Add($"{XamlScanner.Relative(file)} → <{element} {attribute}=\"{{Binding {path}}}\">: {failure}");
    }

    /// <summary>Explicit <c>DataType</c> on a DataTemplate, when it declares one.</summary>
    private static Type? ResolveTemplateDataType(XElement template)
    {
        var declared = template.Attribute("DataType")?.Value;
        return declared is null ? null : XamlDataContextMap.ResolveTypeName(declared);
    }

    /// <summary>Element type behind an <c>ItemsSource="{Binding Path}"</c> expression.</summary>
    private static Type? ResolveItemContext(string itemsSource, Type rootContext)
    {
        var body = XamlScanner.ExtractMarkupExtensions(itemsSource, "Binding").FirstOrDefault();
        if (body is null) return null;
        var path = XamlScanner.ExtractBindingPath(body);
        if (path is null) return null;
        var collection = ResolvePropertyType(rootContext, path);
        return collection is null ? null : BindingPathResolver.GetEnumerableItemType(collection);
    }

    private static Type? ResolvePropertyType(Type root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment.Trim();
            if (name.StartsWith("DataContext", StringComparison.Ordinal)) continue;
            var property = BindingPathResolver.FindProperty(current, name);
            if (property is null) return null;
            current = property.PropertyType;
        }
        return current;
    }
}
