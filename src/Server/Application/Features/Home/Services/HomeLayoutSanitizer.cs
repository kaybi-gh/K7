using K7.Shared.Dtos.Home;
using K7.Shared.Home;

namespace K7.Server.Application.Features.Home.Services;

public static class HomeLayoutSanitizer
{
    public static HomeLayoutDto Sanitize(
        HomeLayoutDto layout,
        IReadOnlySet<Guid> validLibraryIds,
        IReadOnlySet<Guid> validLibraryGroupIds)
    {
        var cleanedRows = new List<HomeRowConfigDto>();
        var order = 0;

        foreach (var row in layout.Rows.OrderBy(r => r.Order))
        {
            if (row.ContinueWatching)
            {
                cleanedRows.Add(row with { LibraryIds = null, LibraryGroupIds = null, Order = order++ });
                continue;
            }

            var sanitized = SanitizeCatalogScope(row, validLibraryIds, validLibraryGroupIds);
            if (sanitized is null)
                continue;

            cleanedRows.Add(sanitized with { Order = order++ });
        }

        return new HomeLayoutDto { Rows = cleanedRows };
    }

    public static bool HasChanges(HomeLayoutDto original, HomeLayoutDto sanitized) =>
        original.Rows.Count != sanitized.Rows.Count
        || original.Rows.Zip(sanitized.Rows).Any(pair => !RowReferencesMatch(pair.First, pair.Second));

    private static HomeRowConfigDto? SanitizeCatalogScope(
        HomeRowConfigDto row,
        IReadOnlySet<Guid> validLibraryIds,
        IReadOnlySet<Guid> validLibraryGroupIds)
    {
        var groupIds = row.LibraryGroupIds?.Where(validLibraryGroupIds.Contains).ToList() ?? [];
        var hadExplicitGroupScope = row.LibraryGroupIds is { Count: > 0 };

        // Legacy default "Newly added in {group}" rows snapshotted LibraryIds under Id = group.Id.
        // Promote them to a live library-group scope so new libraries in the group appear.
        var isLegacyNewlyAddedGroupRow = !hadExplicitGroupScope
            && validLibraryGroupIds.Contains(row.Id)
            && HomeLayoutRowTitles.TryParseNewlyAddedIn(row.Title, out _);

        if (groupIds.Count == 0 && isLegacyNewlyAddedGroupRow)
            groupIds = [row.Id];

        if (hadExplicitGroupScope || isLegacyNewlyAddedGroupRow)
        {
            if (groupIds.Count == 0)
                return null;

            return row with
            {
                LibraryGroupIds = groupIds,
                LibraryIds = null
            };
        }

        if (row.LibraryIds is { Count: > 0 })
        {
            var libraryIds = row.LibraryIds.Where(validLibraryIds.Contains).ToList();
            if (libraryIds.Count == 0)
                return null;

            return row with
            {
                LibraryIds = libraryIds,
                LibraryGroupIds = null
            };
        }

        return row with { LibraryIds = null, LibraryGroupIds = null };
    }

    private static bool RowReferencesMatch(HomeRowConfigDto left, HomeRowConfigDto right)
    {
        if (left.Id != right.Id || left.Order != right.Order)
            return false;

        var leftLibraryIds = left.LibraryIds?.OrderBy(id => id).ToList() ?? [];
        var rightLibraryIds = right.LibraryIds?.OrderBy(id => id).ToList() ?? [];
        if (!leftLibraryIds.SequenceEqual(rightLibraryIds))
            return false;

        var leftGroupIds = left.LibraryGroupIds?.OrderBy(id => id).ToList() ?? [];
        var rightGroupIds = right.LibraryGroupIds?.OrderBy(id => id).ToList() ?? [];
        return leftGroupIds.SequenceEqual(rightGroupIds);
    }
}
