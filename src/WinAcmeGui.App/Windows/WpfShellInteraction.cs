using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using WinAcmeGui.App.Features;
using WinAcmeGui.App.Localization;
using WinAcmeGui.App.Presentation;

namespace WinAcmeGui.App.Windows;

/// <summary>WPF implementation of <see cref="IShellInteraction"/>, owned by the main window.</summary>
public sealed class WpfShellInteraction(Window owner, CultureService culture) : IShellInteraction
{
    public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, ToImage(severity));

    public bool Confirm(string title, string message, DialogSeverity severity = DialogSeverity.Warning) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, ToImage(severity)) == MessageBoxResult.Yes;

    public string? PromptForConfirmationText(string title, string message, string expectedAnswer)
    {
        var dialog = new ConfirmationDialog(culture, title, message, expectedAnswer) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }

    public string? PickExecutable(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "win-acme executable|wacs.exe;wacs|All files|*.*",
            CheckFileExists = true
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process can hold the clipboard open; losing a copy is not worth failing the action.
        }
    }

    /// <summary>Opens an absolute HTTPS URL in the default browser; anything else is ignored.</summary>
    public void OpenExternal(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)) return;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception) { }
        catch (System.IO.FileNotFoundException) { }
    }

    private static MessageBoxImage ToImage(DialogSeverity severity) => severity switch
    {
        DialogSeverity.Error => MessageBoxImage.Error,
        DialogSeverity.Warning => MessageBoxImage.Warning,
        _ => MessageBoxImage.Information
    };
}
