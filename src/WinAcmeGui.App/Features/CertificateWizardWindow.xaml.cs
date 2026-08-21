using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WinAcmeGui.App.Presentation;

namespace WinAcmeGui.App.Features;

/// <summary>Thin host for <see cref="CertificateWizardViewModel"/>; closes itself once win-acme succeeds.</summary>
public partial class CertificateWizardWindow : Window
{
    private readonly CertificateWizardViewModel _viewModel;

    public CertificateWizardWindow(CertificateWizardViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CompletedSuccessfully += (_, _) => Dispatcher.Invoke(() =>
        {
            // The operator may have closed the window in the same dispatcher turn; setting
            // DialogResult on a closed window throws InvalidOperationException.
            if (IsLoaded && DialogResult is null)
            {
                DialogResult = true;
                Close();
            }
        });
    }

    /// <summary>An in-flight win-acme run is cancelled when the operator closes the window.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelRunningOperation();
        base.OnClosing(e);
    }

    /// <summary>
    /// Escape closes an idle wizard. While running the footer Cancel command owns cancellation
    /// (it is the only button enabled), so Escape does nothing to avoid double-cancelling.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _viewModel.IsRunning) return;
        Close();
        e.Handled = true;
    }
}
