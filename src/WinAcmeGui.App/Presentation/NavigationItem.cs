using WinAcmeGui.App.Localization;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.App.Presentation;

/// <summary>
/// A navigation entry. It keeps its own culture subscription so a language switch re-renders the
/// sidebar without the shell rebuilding the item list.
/// </summary>
public sealed class NavigationItem : ObservableObject
{
    private readonly CultureService _culture;
    private bool _isSelected;

    public NavigationItem(CultureService culture, string id, string titleKey, string descriptionKey, string glyph)
    {
        _culture = culture;
        Id = id;
        TitleKey = titleKey;
        DescriptionKey = descriptionKey;
        Glyph = glyph;
        _culture.CultureChanged += (_, _) => Raise(nameof(Title), nameof(Description));
    }

    public string Id { get; }
    public string TitleKey { get; }
    public string DescriptionKey { get; }

    /// <summary>Single character drawn as the sidebar icon; avoids shipping an icon font.</summary>
    public string Glyph { get; }

    public string Title => _culture[TitleKey];
    public string Description => _culture[DescriptionKey];

    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
}

/// <summary>A renewal status choice in the filter bar; <c>null</c> status means "all".</summary>
public sealed class RenewalStatusOption : ObservableObject
{
    private readonly CultureService _culture;

    public RenewalStatusOption(CultureService culture, RenewalStatus? status, string labelKey)
    {
        _culture = culture;
        Status = status;
        LabelKey = labelKey;
        _culture.CultureChanged += (_, _) => Raise(nameof(Label));
    }

    public RenewalStatus? Status { get; }
    public string LabelKey { get; }
    public string Label => _culture[LabelKey];
}
