using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Services;

public static class HomeLayoutSanitizer
{
    public static HomeLayoutDto Sanitize(HomeLayoutDto layout, IReadOnlySet<Guid> validLibraryIds)
    {
        var cleanedRows = new List<HomeRowConfigDto>();
        var order = 0;

        foreach (var row in layout.Rows.OrderBy(r => r.Order))
        {
            if (row.ContinueWatching || row.LibraryIds is not { Count: > 0 })
            {
                cleanedRows.Add(row with { Order = order++ });
                continue;
            }

            var validIds = row.LibraryIds.Where(validLibraryIds.Contains).ToList();
            if (validIds.Count == 0)
                continue;

            cleanedRows.Add(row with { LibraryIds = validIds, Order = order++ });
        }

        return new HomeLayoutDto { Rows = cleanedRows };
    }

    public static bool HasChanges(HomeLayoutDto original, HomeLayoutDto sanitized) =>
        original.Rows.Count != sanitized.Rows.Count
        || original.Rows.Zip(sanitized.Rows).Any(pair => !RowReferencesMatch(pair.First, pair.Second));

    private static bool RowReferencesMatch(HomeRowConfigDto left, HomeRowConfigDto right)
    {
        if (left.Id != right.Id || left.Order != right.Order)
            return false;

        var leftIds = left.LibraryIds?.OrderBy(id => id).ToList() ?? [];
        var rightIds = right.LibraryIds?.OrderBy(id => id).ToList() ?? [];
        return leftIds.SequenceEqual(rightIds);
    }
}
