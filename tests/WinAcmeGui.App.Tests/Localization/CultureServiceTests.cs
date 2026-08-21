using System.Globalization;
using FluentAssertions;
using WinAcmeGui.App.Localization;

namespace WinAcmeGui.App.Tests.Localization;

public sealed class CultureServiceTests
{
    [Theory]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("en-GB", "en-US")]
    [InlineData("de-DE", "en-US")]
    public void Chooses_supported_initial_culture(string windowsCulture, string expected) =>
        CultureService.ChooseInitial(windowsCulture).Should().Be(expected);

    [Fact]
    public void Both_languages_expose_core_navigation_labels()
    {
        var service = new CultureService();
        service.SetCulture("pt-BR");
        service["Renewals"].Should().Be("Renovações");
        service.SetCulture("en-US");
        service["Renewals"].Should().Be("Renewals");
    }

    [Fact]
    public void All_supported_resource_keys_have_values_in_both_languages()
    {
        var service = new CultureService();
        foreach (var key in CultureService.Keys)
        {
            service.SetCulture("pt-BR");
            service[key].Should().NotBeNullOrWhiteSpace();
            service.SetCulture("en-US");
            service[key].Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void An_unknown_key_returns_itself_so_a_gap_is_visible_instead_of_fatal()
    {
        var service = new CultureService();
        service["NoSuchKey"].Should().Be("NoSuchKey");
    }

    [Fact]
    public void Switching_language_raises_the_indexer_notification_that_bindings_listen_to()
    {
        var service = new CultureService();
        service.SetCulture("pt-BR");
        var raised = new List<string?>();
        var cultureChanged = 0;
        service.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        service.CultureChanged += (_, _) => cultureChanged++;

        service.SetCulture("en-US");

        raised.Should().Contain("Item[]");
        raised.Should().Contain(nameof(CultureService.CultureName));
        cultureChanged.Should().Be(1);
    }

    [Fact]
    public void Re_selecting_the_active_language_reasserts_bindings_without_changing_culture()
    {
        var service = new CultureService();
        service.SetCulture("en-US");
        var raised = new List<string?>();
        var cultureChanged = 0;
        service.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        service.CultureChanged += (_, _) => cultureChanged++;

        service.SetCulture("en-US");

        // Selector controls bind IsChecked OneWay; a click on the active option must reassert the
        // visual state even though the culture itself is unchanged.
        raised.Should().Contain(nameof(CultureService.IsPortuguese));
        raised.Should().Contain(nameof(CultureService.IsEnglish));
        cultureChanged.Should().Be(0);
        service.CultureName.Should().Be("en-US");
    }

    [Fact]
    public void Format_substitutes_positional_arguments()
    {
        var service = new CultureService();
        service.SetCulture("en-US");

        service.Format("DownloadCompletedMessage", "2.2.9", @"C:\win-acme")
            .Should().Contain("2.2.9").And.Contain(@"C:\win-acme");
    }

    [Fact]
    public void IsPortuguese_and_IsEnglish_track_the_active_language()
    {
        var service = new CultureService();

        service.SetCulture("pt-BR");
        service.IsPortuguese.Should().BeTrue();
        service.IsEnglish.Should().BeFalse();

        service.SetCulture("en-US");
        service.IsPortuguese.Should().BeFalse();
        service.IsEnglish.Should().BeTrue();
    }

    [Fact]
    public void Selecting_pt_br_replaces_a_pt_pt_initial_culture()
    {
        var service = new CultureService(CultureInfo.GetCultureInfo("pt-PT"));

        service.SetCulture("pt-BR");

        service.Current.Name.Should().Be("pt-BR");
    }
}
