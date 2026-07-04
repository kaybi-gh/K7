namespace K7.Clients.Shared.UI.Components;

internal static class CatalogCarouselRefreshScope
{
    internal static bool IsAffected(Guid[]? libraryIds, Guid[]? libraryGroupIds, Guid eventLibraryId)
    {
        if (libraryIds is { Length: > 0 })
            return libraryIds.Contains(eventLibraryId);

        if (libraryGroupIds is { Length: > 0 })
            return true;

        return true;
    }
}
