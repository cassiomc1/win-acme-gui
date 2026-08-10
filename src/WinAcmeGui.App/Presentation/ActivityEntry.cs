using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Presentation;

public enum ActivityOutcome
{
    Information,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>One row in the in-session activity log. Detail text is already redacted upstream.</summary>
public sealed class ActivityEntry : ObservableObject
{
    private readonly CultureService _culture;

    public ActivityEntry(
        DateTimeOffset timestamp,
        string operationKey,
        ActivityOutcome outcome,
        string detail,
        CultureService culture)
    {
        Timestamp = timestamp;
        OperationKey = operationKey;
        Outcome = outcome;
        Detail = detail;
        _culture = culture;
        _culture.CultureChanged += (_, _) =>
            Raise(nameof(TimeText), nameof(OperationText), nameof(OutcomeText));
    }

    public DateTimeOffset Timestamp { get; }
    public string OperationKey { get; }
    public ActivityOutcome Outcome { get; }
    public string Detail { get; }

    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss", _culture.Current);
    public string OperationText => _culture[OperationKey];
    public string OutcomeText => _culture[OutcomeKey(Outcome)];

    public string OutcomeBrushKey => Outcome switch
    {
        ActivityOutcome.Succeeded => "StatusHealthyBrush",
        ActivityOutcome.Failed => "StatusDangerBrush",
        ActivityOutcome.Cancelled => "StatusWarningBrush",
        _ => "StatusNeutralBrush"
    };

    public static string OutcomeKey(ActivityOutcome outcome) => outcome switch
    {
        ActivityOutcome.Succeeded => "ResultSucceeded",
        ActivityOutcome.Failed => "ResultFailed",
        ActivityOutcome.Cancelled => "ResultCancelled",
        _ => "ResultInfo"
    };

    public static ActivityOutcome FromStatus(OperationStatus status) => status switch
    {
        OperationStatus.Succeeded => ActivityOutcome.Succeeded,
        OperationStatus.Cancelled => ActivityOutcome.Cancelled,
        _ => ActivityOutcome.Failed
    };

    public string ToPlainText() => $"{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}\t{OperationText}\t{OutcomeText}\t{Detail}";
}
