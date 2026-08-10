using System.ComponentModel;
using System.Windows;
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
            DialogResult = true;
            Close();
        });
    }

    /// <summary>An in-flight win-acme run is cancelled when the operator closes the window.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelRunningOperation();
        base.OnClosing(e);
    }
}
