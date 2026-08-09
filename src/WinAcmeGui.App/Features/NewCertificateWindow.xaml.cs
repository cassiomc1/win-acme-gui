using System.Windows;
using System.ComponentModel;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Features;

public partial class NewCertificateWindow : Window
{
    private readonly string _executablePath;
    private readonly Func<CertificateDraft, bool, CancellationToken, Task<OperationResult>> _execute;
    private readonly CultureService _culture;
    private readonly CertificateDraftValidator _validator = new();
    private readonly WinAcmeCommandFactory _factory = new();
    private CancellationTokenSource? _operationCancellation;

    public NewCertificateWindow(
        string executablePath,
        Func<CertificateDraft, bool, CancellationToken, Task<OperationResult>> execute,
        CultureService? culture = null)
    {
        _executablePath = executablePath;
        _execute = execute;
        _culture = culture ?? new CultureService();
        InitializeComponent();
        ApplyCulture();
    }

    private CertificateDraft Draft => new(
        "manual",
        Domains.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ((ComboBoxItem)Validation.SelectedItem).Content.ToString()!,
        ((ComboBoxItem)KeyType.SelectedItem).Content.ToString()!,
        ((ComboBoxItem)Store.SelectedItem).Content.ToString()!,
        Email.Text.Trim(),
        AcceptTerms.IsChecked == true,
        StoragePath.Text.Trim());

    private void PreviewClick(object sender, RoutedEventArgs e)
    {
        var errors = _validator.Validate(Draft);
        if (errors.Count > 0) { Preview.Text = string.Join(Environment.NewLine, errors.Select(x => x.Message)); return; }
        Preview.Text = _factory.CreateCertificate(_executablePath, Draft, Staging.IsChecked == true).DisplayText;
    }

    private async void ExecuteClick(object sender, RoutedEventArgs e)
    {
        var errors = _validator.Validate(Draft);
        if (errors.Count > 0) { Preview.Text = string.Join(Environment.NewLine, errors.Select(x => x.Message)); return; }
        PreviewClick(sender, e);
        if (MessageBox.Show(this, _culture["ConfirmCreate"], _culture["ConfirmCreateTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            PreviewButton.IsEnabled = false;
            ExecuteButton.IsEnabled = false;
            _operationCancellation = new CancellationTokenSource();
            CancelOperationButton.IsEnabled = true;
            try
            {
                var result = await _execute(Draft, Staging.IsChecked == true, _operationCancellation.Token);
                if (result.Status == OperationStatus.Succeeded)
                    DialogResult = true;
                else
                    Preview.Text = string.Join(Environment.NewLine, result.Output.Append(result.ErrorCode ?? "certificate.operation.failed"));
            }
            catch (Exception ex)
            {
                Preview.Text = ex.Message;
            }
            finally
            {
                _operationCancellation.Dispose();
                _operationCancellation = null;
                CancelOperationButton.IsEnabled = false;
                PreviewButton.IsEnabled = true;
                ExecuteButton.IsEnabled = true;
            }
        }
    }

    private void StoreChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var store = ((ComboBoxItem?)Store.SelectedItem)?.Content?.ToString();
        var needsPath = store is "pemfiles" or "pfxfile";
        StoragePathLabel.Visibility = needsPath ? Visibility.Visible : Visibility.Collapsed;
        StoragePath.Visibility = needsPath ? Visibility.Visible : Visibility.Collapsed;
        if (!needsPath) StoragePath.Clear();
    }

    private void CancelOperationClick(object sender, RoutedEventArgs e) => _operationCancellation?.Cancel();

    protected override void OnClosing(CancelEventArgs e)
    {
        _operationCancellation?.Cancel();
        base.OnClosing(e);
    }

    private void ApplyCulture()
    {
        Title = _culture["NewCertificate"];
        CertificateTitle.Text = _culture["CertificateTitle"];
        CertificateInstructions.Text = _culture["CertificateInstructions"];
        DomainsLabel.Text = _culture["DomainsLabel"];
        ValidationLabel.Text = _culture["Validation"];
        PrivateKeyLabel.Text = _culture["PrivateKey"];
        StorageLabel.Text = _culture["Storage"];
        StoragePathLabel.Text = _culture["StoragePath"];
        EmailLabel.Text = _culture["EmailAddress"];
        AcceptTerms.Content = _culture["AcceptTerms"];
        Staging.Content = _culture["UseStaging"];
        Preview.Text = _culture["PreviewPlaceholder"];
        PreviewButton.Content = _culture["PreviewAction"];
        CancelOperationButton.Content = _culture["CancelOperation"];
        ExecuteButton.Content = _culture["Execute"];
    }
}
