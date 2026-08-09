using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Notifications;

namespace K7.Clients.Shared.Helpers;

public static class MediaBrowseCarouselRefreshScope
{
    /// <summary>
    /// Library-scoped events (scan, indexed files). Subscriptions scoped only by library-group ids
    /// must also pass member library ids; otherwise those events are ignored.
    /// </summary>
    public static bool IsAffected(Guid[]? libraryIds, Guid[]? libraryGroupIds, Guid eventLibraryId)
    {
        if (libraryIds is { Length: > 0 })
            return libraryIds.Contains(eventLibraryId);

        // Library-group-only: cannot resolve membership without library ids.
        if (libraryGroupIds is { Length: > 0 })
            return false;

        return true;
    }

    public static bool IsBatchAffected(
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        IReadOnlyCollection<MediaType>? mediaTypes,
        IReadOnlyList<MediaBatchItem> items)
    {
        if (items.Count == 0)
            return false;

        var matched = items.Where(i => MatchesMediaType(mediaTypes, i.MediaType)).ToList();
        if (matched.Count == 0)
            return false;

        if (libraryIds is { Length: > 0 })
        {
            // Prefer library id when present; items without LibraryId cannot be attributed.
            return matched.Any(i => i.LibraryId is { } id && libraryIds.Contains(id));
        }

        // Library-group-only without member library ids: media-type filter is the best we can do.
        if (libraryGroupIds is { Length: > 0 })
            return true;

        return true;
    }

    public static bool MatchesMediaType(IReadOnlyCollection<MediaType>? mediaTypes, string batchMediaType)
    {
        if (mediaTypes is null || mediaTypes.Count == 0)
            return true;

        if (!Enum.TryParse<MediaType>(batchMediaType, ignoreCase: true, out var type))
            return true;

        return mediaTypes.Contains(type);
    }

    public static MediaType[]? ForLibraryMediaType(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => [MediaType.Movie],
        LibraryMediaType.Serie => [MediaType.Serie, MediaType.SerieSeason, MediaType.SerieEpisode],
        LibraryMediaType.Music => [MediaType.MusicAlbum, MediaType.MusicTrack, MediaType.MusicArtist],
        _ => null
    };
}
