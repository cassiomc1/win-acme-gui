using System.Windows;
using System.Windows.Controls;
using WinAcmeGui.App.Localization;

namespace WinAcmeGui.App.Features;

/// <summary>
/// Confirmation that requires retyping an exact value. Confirm stays disabled until the typed text
/// matches, so a destructive action cannot be triggered by muscle memory alone.
/// </summary>
public partial class ConfirmationDialog : Window
{
    private readonly string _expectedAnswer;

    public ConfirmationDialog(CultureService culture, string title, string message, string expectedAnswer)
    {
        InitializeComponent();
        _expectedAnswer = expectedAnswer;
        Title = title;
        MessageText.Text = message;
        MismatchText.Text = culture["ConfirmationMismatch"];
        ConfirmButton.Content = culture["Confirm"];
        CancelButton.Content = culture["Close"];
        Loaded += (_, _) => AnswerBox.Focus();
    }

    /// <summary>Text typed by the operator; only meaningful when the dialog was confirmed.</summary>
    public string Answer => AnswerBox.Text;

    private void AnswerChanged(object sender, TextChangedEventArgs e)
    {
        var matches = AnswerBox.Text.Equals(_expectedAnswer, StringComparison.Ordinal);
        ConfirmButton.IsEnabled = matches;
        MismatchText.Visibility = AnswerBox.Text.Length > 0 && !matches ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
