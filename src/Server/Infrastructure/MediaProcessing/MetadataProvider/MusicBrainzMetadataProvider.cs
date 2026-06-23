using System.Reflection;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class MusicBrainzMetadataProvider : IMetadataProvider<ExternalMusicAlbumMetadata>, IMusicArtistMetadataProvider, IMetadataProviderInfo, IMetadataImageProvider
{
    private const string BaseUrl = "https://musicbrainz.org/ws/2";
    private const string CoverArtBaseUrl = "https://coverartarchive.org";
    private const string Host = "musicbrainz.org";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OutboundRateLimiter _rateLimiter;
    private readonly ILogger<MusicBrainzMetadataProvider> _logger;

    public MusicBrainzMetadataProvider(HttpClient httpClient, OutboundRateLimiter rateLimiter, ILogger<MusicBrainzMetadataProvider> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _logger = logger;
    }

    public string ProviderName => "musicbrainz";
    public IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; } = [LibraryMediaType.Music];

    public async Task<string?> SearchAsync(MediaIdentification identification, CancellationToken cancellationToken = default)
    {
        try
        {
            // Search for a release (album) by album name + artist
            var query = BuildSearchQuery(identification);
            var url = $"{BaseUrl}/release/?query={Uri.EscapeDataString(query)}&limit=5&fmt=json";

            await _rateLimiter.WaitAsync(Host, cancellationToken);
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
        var externalIds = new List<ExternalId>
        {
            new() { ProviderName = "musicbrainz", Value = releaseGroupId }
        };

        if (releaseGroup?.Relations is not null)
        {
            var wikidataUrl = releaseGroup.Relations.FirstOrDefault(r => r.Type == "wikidata")?.Url?.Resource;
            if (!string.IsNullOrEmpty(wikidataUrl))
            {
                var qid = ExtractQid(wikidataUrl);
                if (qid is not null)
                    externalIds.Add(new ExternalId { ProviderName = "wikidata", Value = qid });
            }

            var spotifyUrl = releaseGroup.Relations
                .FirstOrDefault(r => r.Type == "streaming music" && r.Url?.Resource?.Contains("spotify.com") == true)?.Url?.Resource;
            if (!string.IsNullOrEmpty(spotifyUrl))
            {
                var spotifyId = ExtractSpotifyId(spotifyUrl);
                if (spotifyId is not null)
                    externalIds.Add(new ExternalId { ProviderName = "spotify", Value = spotifyId });
            }
        }

        var metadata = new ExternalMusicAlbumMetadata
        {
            Title = releaseGroup?.Title ?? release?.Title,
            ReleaseDate = ParseDate(releaseGroup?.FirstReleaseDate ?? release?.Date),
            Genres = ExtractGenreTags(releaseGroup?.Genres, releaseGroup?.Tags),
            ExternalIds = externalIds,
            Tracks = ExtractTracks(release),
            Artists = ExtractArtists(release),
            Pictures = await FetchCoverArtAsync(releaseGroupId, releaseId, cancellationToken)
        };

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
            var url = $"{BaseUrl}/release-group/{releaseGroupId}?inc=genres+tags+url-rels&fmt=json";
            await _rateLimiter.WaitAsync(Host, cancellationToken);
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
            await _rateLimiter.WaitAsync(Host, cancellationToken);
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
            var url = $"{BaseUrl}/release/{releaseId}?inc=recordings+artist-credits+isrcs&fmt=json";
            await _rateLimiter.WaitAsync(Host, cancellationToken);
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
                Type = MetadataPictureType.Cover,
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
            // Cover art not available - not an error
        }
        return null;
    }

    private static IList<string> ExtractGenreTags(List<MbGenre>? genres, List<MbGenre>? tags)
    {
        if (genres is { Count: > 0 })
            return OrderGenreLikeValues(genres);

        if (tags is { Count: > 0 })
        {
            return tags
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .OrderByDescending(t => t.Count)
                .Take(8)
                .Select(t => t.Name!)
                .ToList();
        }

        return [];
    }

    private static IList<string> OrderGenreLikeValues(List<MbGenre> values) =>
        values
            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
            .OrderByDescending(g => g.Count)
            .Select(g => g.Name!)
            .ToList();

    private static IList<ExternalMusicTrackMetadata> ExtractTracks(MbRelease? release)
    {
        if (release?.Media == null) return [];

        // Collect album-level artist IDs to determine guest status
        var albumArtistIds = (release.ArtistCredit ?? [])
            .Where(ac => ac.Artist is not null)
            .Select(ac => ac.Artist!.Id)
            .ToHashSet();

        var tracks = new List<ExternalMusicTrackMetadata>();
        foreach (var medium in release.Media.OrderBy(m => m.Position))
        {
            if (medium.Tracks == null) continue;
            foreach (var track in medium.Tracks.OrderBy(t => t.Position))
            {
                var credits = (track.ArtistCredit ?? [])
                    .Where(ac => ac.Artist is not null && !string.IsNullOrEmpty(ac.Artist.Id))
                    .Select(ac => new ExternalMusicTrackArtistCredit
                    {
                        Name = ac.Artist!.Name ?? ac.Name ?? "Unknown",
                        MusicBrainzArtistId = ac.Artist.Id,
                        IsGuest = !albumArtistIds.Contains(ac.Artist.Id)
                    })
                    .ToList();

                tracks.Add(new ExternalMusicTrackMetadata
                {
                    Title = track.Title ?? track.Recording?.Title ?? "Unknown",
                    TrackNumber = track.Position,
                    DiscNumber = medium.Position,
                    Duration = track.Length.HasValue ? TimeSpan.FromMilliseconds(track.Length.Value) : null,
                    MusicBrainzRecordingId = track.Recording?.Id,
                    Isrc = track.Recording?.Isrcs?.FirstOrDefault(),
                    ArtistCredits = credits
                });
            }
        }
        return tracks;
    }

    private static IList<ExternalMusicArtistMetadata> ExtractArtists(MbRelease? release)
    {
        if (release?.ArtistCredit == null) return [];

        return release.ArtistCredit
            .Where(ac => ac.Artist != null && !string.IsNullOrEmpty(ac.Artist.Id))
            .Select(ac => new ExternalMusicArtistMetadata
            {
                Name = ac.Artist!.Name ?? ac.Name ?? "Unknown",
                MusicBrainzArtistId = ac.Artist.Id
            })
            .DistinctBy(a => a.MusicBrainzArtistId)
            .ToList();
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

    public async Task<ExternalMusicArtistDetails?> FetchByProviderIdAsync(
        string providerId, string language, CancellationToken cancellationToken = default)
    {
        try
        {
            var artist = await FetchArtistAsync(providerId, cancellationToken);
            if (artist == null) return null;

            var country = artist.Area?.Name;
            var wikidataUrl = artist.Relations?
                .FirstOrDefault(r => r.Type == "wikidata")?.Url?.Resource;
            var wikidataId = !string.IsNullOrEmpty(wikidataUrl) ? ExtractQid(wikidataUrl) : null;

            var spotifyUrl = artist.Relations?
                .FirstOrDefault(r => r.Type == "streaming music" && r.Url?.Resource?.Contains("spotify.com") == true)?.Url?.Resource;
            var spotifyId = !string.IsNullOrEmpty(spotifyUrl) ? ExtractSpotifyId(spotifyUrl) : null;

            var imdbUrl = artist.Relations?
                .FirstOrDefault(r => r.Type == "IMDb")?.Url?.Resource;
            var imdbId = !string.IsNullOrEmpty(imdbUrl) ? ExtractImdbId(imdbUrl) : null;

            var imageUrl = await TryGetArtistImageUrlAsync(providerId, cancellationToken);

            var members = artist.Relations?
                .Where(r => r.Type == "member of band" && r.Direction == "backward" && r.Artist is not null)
                .Select(r => new ExternalMusicArtistMember
                {
                    Name = r.Artist!.Name ?? "Unknown",
                    MusicBrainzArtistId = r.Artist.Id,
                    Role = r.Attributes is { Count: > 0 } ? string.Join(", ", r.Attributes) : null,
                    IsActive = r.Ended is not true
                })
                .ToList();

            // Solo artist: link the artist itself as a member
            if (artist.Type == "Person" && members is not { Count: > 0 })
            {
                members =
                [
                    new ExternalMusicArtistMember
                    {
                        Name = artist.Name ?? "Unknown",
                        MusicBrainzArtistId = providerId,
                        IsActive = true
                    }
                ];
            }

            return new ExternalMusicArtistDetails
            {
                Country = country,
                MusicBrainzArtistId = providerId,
                WikidataId = wikidataId,
                SpotifyId = spotifyId,
                ImdbId = imdbId,
                ImageUrl = imageUrl,
                Members = members
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MusicBrainz artist fetch failed for {Id}", providerId);
            return null;
        }
    }

    public async Task<ExternalMusicArtistDetails?> SearchByNameAsync(
        string artistName, string language, CancellationToken cancellationToken = default)
    {
        try
        {
            await _rateLimiter.WaitAsync(Host, cancellationToken);
            var url = $"{BaseUrl}/artist/?query=artist:\"{Uri.EscapeDataString(artistName)}\"&limit=1&fmt=json";
            var result = await _httpClient.GetFromJsonAsync<MbArtistSearchResult>(url, JsonOptions, cancellationToken);
            var best = result?.Artists?.FirstOrDefault();
            if (best is not { Score: >= 90 }) return null;

            return await FetchByProviderIdAsync(best.Id, language, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MusicBrainz artist search failed for {Name}", artistName);
            return null;
        }
    }

    private async Task<MbArtistDetail?> FetchArtistAsync(string mbid, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(Host, ct);
        var url = $"{BaseUrl}/artist/{Uri.EscapeDataString(mbid)}?inc=url-rels+artist-rels&fmt=json";
        return await _httpClient.GetFromJsonAsync<MbArtistDetail>(url, JsonOptions, ct);
    }

    private async Task<string?> TryGetArtistImageUrlAsync(string artistMbid, CancellationToken cancellationToken)
    {
        try
        {
            await _rateLimiter.WaitAsync(Host, cancellationToken);
            var url = $"{BaseUrl}/release-group?artist={Uri.EscapeDataString(artistMbid)}&type=album&limit=1&fmt=json";
            var result = await _httpClient.GetFromJsonAsync<MbReleaseGroupSearchResult>(url, JsonOptions, cancellationToken);
            var releaseGroupId = result?.ReleaseGroups?.FirstOrDefault()?.Id;

            if (string.IsNullOrEmpty(releaseGroupId)) return null;

            return await TryGetCoverArtUrl($"{CoverArtBaseUrl}/release-group/{releaseGroupId}", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractQid(string wikidataUrl)
    {
        var idx = wikidataUrl.LastIndexOf('/');
        if (idx < 0 || idx == wikidataUrl.Length - 1) return null;
        var qid = wikidataUrl[(idx + 1)..];
        return qid.StartsWith('Q') ? qid : null;
    }

    private static string? ExtractSpotifyId(string spotifyUrl)
    {
        // e.g. https://open.spotify.com/artist/4Z8W4fKeB5YxbusRsdQVPb
        var segments = new Uri(spotifyUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[^1] : null;
    }

    private static string? ExtractImdbId(string imdbUrl)
    {
        // e.g. https://www.imdb.com/name/nm0000093/
        var segments = new Uri(imdbUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[^1] : null;
    }

    public bool SupportsMediaType(MediaType mediaType) => mediaType is MediaType.MusicAlbum or MediaType.MusicArtist;

    public async Task<IReadOnlyList<ProviderImageDto>> GetImagesAsync(ImageProviderContext context, CancellationToken cancellationToken = default)
    {
        return context.MediaType switch
        {
            MediaType.MusicAlbum => await FetchReleaseGroupImagesAsync(context.ProviderId, cancellationToken),
            MediaType.MusicArtist => await FetchArtistImagesAsync(context.ProviderId, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<ProviderImageDto>> FetchReleaseGroupImagesAsync(string releaseGroupId, CancellationToken cancellationToken)
    {
        var results = new List<ProviderImageDto>();

        try
        {
            var coverArtUrl = $"{CoverArtBaseUrl}/release-group/{releaseGroupId}";
            await _rateLimiter.WaitAsync(Host, cancellationToken);
            var response = await _httpClient.GetAsync(coverArtUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return results;

            var coverArt = await response.Content.ReadFromJsonAsync<CoverArtResponse>(JsonOptions, cancellationToken);
            if (coverArt?.Images is null)
                return results;

            AddCoverArtImages(results, coverArt.Images);
        }
        catch
        {
            // Cover Art Archive unavailable
        }

        return results;
    }

    private async Task<IReadOnlyList<ProviderImageDto>> FetchArtistImagesAsync(string artistMbid, CancellationToken cancellationToken)
    {
        var results = new List<ProviderImageDto>();

        try
        {
            await _rateLimiter.WaitAsync(Host, cancellationToken);
            var url = $"{BaseUrl}/release-group?artist={Uri.EscapeDataString(artistMbid)}&type=album&limit=10&fmt=json";
            var searchResult = await _httpClient.GetFromJsonAsync<MbReleaseGroupSearchResult>(url, JsonOptions, cancellationToken);

            if (searchResult?.ReleaseGroups is null)
                return results;

            foreach (var rg in searchResult.ReleaseGroups)
            {
                if (string.IsNullOrEmpty(rg.Id)) continue;

                try
                {
                    var coverArtUrl = $"{CoverArtBaseUrl}/release-group/{rg.Id}";
                    await _rateLimiter.WaitAsync(Host, cancellationToken);
                    var response = await _httpClient.GetAsync(coverArtUrl, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                        continue;

                    var coverArt = await response.Content.ReadFromJsonAsync<CoverArtResponse>(JsonOptions, cancellationToken);
                    if (coverArt?.Images is null)
                        continue;

                    AddCoverArtImages(results, coverArt.Images);
                }
                catch
                {
                    // Skip unavailable cover art
                }
            }
        }
        catch
        {
            // MusicBrainz or Cover Art Archive unavailable
        }

        return results;
    }

    private static void AddCoverArtImages(List<ProviderImageDto> results, List<CoverArtImage> images)
    {
        foreach (var img in images)
        {
            if (string.IsNullOrEmpty(img.Image)) continue;

            var isFront = img.Front == true;
            var thumbUrl = img.Thumbnails?.Large ?? img.Thumbnails?.Small ?? img.Image;

            results.Add(new ProviderImageDto
            {
                Url = img.Image,
                ThumbnailUrl = thumbUrl,
                Type = MetadataPictureType.Cover,
                Width = 0,
                Height = 0,
                VoteAverage = isFront ? 10 : 0,
                Language = null
            });
        }
    }

    private record CoverArtResponse
    {
        public List<CoverArtImage>? Images { get; init; }
    }

    private record CoverArtImage
    {
        public string? Image { get; init; }
        public bool? Front { get; init; }
        public bool? Back { get; init; }
        public CoverArtThumbnails? Thumbnails { get; init; }
    }

    private record CoverArtThumbnails
    {
        public string? Small { get; init; }
        public string? Large { get; init; }
        [JsonPropertyName("1200")]
        public string? ExtraLarge { get; init; }
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
        public List<MbGenre>? Tags { get; init; }
        public List<MbRelation>? Relations { get; init; }
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
        [JsonPropertyName("artist-credit")]
        public List<MbArtistCredit>? ArtistCredit { get; init; }
    }

    private record MbArtistCredit
    {
        public string? Name { get; init; }
        public MbArtist? Artist { get; init; }
    }

    private record MbArtist
    {
        public string Id { get; init; } = "";
        public string? Name { get; init; }
        public string? Type { get; init; }
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
        [JsonPropertyName("artist-credit")]
        public List<MbArtistCredit>? ArtistCredit { get; init; }
    }

    private record MbRecording
    {
        public string Id { get; init; } = "";
        public string? Title { get; init; }
        public List<string>? Isrcs { get; init; }
    }

    private record MbArtistSearchResult
    {
        public List<MbArtistSearchEntry>? Artists { get; init; }
    }

    private record MbArtistSearchEntry
    {
        public string Id { get; init; } = "";
        public int Score { get; init; }
    }

    private record MbArtistDetail
    {
        public string? Name { get; init; }
        public string? Type { get; init; }
        public MbArea? Area { get; init; }
        public List<MbRelation>? Relations { get; init; }
    }

    private record MbReleaseGroupSearchResult
    {
        [JsonPropertyName("release-groups")]
        public List<MbReleaseGroupEntry>? ReleaseGroups { get; init; }
    }

    private record MbReleaseGroupEntry
    {
        public string Id { get; init; } = "";
    }

    private record MbArea
    {
        public string? Name { get; init; }
    }

    private record MbRelation
    {
        public string? Type { get; init; }
        public string? Direction { get; init; }
        public bool? Ended { get; init; }
        public List<string>? Attributes { get; init; }
        public MbUrl? Url { get; init; }
        public MbArtist? Artist { get; init; }
    }

    private record MbUrl
    {
        public string? Resource { get; init; }
    }

    #endregion
}
