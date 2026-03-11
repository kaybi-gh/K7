using System.Threading;
using System.Reflection;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class MusicBrainzMetadataProvider : IMetadataProvider<ExternalMusicAlbumMetadata>
{
    private const string BaseUrl = "https://musicbrainz.org/ws/2";
    private const string CoverArtBaseUrl = "https://coverartarchive.org";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // MusicBrainz API restricts to 1 request per second
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    private readonly HttpClient _httpClient;
    private readonly ILogger<MusicBrainzMetadataProvider> _logger;

    public MusicBrainzMetadataProvider(HttpClient httpClient, ILogger<MusicBrainzMetadataProvider> logger)
    {
        _httpClient = httpClient;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _logger = logger;
    }

    private async Task WaitRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var requiredDelay = TimeSpan.FromSeconds(1.1); // 1.1s to be safe
            
            if (timeSinceLastRequest < requiredDelay)
            {
                await Task.Delay(requiredDelay - timeSinceLastRequest, cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<string?> SearchAsync(MediaIdentification identification, CancellationToken cancellationToken = default)
    {
        try
        {
            // Search for a release (album) by album name + artist
            var query = BuildSearchQuery(identification);
            var url = $"{BaseUrl}/release/?query={Uri.EscapeDataString(query)}&limit=5&fmt=json";

            await WaitRateLimitAsync(cancellationToken);
            var response = await _httpClient.GetFromJsonAsync<MbReleaseSearchResult>(url, JsonOptions, cancellationToken);
            var bestMatch = response?.Releases?.FirstOrDefault();

            if (bestMatch != null && bestMatch.Score >= 80)
            {
                // Prefer the release-group ID for consistency across editions
                return bestMatch.ReleaseGroup?.Id ?? bestMatch.Id;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MusicBrainz search failed for {Title}", identification.AlbumName ?? identification.Title);
            return null;
        }
    }

    public async Task<ExternalMusicAlbumMetadata> FetchMetadata(string releaseGroupId, string language, CancellationToken cancellationToken = default)
    {
        // 1. Fetch release-group for genres/tags
        var releaseGroup = await GetReleaseGroupAsync(releaseGroupId, cancellationToken);

        // 2. Find the best release within the group (prefer the one with most tracks)
        var releaseId = await FindBestReleaseIdAsync(releaseGroupId, cancellationToken);

        // 3. Fetch the full release with recordings
        var release = releaseId != null
            ? await GetReleaseAsync(releaseId, cancellationToken)
            : null;

        // 4. Build metadata
        var metadata = new ExternalMusicAlbumMetadata
        {
            Title = releaseGroup?.Title ?? release?.Title,
            ReleaseDate = ParseDate(releaseGroup?.FirstReleaseDate ?? release?.Date),
            Genres = ExtractGenres(releaseGroup?.Genres),
            ExternalIds =
            [
                new ExternalId { Platform = "musicbrainz-release-group", Value = releaseGroupId }
            ],
            Tracks = ExtractTracks(release),
            Pictures = await FetchCoverArtAsync(releaseGroupId, releaseId, cancellationToken)
        };

        if (releaseId != null)
        {
            metadata.ExternalIds.Add(new ExternalId { Platform = "musicbrainz-release", Value = releaseId });
        }

        return metadata;
    }

    private static string BuildSearchQuery(MediaIdentification identification)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(identification.AlbumName))
        {
            parts.Add($"release:\"{identification.AlbumName}\"");
        }
        else if (!string.IsNullOrEmpty(identification.Title))
        {
            parts.Add($"release:\"{identification.Title}\"");
        }

        if (!string.IsNullOrEmpty(identification.ArtistName))
        {
            parts.Add($"artist:\"{identification.ArtistName}\"");
        }

        if (identification.ReleaseYear.HasValue)
        {
            parts.Add($"date:{identification.ReleaseYear.Value.Year}");
        }

        return string.Join(" AND ", parts);
    }

    private async Task<MbReleaseGroup?> GetReleaseGroupAsync(string releaseGroupId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/release-group/{releaseGroupId}?inc=genres+tags&fmt=json";
            await WaitRateLimitAsync(cancellationToken);
            return await _httpClient.GetFromJsonAsync<MbReleaseGroup>(url, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch MusicBrainz release-group {Id}", releaseGroupId);
            return null;
        }
    }

    private async Task<string?> FindBestReleaseIdAsync(string releaseGroupId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/release?release-group={releaseGroupId}&inc=media&fmt=json&limit=10";
            await WaitRateLimitAsync(cancellationToken);
            var result = await _httpClient.GetFromJsonAsync<MbReleaseList>(url, JsonOptions, cancellationToken);

            // Prefer official releases, then by most tracks
            return result?.Releases?
                .OrderByDescending(r => r.Status == "Official" ? 1 : 0)
                .ThenByDescending(r => r.Media?.Sum(m => m.TrackCount) ?? 0)
                .FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find best release for release-group {Id}", releaseGroupId);
            return null;
        }
    }

    private async Task<MbRelease?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/release/{releaseId}?inc=recordings+artist-credits&fmt=json";
            await WaitRateLimitAsync(cancellationToken);
            return await _httpClient.GetFromJsonAsync<MbRelease>(url, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch MusicBrainz release {Id}", releaseId);
            return null;
        }
    }

    private async Task<List<MetadataPicture>> FetchCoverArtAsync(string releaseGroupId, string? releaseId, CancellationToken cancellationToken)
    {
        var pictures = new List<MetadataPicture>();

        // Try release-group cover first, then individual release
        var coverUrl = await TryGetCoverArtUrl($"{CoverArtBaseUrl}/release-group/{releaseGroupId}", cancellationToken)
                    ?? (releaseId != null ? await TryGetCoverArtUrl($"{CoverArtBaseUrl}/release/{releaseId}", cancellationToken) : null);

        if (coverUrl != null)
        {
            var picture = new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                OriginalRemoteUri = new Uri(coverUrl)
            };
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            pictures.Add(picture);
        }

        return pictures;
    }

    private async Task<string?> TryGetCoverArtUrl(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/front-500", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response.RequestMessage?.RequestUri?.ToString();
            }
        }
        catch
        {
            // Cover art not available — not an error
        }
        return null;
    }

    private static IList<string> ExtractGenres(List<MbGenre>? genres)
    {
        if (genres == null || genres.Count == 0) return [];
        return genres
            .OrderByDescending(g => g.Count)
            .Select(g => g.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList()!;
    }

    private static IList<ExternalMusicTrackMetadata> ExtractTracks(MbRelease? release)
    {
        if (release?.Media == null) return [];

        var tracks = new List<ExternalMusicTrackMetadata>();
        foreach (var medium in release.Media.OrderBy(m => m.Position))
        {
            if (medium.Tracks == null) continue;
            foreach (var track in medium.Tracks.OrderBy(t => t.Position))
            {
                tracks.Add(new ExternalMusicTrackMetadata
                {
                    Title = track.Title ?? track.Recording?.Title ?? "Unknown",
                    TrackNumber = track.Position,
                    DiscNumber = medium.Position,
                    Duration = track.Length.HasValue ? TimeSpan.FromMilliseconds(track.Length.Value) : null,
                    MusicBrainzRecordingId = track.Recording?.Id
                });
            }
        }
        return tracks;
    }

    private static DateOnly? ParseDate(string? date)
    {
        if (string.IsNullOrEmpty(date)) return null;

        // MusicBrainz dates can be "2001", "2001-03", or "2001-03-12"
        if (DateOnly.TryParse(date, out var full)) return full;
        if (date.Length == 4 && int.TryParse(date, out var year)) return new DateOnly(year, 1, 1);
        if (date.Length == 7 && int.TryParse(date[..4], out var y) && int.TryParse(date[5..7], out var m))
            return new DateOnly(y, m, 1);

        return null;
    }

    #region MusicBrainz API DTOs

    private record MbReleaseSearchResult
    {
        public List<MbReleaseSearchEntry>? Releases { get; init; }
    }

    private record MbReleaseSearchEntry
    {
        public string Id { get; init; } = "";
        public int Score { get; init; }
        public string? Title { get; init; }
        [JsonPropertyName("release-group")]
        public MbReleaseGroupRef? ReleaseGroup { get; init; }
    }

    private record MbReleaseGroupRef
    {
        public string Id { get; init; } = "";
    }

    private record MbReleaseGroup
    {
        public string Id { get; init; } = "";
        public string? Title { get; init; }
        [JsonPropertyName("first-release-date")]
        public string? FirstReleaseDate { get; init; }
        public List<MbGenre>? Genres { get; init; }
    }

    private record MbGenre
    {
        public string? Name { get; init; }
        public int Count { get; init; }
    }

    private record MbReleaseList
    {
        public List<MbReleaseSummary>? Releases { get; init; }
    }

    private record MbReleaseSummary
    {
        public string Id { get; init; } = "";
        public string? Status { get; init; }
        public List<MbMedium>? Media { get; init; }
    }

    private record MbRelease
    {
        public string Id { get; init; } = "";
        public string? Title { get; init; }
        public string? Date { get; init; }
        public List<MbMedium>? Media { get; init; }
    }

    private record MbMedium
    {
        public int Position { get; init; }
        [JsonPropertyName("track-count")]
        public int TrackCount { get; init; }
        public List<MbTrack>? Tracks { get; init; }
    }

    private record MbTrack
    {
        public string? Title { get; init; }
        public int Position { get; init; }
        public long? Length { get; init; }
        public MbRecording? Recording { get; init; }
    }

    private record MbRecording
    {
        public string Id { get; init; } = "";
        public string? Title { get; init; }
    }

    #endregion
}
