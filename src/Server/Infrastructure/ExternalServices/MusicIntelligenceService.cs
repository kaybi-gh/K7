using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices;

public class MusicIntelligenceService(
    AudioMuseMusicIntelligenceAdapter adapter,
    IMemoryCache cache,
    ILogger<MusicIntelligenceService> logger) : IMusicIntelligenceService
{
    private static readonly TimeSpan SimilarTracksCacheDuration = TimeSpan.FromMinutes(30);

    public Task<MusicIntelligenceConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
        => adapter.TestConnectionAsync(cancellationToken);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => adapter.IsConfiguredAndEnabledAsync(cancellationToken);

    public async Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        var cacheKey = $"mi:similar:{trackId}:{count}";
        if (cache.TryGetValue(cacheKey, out List<Guid>? cached) && cached is not null)
            return cached;

        try
        {
            var ids = await adapter.GetSimilarTracksAsync(trackId, count, cancellationToken);
            cache.Set(cacheKey, ids, SimilarTracksCacheDuration);
            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get similar tracks for {TrackId}", trackId);
            return [];
        }
    }

    public async Task<IReadOnlyList<MusicMoodPresetDto>> GetMoodPresetsAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        try
        {
            return await adapter.GetMoodPresetsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get mood presets");
            return [];
        }
    }

    public async Task<List<Guid>> GetMoodTracksAsync(string moodKey, int centroidIndex, int count = 50, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        try
        {
            return await adapter.GetMoodTracksAsync(moodKey, centroidIndex, count, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get mood tracks for {MoodKey} centroid {CentroidIndex}", moodKey, centroidIndex);
            return [];
        }
    }

    public async Task<List<Guid>> GetDiscoveryTracksAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        try
        {
            return await adapter.GetDiscoveryTracksAsync(count, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get discovery tracks");
            return [];
        }
    }

    public async Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        try
        {
            return await adapter.GetSonicPathAsync(fromId, toId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get sonic path from {FromId} to {ToId}", fromId, toId);
            return [];
        }
    }

    public async Task<List<Guid>> CreatePlaylistFromPromptAsync(string prompt, int count = 30, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        try
        {
            return await adapter.CreatePlaylistFromPromptAsync(prompt, count, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create playlist from prompt");
            return [];
        }
    }

    public async Task<IReadOnlyList<MusicSimilarArtistMatchDto>> GetSimilarArtistsAsync(
        Guid artistId,
        string? artistName,
        int count = 12,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
            return [];

        var cacheKey = $"mi:similar-artists:{artistId}:{count}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<MusicSimilarArtistMatchDto>? cached) && cached is not null)
            return cached;

        try
        {
            var matches = await adapter.GetSimilarArtistsAsync(artistId, artistName, count, cancellationToken);
            cache.Set(cacheKey, matches, SimilarTracksCacheDuration);
            return matches;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get similar artists for {ArtistId}", artistId);
            return [];
        }
    }
}
