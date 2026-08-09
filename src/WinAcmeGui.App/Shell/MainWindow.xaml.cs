using System.IO;
using System.Windows;
using Microsoft.Win32;
using WinAcmeGui.App.Features;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.App.Shell;

public partial class MainWindow : Window
{
    private readonly CultureService _culture = new();
    private CancellationTokenSource? _downloadCancellation;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel(_culture);
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.CanCancelOperation) or nameof(MainWindowViewModel.CancelOperationText))
                UpdateCancelOperationButton(viewModel);
        };
        RefreshGridHeaders();
        foreach (var item in viewModel.Sections)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = item.Title,
                Tag = item.Id,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 12, 10, 12),
                Margin = new Thickness(0)
            };
            button.Click += async (_, _) =>
            {
                var section = (string)button.Tag;
                viewModel.SelectSection(section);
                if (section.Equals("new", StringComparison.OrdinalIgnoreCase)) await OpenCertificateWindow();
            };
            NavigationPanel.Children.Add(button);
            item.TitleChanged += (_, _) => button.Content = item.Title;
        }
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }

    private void RefreshClick(object sender, RoutedEventArgs e) => ((MainWindowViewModel)DataContext).RefreshCommand.Execute(null);

    private void EnglishClick(object sender, RoutedEventArgs e)
    {
        _culture.SetCulture("en-US");
        ((MainWindowViewModel)DataContext).RefreshLabels();
        RefreshGridHeaders();
    }

    private void PortugueseClick(object sender, RoutedEventArgs e)
    {
        _culture.SetCulture("pt-BR");
        ((MainWindowViewModel)DataContext).RefreshLabels();
        RefreshGridHeaders();
    }

    private async void DownloadClick(object sender, RoutedEventArgs e)
    {
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        UpdateCancelOperationButton((MainWindowViewModel)DataContext);
        try
        {
            var verifier = new PackageVerifier(["api.github.com", "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com"]);
            var catalog = new OfficialReleaseCatalog(verifier);
            var asset = await catalog.GetLatestAsync(cancellation.Token);
            var destination = Path.Combine(AppContext.BaseDirectory, "win-acme-downloads", asset.Version);
            try
            {
                Directory.CreateDirectory(destination);
                var probe = Path.Combine(destination, ".write-test");
                await File.WriteAllTextAsync(probe, string.Empty);
                File.Delete(probe);
            }
            catch (UnauthorizedAccessException)
            {
                destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinAcmeGui", "win-acme-downloads", asset.Version);
            }
            var downloader = new WinAcmeDownloader(
                new OfficialReleaseClient(verifier),
                new SafeZipExtractor(),
                new WindowsAuthenticodeSignatureVerifier());
            await downloader.DownloadAndExtractAsync(asset, destination, null, cancellation.Token);
            MessageBox.Show($"win-acme {asset.Version} instalado em:\n{destination}\n\nUse Atualizar para detectá-lo.", "win-acme GUI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Download do win-acme", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _downloadCancellation = null;
            UpdateCancelOperationButton((MainWindowViewModel)DataContext);
        }
    }

    private void CancelOperationClick(object sender, RoutedEventArgs e)
    {
        ((MainWindowViewModel)DataContext).CancelActiveOperation();
        _downloadCancellation?.Cancel();
    }

    private async void SelectExecutableClick(object sender, RoutedEventArgs e) => await SelectExecutableAsync();

    private async Task SelectExecutableAsync()
    {
        var dialog = new OpenFileDialog { Filter = "win-acme executable|wacs.exe;wacs|All files|*.*", Title = _culture["SelectExecutable"] };
        if (dialog.ShowDialog(this) == true)
            await ((MainWindowViewModel)DataContext).UseExecutableAsync(dialog.FileName);
    }

    private async void NewCertificateClick(object sender, RoutedEventArgs e) =>
        await OpenCertificateWindow();

    private async void ContextActionClick(object sender, RoutedEventArgs e)
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.SelectedSection.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            await OpenCertificateWindow();
            return;
        }

        if (vm.SelectedSection.Equals("installation", StringComparison.OrdinalIgnoreCase))
            await SelectExecutableAsync();
    }

    private async Task OpenCertificateWindow()
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.ActiveCandidate is null) { MessageBox.Show("Select or discover a win-acme installation first."); return; }
        var dialog = new NewCertificateWindow(vm.ActiveCandidate.ExecutablePath, vm.CreateCertificateAsync, _culture) { Owner = this };
        if (dialog.ShowDialog() == true) await vm.LoadAsync();
    }

    private async void RenewClick(object sender, RoutedEventArgs e) => await RunRenewal(false);

    private async void ForceRenewClick(object sender, RoutedEventArgs e) => await RunRenewal(true);

    private async Task RunRenewal(bool force)
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.SelectedRenewal is null)
        {
            MessageBox.Show("Select a renewal first.", "win-acme GUI", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (force && MessageBox.Show(this, "Forçar a renovação pode emitir uma nova ordem. Continuar?", "Confirmar renovação forçada", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            var result = await vm.RenewSelectedAsync(force);
            MessageBox.Show(result?.ErrorCode is null ? "Operation finished successfully." : result.ErrorCode, "win-acme GUI", MessageBoxButton.OK, result?.ErrorCode is null ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "win-acme GUI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CancelClick(object sender, RoutedEventArgs e)
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.SelectedRenewal is null) { MessageBox.Show("Select a renewal first."); return; }
        var confirmation = Prompt("Type the friendly name to cancel:", "Confirm cancellation");
        var result = await vm.CancelSelectedAsync(confirmation);
        MessageBox.Show(result?.ErrorCode is null ? "Cancellation command finished." : result.ErrorCode, "win-acme GUI");
    }

    private async void RevokeClick(object sender, RoutedEventArgs e)
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.SelectedRenewal is null) { MessageBox.Show("Select a renewal first."); return; }
        var confirmation = Prompt("Revocation is for compromised keys. Type the friendly name:", "Confirm revocation");
        var result = await vm.RevokeSelectedAsync(confirmation);
        MessageBox.Show(result?.ErrorCode is null ? "Revocation command finished." : result.ErrorCode, "win-acme GUI");
    }

    private string Prompt(string message, string title)
    {
        var dialog = new Window { Title = title, Width = 420, Height = 170, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var input = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 12, 0, 12) };
        panel.Children.Add(input);
        var ok = new System.Windows.Controls.Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(20, 6, 20, 6) };
        ok.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(ok);
        dialog.Content = panel;
        return dialog.ShowDialog() == true ? input.Text : string.Empty;
    }

    private void RefreshGridHeaders()
    {
        if (RenewalsGrid.Columns.Count < 5) return;
        RenewalsGrid.Columns[0].Header = _culture["FriendlyName"];
        RenewalsGrid.Columns[1].Header = _culture["Domains"];
        RenewalsGrid.Columns[2].Header = _culture["StatusColumn"];
        RenewalsGrid.Columns[3].Header = _culture["Diagnostics"];
        RenewalsGrid.Columns[4].Header = _culture["Editable"];
    }

    private void UpdateCancelOperationButton(MainWindowViewModel viewModel) =>
        CancelOperationButton.IsEnabled = viewModel.CanCancelOperation || _downloadCancellation is not null;
}
