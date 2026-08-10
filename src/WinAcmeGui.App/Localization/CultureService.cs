using System.ComponentModel;
using System.Globalization;

namespace WinAcmeGui.App.Localization;

/// <summary>
/// Runtime language selection. XAML binds through the indexer (<c>{Binding Culture[Renewals]}</c>);
/// raising <c>Item[]</c> on a language switch refreshes every one of those bindings at once, so no
/// per-label passthrough property is needed.
/// </summary>
public sealed class CultureService : INotifyPropertyChanged
{
    private IReadOnlyDictionary<string, string> _strings;

    public CultureService(CultureInfo? initialCulture = null)
    {
        Current = initialCulture ?? CultureInfo.CurrentUICulture;
        _strings = LocalizationTable.For(Current.Name);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Current { get; private set; }

    public static IReadOnlyCollection<string> Keys => LocalizationTable.Keys;

    public string CultureName => LocalizationTable.Normalize(Current.Name);

    public bool IsPortuguese => CultureName == LocalizationTable.PortugueseBrazilCulture;

    public bool IsEnglish => !IsPortuguese;

    /// <summary>Returns the key itself when missing so a gap shows up in the UI instead of crashing.</summary>
    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    /// <summary>Localized text with positional arguments, e.g. "win-acme {0} extracted to {1}".</summary>
    public string Format(string key, params object?[] arguments) =>
        string.Format(Current, this[key], arguments);

    public static string ChooseInitial(string windowsCulture) => LocalizationTable.Normalize(windowsCulture);

    public void SetCulture(string name)
    {
        var selected = LocalizationTable.Normalize(name);
        if (Current.Name.Equals(selected, StringComparison.OrdinalIgnoreCase) && _strings.Count > 0) return;
        Current = CultureInfo.GetCultureInfo(selected);
        CultureInfo.CurrentCulture = Current;
        CultureInfo.CurrentUICulture = Current;
        _strings = LocalizationTable.For(selected);
        Raise("Item[]");
        Raise(nameof(CultureName));
        Raise(nameof(Current));
        Raise(nameof(IsPortuguese));
        Raise(nameof(IsEnglish));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised after the active language changed, for consumers that cache derived text.</summary>
    public event EventHandler? CultureChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
