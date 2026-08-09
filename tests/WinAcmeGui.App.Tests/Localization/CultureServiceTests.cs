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
}
