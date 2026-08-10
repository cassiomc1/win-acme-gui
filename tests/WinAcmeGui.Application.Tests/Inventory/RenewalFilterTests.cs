using FluentAssertions;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Tests.Inventory;

public sealed class RenewalFilterTests
{
    private static readonly Renewal[] Renewals =
    [
        new("one", "Production", ["example.com"], RenewalStatus.Healthy, true, "one.json", []),
        new("two", "Staging", ["staging.example.net"], RenewalStatus.DueSoon, true, "two.json", []),
        new("three", "Broken", [], RenewalStatus.Unreadable, false, "three.json", [])
    ];

    [Fact]
    public void Matches_friendly_name_id_domain_and_status_without_case_sensitivity()
    {
        RenewalFilter.Apply(Renewals, "STAGING").Should().ContainSingle(x => x.Id == "two");
        RenewalFilter.Apply(Renewals, "EXAMPLE.COM").Should().ContainSingle(x => x.Id == "one");
        RenewalFilter.Apply(Renewals, "unreadable").Should().ContainSingle(x => x.Id == "three");
    }

    [Fact]
    public void Empty_query_returns_all_rows_in_original_order()
    {
        RenewalFilter.Apply(Renewals, " ").Select(x => x.Id).Should().Equal("one", "two", "three");
    }

    [Fact]
    public void A_status_filter_keeps_only_that_status()
    {
        RenewalFilter.Apply(Renewals, null, RenewalStatus.DueSoon).Select(x => x.Id).Should().Equal("two");
        RenewalFilter.Apply(Renewals, null, RenewalStatus.Expired).Should().BeEmpty();
    }

    [Fact]
    public void A_null_status_keeps_every_status()
    {
        RenewalFilter.Apply(Renewals, null, null).Should().HaveCount(3);
    }

    [Fact]
    public void Text_and_status_filters_are_combined()
    {
        RenewalFilter.Apply(Renewals, "example", RenewalStatus.Healthy).Select(x => x.Id).Should().Equal("one");
        RenewalFilter.Apply(Renewals, "staging", RenewalStatus.Healthy).Should().BeEmpty();
    }
}
