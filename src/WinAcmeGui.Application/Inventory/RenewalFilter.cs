using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Inventory;

public static class RenewalFilter
{
    public static IReadOnlyList<Renewal> Apply(IEnumerable<Renewal> renewals, string? query) =>
        Apply(renewals, query, null);

    /// <summary>
    /// Filters by free text and, optionally, by an exact status. A null <paramref name="status"/> keeps
    /// every status so the caller can offer an "all" choice without a separate code path.
    /// </summary>
    public static IReadOnlyList<Renewal> Apply(IEnumerable<Renewal> renewals, string? query, RenewalStatus? status)
    {
        var filtered = status is { } required
            ? renewals.Where(renewal => renewal.Status == required)
            : renewals;

        var normalized = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return filtered.ToArray();

        return filtered
            .Where(renewal => renewal.FriendlyName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || renewal.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || renewal.Domains.Any(domain => domain.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                || renewal.Status.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
