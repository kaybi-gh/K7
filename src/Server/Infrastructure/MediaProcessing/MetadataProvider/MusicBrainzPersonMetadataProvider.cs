using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class MusicBrainzPersonMetadataProvider : IPersonMetadataProvider, IPersonImageProvider
{
    private const string MusicBrainzBaseUrl = "https://musicbrainz.org/ws/2";
    private const string CoverArtBaseUrl = "https://coverartarchive.org";
    private const string WikidataBaseUrl = "https://www.wikidata.org/w/api.php";
    private const string Host = "musicbrainz.org";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OutboundRateLimiter _rateLimiter;
    private readonly ILogger<MusicBrainzPersonMetadataProvider> _logger;

    public MusicBrainzPersonMetadataProvider(
        HttpClient httpClient,
        OutboundRateLimiter rateLimiter,
        ILogger<MusicBrainzPersonMetadataProvider> logger)
    {
        _httpClient = httpClient;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public string ProviderName => "musicbrainz";

    public async Task<ExternalPersonDetails?> FetchPersonAsync(
        string providerId, string language, CancellationToken cancellationToken = default)
    {
        try
        {
            var artist = await FetchArtistAsync(providerId, cancellationToken);
            if (artist is null) return null;

            var gender = MapGender(artist.Gender);
            var birthday = ParseDate(artist.LifeSpan?.Begin);
            var deathday = ParseDate(artist.LifeSpan?.End);
            var birthPlace = artist.BeginArea?.Name ?? artist.Area?.Name;

            // Try to get portrait image from Wikidata
            string? imageUrl = null;
            var wikidataUrl = artist.Relations?
                .FirstOrDefault(r => r.Type == "wikidata")?.Url?.Resource;
            if (!string.IsNullOrEmpty(wikidataUrl))
            {
                var qid = ExtractQid(wikidataUrl);
                if (qid is not null)
                    imageUrl = await FetchWikidataImageAsync(qid, cancellationToken);
            }

            // Get biography from Wikipedia via Wikidata sitelinks
            string? biography = null;
            if (!string.IsNullOrEmpty(wikidataUrl))
            {
                var qid = ExtractQid(wikidataUrl);
                if (qid is not null)
                    biography = await FetchWikipediaBiographyAsync(qid, language, cancellationToken);
            }

            var additionalIds = ExtractExternalIds(artist.Relations, wikidataUrl);

            return new ExternalPersonDetails
            {
                Birthday = birthday,
                Deathday = deathday,
                BirthPlace = birthPlace,
                Gender = gender,
                ImageUrl = imageUrl,
                Biography = biography,
                AdditionalExternalIds = additionalIds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MusicBrainz person fetch failed for {Id}", providerId);
            return null;
        }
    }

    private async Task<MbPersonArtist?> FetchArtistAsync(string mbid, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(Host, ct);
        var url = $"{MusicBrainzBaseUrl}/artist/{Uri.EscapeDataString(mbid)}?inc=url-rels&fmt=json";
        return await _httpClient.GetFromJsonAsync<MbPersonArtist>(url, JsonOptions, ct);
    }

    private async Task<string?> FetchWikidataImageAsync(string qid, CancellationToken ct)
    {
        try
        {
            var url = $"{WikidataBaseUrl}?action=wbgetentities&ids={Uri.EscapeDataString(qid)}&props=claims&format=json";
            using var stream = await _httpClient.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("entities", out var entities)
                || !entities.TryGetProperty(qid, out var entity)
                || !entity.TryGetProperty("claims", out var claims)
                || !claims.TryGetProperty("P18", out var p18)
                || p18.GetArrayLength() == 0)
                return null;

            var filename = p18[0]
                .GetProperty("mainsnak")
                .GetProperty("datavalue")
                .GetProperty("value")
                .GetString();

            return string.IsNullOrEmpty(filename)
                ? null
                : $"https://commons.wikimedia.org/wiki/Special:FilePath/{Uri.EscapeDataString(filename)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata image fetch failed for {Qid}", qid);
            return null;
        }
    }

    private async Task<string?> FetchWikipediaBiographyAsync(string qid, string language, CancellationToken ct)
    {
        try
        {
            var url = $"{WikidataBaseUrl}?action=wbgetentities&ids={Uri.EscapeDataString(qid)}&props=sitelinks&format=json";
            using var stream = await _httpClient.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("entities", out var entities)
                || !entities.TryGetProperty(qid, out var entity)
                || !entity.TryGetProperty("sitelinks", out var sitelinks))
                return null;

            var langCode = language.Split('-')[0].ToLowerInvariant();
            string[] candidates = [langCode + "wiki", "enwiki"];

            foreach (var wiki in candidates)
            {
                if (sitelinks.TryGetProperty(wiki, out var link)
                    && link.TryGetProperty("title", out var title))
                {
                    var wikiLang = wiki.Replace("wiki", "");
                    return await FetchWikipediaSummaryAsync(wikiLang, title.GetString()!, ct);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata sitelinks fetch failed for {Qid}", qid);
            return null;
        }
    }

    private async Task<string?> FetchWikipediaSummaryAsync(string lang, string title, CancellationToken ct)
    {
        try
        {
            var url = $"https://{Uri.EscapeDataString(lang)}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
            using var stream = await _httpClient.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.TryGetProperty("extract", out var extract) ? extract.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikipedia summary fetch failed for {Lang}:{Title}", lang, title);
            return null;
        }
    }

    private static PersonGender MapGender(string? gender) => gender?.ToLowerInvariant() switch
    {
        "male" => PersonGender.Male,
        "female" => PersonGender.Female,
        "non-binary" => PersonGender.NonBinary,
        _ => PersonGender.NotSpecified
    };

    private static DateOnly? ParseDate(string? date)
    {
        if (string.IsNullOrEmpty(date)) return null;
        if (DateOnly.TryParse(date, out var parsed)) return parsed;
        if (date.Length == 4 && int.TryParse(date, out var year)) return new DateOnly(year, 1, 1);
        if (date.Length == 7 && int.TryParse(date[..4], out var y) && int.TryParse(date[5..7], out var m))
            return new DateOnly(y, m, 1);
        return null;
    }

    private static string? ExtractQid(string wikidataUrl)
    {
        var idx = wikidataUrl.LastIndexOf('/');
        if (idx < 0 || idx == wikidataUrl.Length - 1) return null;
        var qid = wikidataUrl[(idx + 1)..];
        return qid.StartsWith('Q') ? qid : null;
    }

    private static List<ExternalIdEntry> ExtractExternalIds(List<MbRelation>? relations, string? wikidataUrl)
    {
        var ids = new List<ExternalIdEntry>();

        if (!string.IsNullOrEmpty(wikidataUrl))
        {
            var qid = ExtractQid(wikidataUrl);
            if (qid is not null)
                ids.Add(new ExternalIdEntry("wikidata", qid));
        }

        if (relations is null) return ids;

        var spotifyUrl = relations
            .FirstOrDefault(r => r.Type == "streaming music" && r.Url?.Resource?.Contains("spotify.com") == true)?.Url?.Resource;
        if (!string.IsNullOrEmpty(spotifyUrl))
        {
            var segments = new Uri(spotifyUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
                ids.Add(new ExternalIdEntry("spotify", segments[^1]));
        }

        var imdbUrl = relations
            .FirstOrDefault(r => r.Type == "IMDb")?.Url?.Resource;
        if (!string.IsNullOrEmpty(imdbUrl))
        {
            var segments = new Uri(imdbUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
                ids.Add(new ExternalIdEntry("imdb", segments[^1]));
        }

        return ids;
    }

    public async Task<IReadOnlyList<ProviderImageDto>> GetPersonImagesAsync(string providerId, string language, CancellationToken cancellationToken = default)
    {
        var results = new List<ProviderImageDto>();

        try
        {
            var artist = await FetchArtistAsync(providerId, cancellationToken);
            if (artist is null)
                return results;

            // 1. Try Wikidata portrait image
            var wikidataUrl = artist.Relations?
                .FirstOrDefault(r => r.Type == "wikidata")?.Url?.Resource;
            if (!string.IsNullOrEmpty(wikidataUrl))
            {
                var qid = ExtractQid(wikidataUrl);
                if (qid is not null)
                {
                    var imageUrl = await FetchWikidataImageAsync(qid, cancellationToken);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        var thumbUrl = imageUrl.Contains("Special:FilePath")
                            ? imageUrl + "?width=300"
                            : imageUrl;

                        results.Add(new ProviderImageDto
                        {
                            Url = imageUrl,
                            ThumbnailUrl = thumbUrl,
                            Type = MetadataPictureType.Portrait,
                            Width = 0,
                            Height = 0,
                            VoteAverage = 10,
                            Language = null
                        });
                    }
                }
            }

            // 2. Fetch album covers from CoverArtArchive as additional options
            await _rateLimiter.WaitAsync(Host, cancellationToken);
            var rgUrl = $"{MusicBrainzBaseUrl}/release-group?artist={Uri.EscapeDataString(providerId)}&type=album&limit=5&fmt=json";
            var rgResult = await _httpClient.GetFromJsonAsync<MbReleaseGroupSearchResult>(rgUrl, JsonOptions, cancellationToken);

            if (rgResult?.ReleaseGroups is not null)
            {
                foreach (var rg in rgResult.ReleaseGroups)
                {
                    if (string.IsNullOrEmpty(rg.Id)) continue;

                    try
                    {
                        await _rateLimiter.WaitAsync(Host, cancellationToken);
                        var coverUrl = $"{CoverArtBaseUrl}/release-group/{rg.Id}";
                        var response = await _httpClient.GetAsync(coverUrl, cancellationToken);
                        if (!response.IsSuccessStatusCode) continue;

                        var coverArt = await response.Content.ReadFromJsonAsync<CoverArtResponse>(JsonOptions, cancellationToken);
                        var frontImage = coverArt?.Images?.FirstOrDefault(i => i.Front == true)
                            ?? coverArt?.Images?.FirstOrDefault();

                        if (frontImage?.Image is not null)
                        {
                            var thumb = frontImage.Thumbnails?.Large ?? frontImage.Thumbnails?.Small ?? frontImage.Image;
                            results.Add(new ProviderImageDto
                            {
                                Url = frontImage.Image,
                                ThumbnailUrl = thumb,
                                Type = MetadataPictureType.Portrait,
                                Width = 0,
                                Height = 0,
                                VoteAverage = 5,
                                Language = null
                            });
                        }
                    }
                    catch
                    {
                        // Skip unavailable cover art
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MusicBrainz person images fetch failed for {Id}", providerId);
        }

        return results;
    }

    #region MusicBrainz DTOs

    private record MbPersonArtist
    {
        public string? Name { get; init; }
        public string? Type { get; init; }
        public string? Gender { get; init; }
        public MbArea? Area { get; init; }
        [JsonPropertyName("begin-area")]
        public MbArea? BeginArea { get; init; }
        [JsonPropertyName("life-span")]
        public MbLifeSpan? LifeSpan { get; init; }
        public List<MbRelation>? Relations { get; init; }
    }

    private record MbLifeSpan
    {
        public string? Begin { get; init; }
        public string? End { get; init; }
        public bool? Ended { get; init; }
    }

    private record MbArea
    {
        public string? Name { get; init; }
    }

    private record MbRelation
    {
        public string? Type { get; init; }
        public MbUrl? Url { get; init; }
    }

    private record MbUrl
    {
        public string? Resource { get; init; }
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

    private record CoverArtResponse
    {
        public List<CoverArtImage>? Images { get; init; }
    }

    private record CoverArtImage
    {
        public string? Image { get; init; }
        public bool? Front { get; init; }
        public CoverArtThumbnails? Thumbnails { get; init; }
    }

    private record CoverArtThumbnails
    {
        public string? Small { get; init; }
        public string? Large { get; init; }
        [JsonPropertyName("1200")]
        public string? ExtraLarge { get; init; }
    }

    #endregion
}
