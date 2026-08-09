using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Inventory;

public static class RenewalFilter
{
    public static IReadOnlyList<Renewal> Apply(IEnumerable<Renewal> renewals, string? query)
    {
        var normalized = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return renewals.ToArray();

        return renewals
            .Where(renewal => renewal.FriendlyName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || renewal.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || renewal.Domains.Any(domain => domain.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                || renewal.Status.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
