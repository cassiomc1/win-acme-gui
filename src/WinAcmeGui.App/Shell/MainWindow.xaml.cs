using System.Windows;
using Microsoft.Win32;
using WinAcmeGui.App.Features;
using WinAcmeGui.App.Localization;

namespace WinAcmeGui.App.Shell;

public partial class MainWindow : Window
{
    private readonly CultureService _culture = new();

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel(_culture);
        DataContext = viewModel;
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
            button.Click += (_, _) => viewModel.SelectSection((string)button.Tag);
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
    }

    private void PortugueseClick(object sender, RoutedEventArgs e)
    {
        _culture.SetCulture("pt-BR");
        ((MainWindowViewModel)DataContext).RefreshLabels();
    }

    private void DownloadClick(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.win-acme.com/") { UseShellExecute = true });
    }

    private async void SelectExecutableClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "win-acme executable|wacs.exe;wacs|All files|*.*", Title = _culture["SelectExecutable"] };
        if (dialog.ShowDialog(this) == true)
            await ((MainWindowViewModel)DataContext).UseExecutableAsync(dialog.FileName);
    }

    private void NewCertificateClick(object sender, RoutedEventArgs e) =>
        OpenCertificateWindow();

    private void OpenCertificateWindow()
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.ActiveCandidate is null) { MessageBox.Show("Select or discover a win-acme installation first."); return; }
        new NewCertificateWindow(vm.ActiveCandidate.ExecutablePath) { Owner = this }.ShowDialog();
    }

    private async void RenewClick(object sender, RoutedEventArgs e) => await RunRenewal(false);

    private async void ForceRenewClick(object sender, RoutedEventArgs e) => await RunRenewal(true);

    private async Task RunRenewal(bool force)
    {
        if (((MainWindowViewModel)DataContext).SelectedRenewal is null)
        {
            MessageBox.Show("Select a renewal first.", "win-acme GUI", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = await ((MainWindowViewModel)DataContext).RenewSelectedAsync(force);
        MessageBox.Show(result?.ErrorCode is null ? "Operation finished successfully." : result.ErrorCode, "win-acme GUI", MessageBoxButton.OK, result?.ErrorCode is null ? MessageBoxImage.Information : MessageBoxImage.Error);
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
}
