using System.Windows;

namespace WinAcmeGui.App.Windows;

/// <summary>
/// Carries the page DataContext into places the visual tree does not reach. <c>DataGridColumn</c> is the
/// case that matters here: columns are not part of the visual tree, so a column header cannot use
/// <c>RelativeSource AncestorType</c>. A <see cref="Freezable"/> does receive an inheritance context, so
/// declaring one in the page resources with <c>Data="{Binding}"</c> gives the columns a working source.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
