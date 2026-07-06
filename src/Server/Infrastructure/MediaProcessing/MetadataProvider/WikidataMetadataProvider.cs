using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class WikidataMetadataProvider : IMusicArtistMetadataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikidataMetadataProvider> _logger;

    public WikidataMetadataProvider(HttpClient httpClient, ILogger<WikidataMetadataProvider> logger)
    {
        _httpClient = httpClient;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _logger = logger;
    }

    public string ProviderName => "wikidata";

    public async Task<ExternalMusicArtistDetails?> FetchByProviderIdAsync(
        string providerId, string language, CancellationToken cancellationToken = default)
    {
        try
        {
            var (imageUrl, wikiLang, wikiTitle) = await FetchWikidataInfoAsync(providerId, language, cancellationToken);

            string? biography = null;
            if (wikiLang != null && wikiTitle != null)
                biography = await FetchWikipediaSummaryAsync(wikiLang, wikiTitle, cancellationToken);

            if (imageUrl == null && biography == null) return null;

            return new ExternalMusicArtistDetails
            {
                Biography = biography,
                ImageUrl = imageUrl,
                WikidataId = providerId
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Artist metadata fetch failed for Wikidata {Id}", providerId);
            return null;
        }
    }

    public Task<ExternalMusicArtistDetails?> SearchByNameAsync(
        string artistName, string language, CancellationToken cancellationToken = default) => Task.FromResult<ExternalMusicArtistDetails?>(null);

    private async Task<(string? ImageUrl, string? WikiLang, string? WikiTitle)> FetchWikidataInfoAsync(
        string qid, string language, CancellationToken ct)
    {
        try
        {
            var url = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={Uri.EscapeDataString(qid)}&props=claims|sitelinks&format=json";
            using var stream = await _httpClient.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("entities", out var entities)
                || !entities.TryGetProperty(qid, out var entity))
                return (null, null, null);

            var imageUrl = ExtractCommonsImageUrl(entity);
            var (wikiLang, wikiTitle) = PickWikipediaTitle(entity, language);
            return (imageUrl, wikiLang, wikiTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata fetch failed for {Qid}", qid);
            return (null, null, null);
        }
    }

    private static string? ExtractCommonsImageUrl(JsonElement entity)
    {
        if (!entity.TryGetProperty("claims", out var claims)
            || !claims.TryGetProperty("P18", out var p18)
            || p18.GetArrayLength() == 0)
            return null;

        var filename = p18[0]
            .GetProperty("mainsnak")
            .GetProperty("datavalue")
            .GetProperty("value")
            .GetString();

        return MetadataImageUrlHelper.BuildWikimediaCommonsImageUrl(filename);
    }

    private static (string? Lang, string? Title) PickWikipediaTitle(JsonElement entity, string language)
    {
        if (!entity.TryGetProperty("sitelinks", out var sitelinks))
            return (null, null);

        var langCode = language.Split('-')[0].ToLowerInvariant();
        string[] candidates = [langCode + "wiki", "enwiki"];

        foreach (var wiki in candidates)
        {
            if (sitelinks.TryGetProperty(wiki, out var link)
                && link.TryGetProperty("title", out var title))
            {
                return (wiki.Replace("wiki", ""), title.GetString());
            }
        }

        return (null, null);
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
}
