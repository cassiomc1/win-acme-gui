using System.Globalization;
using System.Windows;
using WinAcmeGui.App.Features;
using WinAcmeGui.App.Localization;
using WinAcmeGui.App.Presentation;
using WinAcmeGui.App.Windows;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Shell;

public partial class MainWindow : Window
{
    private readonly CultureService _culture = new();
    private readonly ShellViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _culture.SetCulture(CultureService.ChooseInitial(CultureInfo.CurrentUICulture.Name));
        _viewModel = new ShellViewModel(_culture, interaction: new WpfShellInteraction(this, _culture))
        {
            CertificateWizardLauncher = ShowCertificateWizardAsync
        };
        _viewModel.ThemeChanged += (_, isDark) => ApplyTheme(isDark);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    /// <summary>
    /// Swaps the palette dictionary in place. It is merged first in App.xaml, so replacing index 0
    /// re-resolves every DynamicResource without touching the rest of the theme.
    /// </summary>
    private static void ApplyTheme(bool isDark)
    {
        var application = System.Windows.Application.Current;
        if (application is null) return;
        var palette = new ResourceDictionary { Source = ThemePalettes.SourceFor(isDark) };
        var dictionaries = application.Resources.MergedDictionaries;
        if (dictionaries.Count == 0) dictionaries.Add(palette);
        else dictionaries[0] = palette;
    }

    private Task<OperationResult?> ShowCertificateWizardAsync()
    {
        var candidate = _viewModel.ActiveCandidate;
        if (candidate is null) return Task.FromResult<OperationResult?>(null);
        var wizard = new CertificateWizardViewModel(
            candidate.ExecutablePath,
            _viewModel.CreateCertificateAsync,
            _culture,
            new WpfShellInteraction(this, _culture));
        var window = new CertificateWizardWindow(wizard) { Owner = this };
        _ = window.ShowDialog();
        return Task.FromResult(wizard.LastResult);
    }
}
