using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

public static class MetadataProviderPageUrlHelper
{
    /// <summary>
    /// Public catalog page for a metadata search hit, when the provider exposes one.
    /// </summary>
    public static string? TryBuild(string? provider, string? externalId, MediaType? mediaType = null)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalId))
            return null;

        var id = externalId.Trim();
        var normalized = provider.Trim().ToLowerInvariant();

        return normalized switch
        {
            "musicbrainz" => $"https://musicbrainz.org/release-group/{id}",
            "tmdb" when IsSerieLike(mediaType) => $"https://www.themoviedb.org/tv/{id}",
            "tmdb" => $"https://www.themoviedb.org/movie/{id}",
            "tvdb" => $"https://www.thetvdb.com/dereferrer/series/{id}",
            _ => null
        };
    }

    private static bool IsSerieLike(MediaType? mediaType) =>
        mediaType is MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;
}
