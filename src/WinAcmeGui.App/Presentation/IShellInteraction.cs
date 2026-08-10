namespace WinAcmeGui.App.Presentation;

public enum DialogSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>
/// Everything the shell needs from the windowing layer. Keeping it behind an interface lets the whole
/// presentation layer compile and be tested on non-Windows hosts, where WPF is unavailable.
/// </summary>
public interface IShellInteraction
{
    void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information);

    /// <summary>Returns true only when the user explicitly confirms.</summary>
    bool Confirm(string title, string message, DialogSeverity severity = DialogSeverity.Warning);

    /// <summary>
    /// Asks the user to retype <paramref name="expectedAnswer"/>. Returns the typed text, or null when
    /// the dialog was dismissed.
    /// </summary>
    string? PromptForConfirmationText(string title, string message, string expectedAnswer);

    /// <summary>Returns the chosen executable path, or null when the picker was dismissed.</summary>
    string? PickExecutable(string title);

    void CopyToClipboard(string text);

    void OpenExternal(string target);
}

/// <summary>Interaction stub used by tests and by the non-visual build; it never blocks and never confirms.</summary>
public sealed class NullShellInteraction : IShellInteraction
{
    public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information) { }

    public bool Confirm(string title, string message, DialogSeverity severity = DialogSeverity.Warning) => false;

    public string? PromptForConfirmationText(string title, string message, string expectedAnswer) => null;

    public string? PickExecutable(string title) => null;

    public void CopyToClipboard(string text) { }

    public void OpenExternal(string target) { }
}
