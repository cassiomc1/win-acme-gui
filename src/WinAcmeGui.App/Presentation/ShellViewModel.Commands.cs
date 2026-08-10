using System.Text;

namespace WinAcmeGui.App.Presentation;

public sealed partial class ShellViewModel
{
    /// <summary>Downloads and extracts the latest official release, then rediscovers installations.</summary>
    public async Task DownloadLatestAsync()
    {
        if (IsBusy) return;
        using var cancellation = BeginCancellableOperation();
        IsBusy = true;
        Status = Culture["Download"];
        try
        {
            var package = await _installer.InstallLatestAsync(null, cancellation.Token);
            Status = $"{package.Version} · {package.Destination}";
            Log("OperationDownload", ActivityOutcome.Succeeded, Status);
            _interaction.ShowMessage(
                Culture["DownloadCompleted"],
                Culture.Format("DownloadCompletedMessage", package.Version, package.Destination));
            IsBusy = false;
            EndCancellableOperation(cancellation);
            await LoadAsync();
            return;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = Describe("OperationDownload", "OperationCancelled");
            Log("OperationDownload", ActivityOutcome.Cancelled, Status);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            Log("OperationDownload", ActivityOutcome.Failed, ex.Message);
            _interaction.ShowMessage(Culture["DownloadFailed"], ex.Message, DialogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            EndCancellableOperation(cancellation);
        }
    }

    public async Task SelectExecutableAsync()
    {
        var path = _interaction.PickExecutable(Culture["SelectExecutable"]);
        if (string.IsNullOrWhiteSpace(path)) return;
        await UseExecutableAsync(path);
    }

    public async Task UseInstallationAsync(InstallationRow? row)
    {
        if (row is null) return;
        if (!row.IsOperational)
        {
            _interaction.ShowMessage(Culture["Warning"], row.Diagnostic ?? Culture["ReadOnlyBadge"], DialogSeverity.Warning);
            return;
        }
        await UseExecutableAsync(row.ExecutablePath);
    }

    public void CopyActivity() =>
        _interaction.CopyToClipboard(string.Join(Environment.NewLine, Activity.Select(x => x.ToPlainText())));

    /// <summary>Copies the System page facts so they can be pasted into a support thread.</summary>
    public void CopySystemDetails()
    {
        var builder = new StringBuilder()
            .AppendLine($"{Culture["GuiVersion"]}: {GuiVersionText}")
            .AppendLine($"{Culture["HostPlatform"]}: {PlatformText}")
            .AppendLine($"{Culture["ElevationMode"]}: {ExecutionModeText}")
            .AppendLine($"{Culture["ActiveInstallation"]}: {ActiveExecutablePath}")
            .AppendLine($"{Culture["DetectedInstallation"]}: {ActiveVersion}")
            .AppendLine($"{Culture["ConfigurationPath"]}: {ActiveConfigurationPath}")
            .AppendLine($"{Culture["SettingsFile"]}: {SettingsPath}")
            .AppendLine($"{Culture["Endpoint"]}: {ActiveEndpoint} ({EndpointKindText})")
            .AppendLine($"{Culture["RenewalsLoaded"]}: {TotalRenewalCount}");
        _interaction.CopyToClipboard(builder.ToString());
        Status = Culture["CopiedToClipboard"];
    }
}
