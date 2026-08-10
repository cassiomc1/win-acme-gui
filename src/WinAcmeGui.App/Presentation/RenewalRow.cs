using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Presentation;

/// <summary>Presentation wrapper around a <see cref="Renewal"/>: localized status text plus a style key.</summary>
public sealed class RenewalRow(Renewal renewal, CultureService culture)
{
    public Renewal Renewal { get; } = renewal;

    public string Id => Renewal.Id;
    public string FriendlyName => Renewal.FriendlyName;
    public string Domains => string.Join(", ", Renewal.Domains);
    public string Diagnostics => string.Join("; ", Renewal.Diagnostics.Select(x => x.Message));
    public bool IsEditable => Renewal.IsEditable;
    public bool HasDiagnostics => Renewal.Diagnostics.Count > 0;
    public RenewalStatus Status => Renewal.Status;

    /// <summary>Localized status label for the grid badge.</summary>
    public string StatusText => culture[StatusKey(Renewal.Status)];

    /// <summary>Resource key of the badge brush, resolved dynamically so it follows the active theme.</summary>
    public string StatusBrushKey => Renewal.Status switch
    {
        RenewalStatus.Healthy => "StatusHealthyBrush",
        RenewalStatus.DueSoon => "StatusWarningBrush",
        RenewalStatus.Failed => "StatusDangerBrush",
        RenewalStatus.Expired => "StatusDangerBrush",
        _ => "StatusNeutralBrush"
    };

    public static string StatusKey(RenewalStatus status) => status switch
    {
        RenewalStatus.Healthy => "StatusHealthy",
        RenewalStatus.DueSoon => "StatusDueSoon",
        RenewalStatus.Failed => "StatusFailed",
        RenewalStatus.Expired => "StatusExpired",
        _ => "StatusUnreadable"
    };
}
