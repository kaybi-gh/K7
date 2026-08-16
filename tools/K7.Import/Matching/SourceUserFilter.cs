using K7.Import.Models;

namespace K7.Import.Matching;

internal static class SourceUserFilter
{
    public static SourceUserFilterResult Apply(
        IReadOnlyList<SourceUser> users,
        IReadOnlyList<string>? filters)
    {
        var normalized = (filters ?? [])
            .Select(f => f.Trim())
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new SourceUserFilterResult(
                IsActive: false,
                Kept: users.ToList(),
                Excluded: [],
                UnmatchedFilters: []);
        }

        var kept = new List<SourceUser>();
        var excluded = new List<SourceUser>();
        foreach (var user in users)
        {
            if (normalized.Any(filter => Matches(user, filter)))
                kept.Add(user);
            else
                excluded.Add(user);
        }

        var unmatched = normalized
            .Where(filter => users.All(user => !Matches(user, filter)))
            .ToList();

        return new SourceUserFilterResult(
            IsActive: true,
            Kept: kept,
            Excluded: excluded,
            UnmatchedFilters: unmatched);
    }

    private static bool Matches(SourceUser user, string filter) =>
        string.Equals(user.Id, filter, StringComparison.OrdinalIgnoreCase)
        || string.Equals(user.Name, filter, StringComparison.OrdinalIgnoreCase)
        || string.Equals(user.DisplayName, filter, StringComparison.OrdinalIgnoreCase);
}

internal sealed record SourceUserFilterResult(
    bool IsActive,
    IReadOnlyList<SourceUser> Kept,
    IReadOnlyList<SourceUser> Excluded,
    IReadOnlyList<string> UnmatchedFilters);
