using WinAcmeGui.App.Localization;
using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.App.Presentation;

/// <summary>A discovered win-acme installation as shown on the Installation page.</summary>
public sealed class InstallationRow : ObservableObject
{
    private readonly CultureService _culture;
    private bool _isActive;

    public InstallationRow(InstallationCandidate candidate, CultureService culture, bool isActive)
    {
        Candidate = candidate;
        _culture = culture;
        _isActive = isActive;
        _culture.CultureChanged += (_, _) => Raise(nameof(StateText), nameof(BadgeText));
    }

    public InstallationCandidate Candidate { get; }

    public string ExecutablePath => Candidate.ExecutablePath;
    public string VersionText => Candidate.VersionText;
    public string ConfigurationPath => Candidate.ConfigurationPath;
    public bool IsOperational => Candidate.IsOperational;
    public string? Diagnostic => Candidate.Diagnostic;
    public bool HasDiagnostic => !string.IsNullOrWhiteSpace(Candidate.Diagnostic);

    public bool IsActive { get => _isActive; set { if (SetField(ref _isActive, value)) Raise(nameof(BadgeText), nameof(HasBadge)); } }

    public bool HasBadge => IsActive || !IsOperational;

    public string BadgeText => IsActive ? _culture["ActiveBadge"] : !IsOperational ? _culture["ReadOnlyBadge"] : string.Empty;

    public string StateText => IsOperational ? _culture["ReadyToOperate"] : _culture["ReadOnlyBadge"];

    public string StateBrushKey => IsOperational ? "StatusHealthyBrush" : "StatusNeutralBrush";
}
