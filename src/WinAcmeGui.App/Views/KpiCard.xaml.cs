using System.Windows;
using System.Windows.Controls;

namespace WinAcmeGui.App.Views;

/// <summary>
/// One dashboard metric: uppercase caption, large value, supporting hint and a status accent resolved
/// from a resource key so it follows the active theme.
/// </summary>
public partial class KpiCard : UserControl
{
    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    /// <summary>Resource key of the accent brush, e.g. <c>StatusWarningBrush</c>.</summary>
    public static readonly DependencyProperty AccentKeyProperty =
        DependencyProperty.Register(nameof(AccentKey), typeof(string), typeof(KpiCard), new PropertyMetadata("StatusNeutralBrush"));

    public KpiCard() => InitializeComponent();

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string AccentKey
    {
        get => (string)GetValue(AccentKeyProperty);
        set => SetValue(AccentKeyProperty, value);
    }
}
