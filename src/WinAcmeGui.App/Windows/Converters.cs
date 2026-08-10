using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WinAcmeGui.App.Windows;

/// <summary>
/// Resolves a resource key (e.g. <c>StatusHealthyBrush</c>) into the brush currently registered under
/// that key. View models expose keys instead of colours so status colouring follows the active theme
/// without the presentation layer referencing WPF types.
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrWhiteSpace(key))
        {
            if (System.Windows.Application.Current?.TryFindResource(key) is Brush brush) return brush;
        }
        return System.Windows.Application.Current?.TryFindResource("StatusNeutralBrush") as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Same lookup as <see cref="ResourceKeyToBrushConverter"/>, but for the soft/background variant.</summary>
public sealed class ResourceKeyToSoftBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        var softKey = string.IsNullOrWhiteSpace(key)
            ? "StatusNeutralSoftBrush"
            : key!.Replace("Brush", "SoftBrush", StringComparison.Ordinal);
        if (System.Windows.Application.Current?.TryFindResource(softKey) is Brush brush) return brush;
        return System.Windows.Application.Current?.TryFindResource("StatusNeutralSoftBrush") as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collapses the element when the bound boolean is true (the inverse of the built-in converter).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

/// <summary>Negates a boolean, e.g. to check the "light" toggle while <c>IsDarkTheme</c> is false.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Shows the element only when the bound string has content.</summary>
public sealed class StringPresenceToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
