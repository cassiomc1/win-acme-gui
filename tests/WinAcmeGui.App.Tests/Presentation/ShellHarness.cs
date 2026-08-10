using WinAcmeGui.App.Localization;
using WinAcmeGui.App.Presentation;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Tests.Presentation;

/// <summary>
/// Builds a <see cref="ShellViewModel"/> over fakes and a temporary configuration directory, so shell
/// behaviour can be exercised without win-acme, WPF or the file layout of a real installation.
/// </summary>
public sealed class ShellHarness : IDisposable
{
    private readonly string _configurationDirectory;

    private ShellHarness(
        ShellViewModel viewModel,
        FakeInteraction interaction,
        RecordingRunner runner,
        FakeInstaller installer,
        string configurationDirectory,
        string executablePath)
    {
        ViewModel = viewModel;
        Interaction = interaction;
        Runner = runner;
        Installer = installer;
        _configurationDirectory = configurationDirectory;
        ExecutablePath = executablePath;
    }

    public ShellViewModel ViewModel { get; }
    public FakeInteraction Interaction { get; }
    public RecordingRunner Runner { get; }
    public FakeInstaller Installer { get; }
    public string ExecutablePath { get; }

    public static ShellHarness Create(
        IEnumerable<Renewal>? renewals = null,
        bool discoverInstallation = true,
        bool operational = true,
        string culture = LocalizationTable.PortugueseBrazilCulture)
    {
        var root = Path.Combine(Path.GetTempPath(), "win-acme-gui-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executablePath = Path.Combine(root, "wacs.exe");
        File.WriteAllText(executablePath, string.Empty);

        var snapshot = new ConfigurationSnapshot(
            Path.Combine(root, "settings.json"),
            "win-acme",
            root,
            AcmeEndpoint.Production,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var candidate = new InstallationCandidate(
            executablePath,
            "2.2.9.1701",
            root,
            AcmeEndpoint.Production,
            snapshot,
            operational,
            operational ? null : "Shared configuration directory.");

        var validator = new FakeValidator(candidate);
        var discovery = new DiscoverInstallations(
            [new FakeCandidateSource(discoverInstallation ? [executablePath] : [])],
            validator);
        var inventory = new InventoryService(
            new FakeConfigurationReader(snapshot),
            new FakeRenewalReader((renewals ?? []).ToArray()));

        var runner = new RecordingRunner();
        var commandFactory = new WinAcmeCommandFactory();
        var services = new ShellServices(
            discovery,
            validator,
            inventory,
            new ManageRenewal(runner, commandFactory),
            new ManageCertificate(runner, new CertificateDraftValidator(), commandFactory),
            UsesElevatedWorker: false);

        var interaction = new FakeInteraction();
        var installer = new FakeInstaller();
        var cultureService = new CultureService();
        cultureService.SetCulture(culture);
        var viewModel = new ShellViewModel(
            cultureService,
            services,
            interaction,
            installer,
            () => new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));

        return new(viewModel, interaction, runner, installer, root, executablePath);
    }

    /// <summary>A renewal in the harness's configuration directory.</summary>
    public static Renewal Renewal(
        string id,
        string name,
        RenewalStatus status = RenewalStatus.Healthy,
        bool editable = true,
        params string[] domains) =>
        new(id, name, domains.Length > 0 ? domains : [$"{id}.example.com"], status, editable, $"{id}.renewal.json", []);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_configurationDirectory)) Directory.Delete(_configurationDirectory, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
