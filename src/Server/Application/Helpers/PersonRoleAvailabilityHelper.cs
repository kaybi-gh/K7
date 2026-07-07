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

        return DedupeByMediaIdentity(playableRoles);
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
