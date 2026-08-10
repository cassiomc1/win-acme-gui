using System.IO;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Infrastructure.Configuration;
using WinAcmeGui.Infrastructure.Discovery;
using WinAcmeGui.Infrastructure.Operations;
using WinAcmeGui.Infrastructure.Renewals;

namespace WinAcmeGui.App.Presentation;

/// <summary>
/// The collaborators the shell view model needs. Explicit construction keeps the view model testable
/// off-Windows while <see cref="CreateDefault"/> preserves the real, production wiring.
/// </summary>
public sealed record ShellServices(
    DiscoverInstallations Discovery,
    IInstallationValidator Validator,
    InventoryService Inventory,
    ManageRenewal Renewals,
    ManageCertificate Certificates,
    bool UsesElevatedWorker)
{
    public static ShellServices CreateDefault()
    {
        var versionProbe = new ProcessWinAcmeVersionProbe();
        var configurationReader = new WinAcmeConfigurationReader(versionProbe);
        var validator = new InstallationValidator(versionProbe, configurationReader);
        var discovery = new DiscoverInstallations(
            [
                new ScheduledTaskCandidateSource(),
                new PathCandidateSource(),
                new KnownLocationCandidateSource(AppContext.BaseDirectory),
                new ProcessCandidateSource()
            ],
            validator);
        var inventory = new InventoryService(configurationReader, new RenewalDocumentReader());
        var usesWorker = OperatingSystem.IsWindows();
        var runner = usesWorker
            ? new NamedPipeElevatedOperationClient(
                Path.Combine(AppContext.BaseDirectory, "worker", "WinAcmeGui.ElevatedWorker.exe"))
            : (IWinAcmeRunner)new WinAcmeProcessRunner();
        var commandFactory = new WinAcmeCommandFactory();
        return new(
            discovery,
            validator,
            inventory,
            new ManageRenewal(runner, commandFactory),
            new ManageCertificate(runner, new CertificateDraftValidator(), commandFactory),
            usesWorker);
    }
}
