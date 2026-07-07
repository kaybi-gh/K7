using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Application.Helpers;

internal static class PersonRoleAvailabilityHelper
{
    internal static List<BasePersonRole> FilterPlayableRoles(
        IEnumerable<BasePersonRole> roles,
        IReadOnlySet<Guid>? excludedLibraryIds = null)
    {
        var playableRoles = roles
            .Where(r => r.Media is not null && CatalogMediaAvailabilityHelper.HasPlayableFiles(r.Media, excludedLibraryIds));

        var result = DedupeByMediaIdentity(playableRoles);

        foreach (var role in result)
        {
            if (role.Media is MusicArtist artist)
                TrimMusicArtistCollections(artist, excludedLibraryIds);
        }

        return result;
    }

    private static void TrimMusicArtistCollections(MusicArtist artist, IReadOnlySet<Guid>? excludedLibraryIds)
    {
        artist.Albums = artist.Albums
            .Where(a => CatalogMediaAvailabilityHelper.HasPlayableFiles(a, excludedLibraryIds))
            .ToList();

        artist.ArtistCredits = artist.ArtistCredits
            .Where(c => c.Media is MusicTrack track && (
                CatalogMediaAvailabilityHelper.HasPlayableFiles(track, excludedLibraryIds)
                || track.Album is MusicAlbum album && CatalogMediaAvailabilityHelper.HasPlayableFiles(album, excludedLibraryIds)))
            .ToList();
    }

    private static List<BasePersonRole> DedupeByMediaIdentity(IEnumerable<BasePersonRole> roles)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<BasePersonRole>();

        foreach (var role in roles)
        {
            if (seenKeys.Add(GetMediaDedupKey(role.Media!)))
                result.Add(role);
        }

        return result;
    }

    private static string GetMediaDedupKey(BaseMedia media)
    {
        var tmdbId = media.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb")?.Value;

        return media switch
        {
            SerieEpisode episode => tmdbId is not null
                ? $"serie:{tmdbId}"
                : $"serie:{episode.SerieId}",
            Serie => tmdbId is not null ? $"serie:{tmdbId}" : media.Id.ToString(),
            _ => tmdbId is not null ? $"{media.Type}:{tmdbId}" : media.Id.ToString()
        };
    }
}
