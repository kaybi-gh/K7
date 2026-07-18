using K7.Server.Application.Common.Extensions;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TMDbLib.Client;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class TMDbPersonCreditsProvider(
    TMDbClient tmdbClient,
    IMemoryCache cache,
    ILogger<TMDbPersonCreditsProvider> logger) : IPersonCreditsProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<ExternalPersonCredit>> GetPersonCreditsAsync(
        string providerId, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(tmdbClient, cancellationToken);
        var cacheKey = $"person-credits:tmdb:{providerId}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ExternalPersonCredit>? cached) && cached is not null)
            return cached;

        if (!int.TryParse(providerId, out var tmdbId))
            return [];

        try
        {
            var credits = await tmdbClient.GetPersonMovieCreditsAsync(tmdbId, cancellationToken: cancellationToken);
            var tvCredits = await tmdbClient.GetPersonTvCreditsAsync(tmdbId, cancellationToken: cancellationToken);

            var results = new List<ExternalPersonCredit>();

            if (credits?.Cast is not null)
            {
                foreach (var role in credits.Cast)
                {
                    results.Add(new ExternalPersonCredit
                    {
                        ExternalId = role.Id.ToString(),
                        Title = role.Title ?? role.OriginalTitle ?? "Unknown",
                        Year = role.ReleaseDate?.Year,
                        MediaType = "movie",
                        PosterPath = role.PosterPath is not null
                            ? tmdbClient.GetImageUrl("w342", role.PosterPath, true)?.ToString()
                            : null,
                        Popularity = role.Popularity
                    });
                }
            }

            if (tvCredits?.Cast is not null)
            {
                foreach (var role in tvCredits.Cast)
                {
                    results.Add(new ExternalPersonCredit
                    {
                        ExternalId = role.Id.ToString(),
                        Title = role.Name ?? role.OriginalName ?? "Unknown",
                        Year = role.FirstAirDate?.Year,
                        MediaType = "tv",
                        PosterPath = role.PosterPath is not null
                            ? tmdbClient.GetImageUrl("w342", role.PosterPath, true)?.ToString()
                            : null,
                        Popularity = role.EpisodeCount
                    });
                }
            }

            var sorted = results
                .OrderByDescending(r => r.Popularity)
                .Take(30)
                .ToList();

            cache.SetWithSize(cacheKey, (IReadOnlyList<ExternalPersonCredit>)sorted, CacheDuration);
            return sorted;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch TMDB credits for person {ProviderId}", providerId);
            return [];
        }
    }
}
