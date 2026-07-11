namespace K7.Clients.Shared.Helpers;

public static class MediaBrowseCarouselRefreshScope
{
    public static bool IsAffected(Guid[]? libraryIds, Guid[]? libraryGroupIds, Guid eventLibraryId)
    {
        if (libraryIds is { Length: > 0 })
            return libraryIds.Contains(eventLibraryId);

        if (libraryGroupIds is { Length: > 0 })
            return true;

        return true;
    }
}
