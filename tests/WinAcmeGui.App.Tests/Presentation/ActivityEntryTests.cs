using System.Globalization;
using FluentAssertions;
using WinAcmeGui.App.Localization;
using WinAcmeGui.App.Presentation;

namespace WinAcmeGui.App.Tests.Presentation;

public sealed class ActivityEntryTests
{
    [Fact]
    public void Existing_activity_rows_notify_when_the_language_changes()
    {
        var culture = new CultureService(CultureInfo.GetCultureInfo("pt-BR"));
        var entry = new ActivityEntry(
            new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
            "OperationRenew",
            ActivityOutcome.Succeeded,
            "ok",
            culture);
        var changed = new List<string?>();
        entry.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        culture.SetCulture(LocalizationTable.EnglishCulture);

        entry.OperationText.Should().Be("Renewal");
        entry.OutcomeText.Should().Be("Succeeded");
        changed.Should().Contain(nameof(ActivityEntry.OperationText));
        changed.Should().Contain(nameof(ActivityEntry.OutcomeText));
        changed.Should().Contain(nameof(ActivityEntry.TimeText));
    }
}
