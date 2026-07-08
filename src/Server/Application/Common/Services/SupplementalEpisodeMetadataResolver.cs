using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Common.Services;

public static class SupplementalEpisodeMetadataResolver
{
    public static async Task<ExternalEpisodeMetadata?> TryFetchTmdbEpisodeMetadataAsync(
        ISerieMetadataProvider primaryProvider,
        ISerieMetadataProvider tmdbProvider,
        Serie serie,
        int seasonNumber,
        int episodeNumber,
        string language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(primaryProvider.ProviderName, "tvdb", StringComparison.OrdinalIgnoreCase))
            return null;

        var supplementalProviderId = serie.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb")?.Value
            ?? serie.ExternalIds.FirstOrDefault(e => e.ProviderName == "imdb")?.Value;

        if (string.IsNullOrWhiteSpace(supplementalProviderId))
            return null;

        try
        {
            return await tmdbProvider.FetchEpisodeMetadataAsync(
                supplementalProviderId,
                seasonNumber,
                episodeNumber,
                language,
                cancellationToken,
                fallbackLanguage);
        }
        catch
        {
            return null;
        }
    }

    public static void MergeSupplementalExternalIds(SerieEpisode episode, IEnumerable<ExternalId>? supplementalIds)
    {
        if (episode.IsFieldLocked(nameof(SerieEpisode.ExternalIds))
            || supplementalIds is null
            || !supplementalIds.Any())
        {
            return;
        }

        foreach (var supplemental in supplementalIds)
        {
            if (string.IsNullOrWhiteSpace(supplemental.ProviderName) || string.IsNullOrWhiteSpace(supplemental.Value))
                continue;

            if (episode.ExternalIds.Any(e => e.ProviderName == supplemental.ProviderName))
                continue;

            episode.ExternalIds.Add(new ExternalId
            {
                ProviderName = supplemental.ProviderName,
                Value = supplemental.Value,
                MediaId = episode.Id
            });
        }
    }
}
