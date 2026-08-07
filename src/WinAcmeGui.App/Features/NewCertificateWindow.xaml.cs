using System.Windows;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;

namespace WinAcmeGui.App.Features;

public partial class NewCertificateWindow : Window
{
    private readonly string _executablePath;
    private readonly CertificateDraftValidator _validator = new();
    private readonly WinAcmeCommandFactory _factory = new();

    public NewCertificateWindow(string executablePath)
    {
        _executablePath = executablePath;
        InitializeComponent();
    }

    private CertificateDraft Draft => new(
        "manual",
        Domains.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ((ComboBoxItem)Validation.SelectedItem).Content.ToString()!,
        ((ComboBoxItem)KeyType.SelectedItem).Content.ToString()!,
        ((ComboBoxItem)Store.SelectedItem).Content.ToString()!);

    private void PreviewClick(object sender, RoutedEventArgs e)
    {
        var errors = _validator.Validate(Draft);
        if (errors.Count > 0) { Preview.Text = string.Join(Environment.NewLine, errors.Select(x => x.Message)); return; }
        Preview.Text = _factory.CreateCertificate(_executablePath, Draft, Staging.IsChecked == true).DisplayText;
    }

    private void ExecuteClick(object sender, RoutedEventArgs e)
    {
        var errors = _validator.Validate(Draft);
        if (errors.Count > 0) { Preview.Text = string.Join(Environment.NewLine, errors.Select(x => x.Message)); return; }
        PreviewClick(sender, e);
        if (MessageBox.Show(this, "Executar esta operação no win-acme?", "Confirmar emissão", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            DialogResult = true;
    }
}
