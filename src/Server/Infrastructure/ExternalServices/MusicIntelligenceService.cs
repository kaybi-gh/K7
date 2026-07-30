using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices;

public class MusicIntelligenceService(
    AudioMuseMusicIntelligenceAdapter adapter,
    MusicIntelligenceHealthMonitor healthMonitor,
    IMemoryCache cache,
    ILogger<MusicIntelligenceService> logger) : IMusicIntelligenceService
{
    private static readonly TimeSpan SimilarTracksCacheDuration = TimeSpan.FromMinutes(30);

    public async Task<MusicIntelligenceConnectionResult> TestConnectionAsync(
        MusicIntelligenceSettingsDto? draftSettings = null,
        CancellationToken cancellationToken = default)
    {
        var result = await adapter.TestConnectionAsync(draftSettings, cancellationToken);
        if (result.Success)
            healthMonitor.MarkReachable();
        else
            healthMonitor.MarkUnreachable();
        return result;
    }

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => adapter.IsConfiguredAndEnabledAsync(cancellationToken);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return false;

        return await healthMonitor.GetReachableAsync(
            async ct => (await adapter.TestConnectionAsync(cancellationToken: ct)).Success,
            cancellationToken);
    }

    public async Task<MusicIntelligenceStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await adapter.GetSettingsAsync(cancellationToken);
        var isEnabled = settings is { Enabled: true } && !string.IsNullOrWhiteSpace(settings.BaseUrl);
        var instantEnabled = settings?.InstantPlaylistEnabled ?? false;

        // Instant playlist visibility is settings-only (no AudioMuse probe for LLM provider).
        var instantAvailable = isEnabled && instantEnabled;

        var isAvailable = isEnabled
            && await healthMonitor.GetReachableAsync(
                async ct => (await adapter.TestConnectionAsync(cancellationToken: ct)).Success,
                cancellationToken);

        return new MusicIntelligenceStatusDto
        {
            IsEnabled = isEnabled,
            IsAvailable = isAvailable,
            InstantPlaylistEnabled = instantEnabled,
            InstantPlaylistAvailable = instantAvailable
        };
    }

    public async Task<List<MusicIntelligenceTrackMatchDto>> GetSimilarTracksAsync(
        Guid trackId,
        int count = 20,
        string? title = null,
        string? artist = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return [];

        var cacheKey = $"mi:similar:{trackId}:{count}";
        if (cache.TryGetValue(cacheKey, out List<MusicIntelligenceTrackMatchDto>? cached) && cached is { Count: > 0 })
            return cached;

        try
        {
            var matches = await adapter.GetSimilarTracksAsync(trackId, count, title, artist, cancellationToken);
            if (matches.Count > 0)
            {
                healthMonitor.MarkReachable();
                cache.SetWithSize(cacheKey, matches, SimilarTracksCacheDuration);
            }

            return matches;
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
            healthMonitor.MarkUnreachable();
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
            healthMonitor.MarkUnreachable();
            logger.LogWarning(ex, "Failed to get mood tracks for {MoodKey} centroid {CentroidIndex}", moodKey, centroidIndex);
            return [];
        }
    }

    public async Task<List<Guid>> GetDiscoveryTracksAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return [];

        try
        {
            var ids = await adapter.GetDiscoveryTracksAsync(count, cancellationToken);
            if (ids.Count > 0)
                healthMonitor.MarkReachable();
            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get discovery tracks");
            return [];
        }
    }

    public async Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return [];

        try
        {
            var ids = await adapter.GetSonicPathAsync(fromId, toId, cancellationToken);
            if (ids.Count > 0)
                healthMonitor.MarkReachable();
            return ids;
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

        var settings = await adapter.GetSettingsAsync(cancellationToken);
        if (settings is not { InstantPlaylistEnabled: true })
            return [];

        try
        {
            return await adapter.CreatePlaylistFromPromptAsync(prompt, count, cancellationToken);
        }
        catch (Exception ex)
        {
            healthMonitor.MarkUnreachable();
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
            if (matches.Count > 0)
                cache.SetWithSize(cacheKey, matches, SimilarTracksCacheDuration);
            return matches;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get similar artists for {ArtistId}", artistId);
            return [];
        }
    }

    public async Task<List<Guid>> SearchTracksBySonicTextAsync(string query, int count = 50, CancellationToken cancellationToken = default)
    {
        // User-triggered search should not be blocked by a stale "unreachable" cache.
        if (!await IsEnabledAsync(cancellationToken))
            return [];

        var cacheKey = $"mi:search-sonic:{query}:{count}";
        if (cache.TryGetValue(cacheKey, out List<Guid>? cached) && cached is { Count: > 0 })
            return cached;

        try
        {
            var ids = await adapter.SearchTracksBySonicTextAsync(query, count, cancellationToken);
            if (ids.Count > 0)
            {
                healthMonitor.MarkReachable();
                cache.SetWithSize(cacheKey, ids, SimilarTracksCacheDuration);
            }

            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search tracks by sonic text");
            return [];
        }
    }

    public async Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count = 50, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return [];

        var cacheKey = $"mi:search-lyrics:{query}:{count}";
        if (cache.TryGetValue(cacheKey, out List<Guid>? cached) && cached is { Count: > 0 })
            return cached;

        try
        {
            var ids = await adapter.SearchTracksByLyricsAsync(query, count, cancellationToken);
            if (ids.Count > 0)
            {
                healthMonitor.MarkReachable();
                cache.SetWithSize(cacheKey, ids, SimilarTracksCacheDuration);
            }

            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search tracks by lyrics");
            return [];
        }
    }
}
