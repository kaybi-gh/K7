using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;
using Microsoft.Extensions.Logging;
using MediaType = K7.Server.Domain.Enums.MediaType;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class TvdbSerieMetadataProvider : ISerieMetadataProvider, ISearchableMetadataProvider, IMetadataImageProvider, IMetadataProviderInfo
{
    private readonly TvdbApiClient _apiClient;
    private readonly ILogger<TvdbSerieMetadataProvider> _logger;
    private readonly Dictionary<int, IReadOnlyList<TvdbEpisodeBase>> _episodeCache = new();
    private readonly Dictionary<int, string?> _seriesOriginalLanguageCache = new();

    public TvdbSerieMetadataProvider(TvdbApiClient apiClient, ILogger<TvdbSerieMetadataProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public string ProviderName => "tvdb";

    public IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; } = [LibraryMediaType.Serie];

    public async Task<string?> SearchSerieAsync(MediaIdentification identification, CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
            return null;

        try
        {
            var query = identification.SeriesTitle ?? identification.Title;
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var year = identification.ReleaseYear?.Year;
            var results = await _apiClient.SearchSeriesAsync(query, year, cancellationToken);
            var bestMatch = results.FirstOrDefault();
            return bestMatch is null ? null : ResolveSearchResultId(bestMatch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching TVDB for serie {Title}", identification.SeriesTitle ?? identification.Title);
            return null;
        }
    }

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(
        string query,
        int? year,
        string? providerId,
        MediaType? mediaType,
        string language,
        CancellationToken cancellationToken)
    {
        if (!_apiClient.IsConfigured)
            return [];

        if (mediaType.HasValue && mediaType != MediaType.Serie)
            return [];

        var results = new List<MetadataSearchResult>();

        try
        {
            var trimmedProviderId = providerId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedProviderId))
            {
                var seriesId = await ResolveSeriesIdAsync(trimmedProviderId, cancellationToken);
                if (seriesId.HasValue)
                {
                    var series = await _apiClient.GetSeriesExtendedAsync(seriesId.Value, cancellationToken);
                    if (series is not null)
                        results.Add(await MapSeriesToSearchResultAsync(series, language, cancellationToken));
                }

                return results;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResults = await _apiClient.SearchSeriesAsync(query, year, cancellationToken);
                foreach (var item in searchResults)
                {
                    var id = ResolveSearchResultId(item);
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    results.Add(new MetadataSearchResult
                    {
                        Provider = "tvdb",
                        ExternalId = id,
                        Title = item.NameTranslated ?? item.Name ?? string.Empty,
                        Year = ParseYear(item.FirstAirTime, item.Year),
                        PosterUrl = TvdbImageUrlHelper.BuildImageUrl(item.ImageUrl),
                        Overview = item.Overview
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching TVDB series for {Query}", query);
        }

        return results;
    }

    public async Task<ExternalSerieMetadata> FetchSerieMetadataAsync(
        string providerId,
        string language,
        CancellationToken cancellationToken = default,
        string? fallbackLanguage = null)
    {
        var seriesId = await ResolveSeriesIdAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException($"Invalid TVDB series id '{providerId}'.");

        var series = await _apiClient.GetSeriesExtendedAsync(seriesId, cancellationToken)
            ?? throw new InvalidOperationException($"TVDB returned no data for series {seriesId}.");

        CacheSeriesOriginalLanguage(seriesId, series.OriginalLanguage);

        var (title, overview) = await TvdbTranslationResolver.ResolveSeriesTextAsync(
            _apiClient,
            seriesId,
            series.Name,
            series.Overview,
            series.OriginalLanguage,
            language,
            fallbackLanguage,
            cancellationToken);

        var artworks = await ResolveSeriesArtworksAsync(seriesId, series.Artworks, cancellationToken);

        return new ExternalSerieMetadata
        {
            Title = title,
            SortTitle = MediaSortTitleHelper.Compute(title),
            OriginalTitle = series.Name,
            ReleaseDate = ParseDate(series.FirstAired),
            Overview = overview,
            Status = series.Status?.Name,
            OriginalLanguage = TvdbLanguageHelper.ToIso6391(series.OriginalLanguage),
            ContentRating = ExtractContentRating(series.ContentRatings, language),
            Network = series.OriginalNetwork?.Name ?? series.LatestNetwork?.Name,
            TotalSeasons = series.Seasons?.Count(s => s.Number > 0),
            Genres = [.. series.Genres?.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>() ?? []],
            Studios = [.. series.Companies?
                .Where(c => string.Equals(c.CompanyType?.CompanyTypeName, "Production Company", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>() ?? []],
            Trailers = MapTrailers(series.Trailers),
            PersonRoles = MapCharactersToPersonRoles(series.Characters),
            ExternalIds = BuildExternalIds(seriesId.ToString(), series.RemoteIds),
            Pictures = MapArtworksToPictures(artworks, language, series.Image),
            Ratings = []
        };
    }

    public async Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(
        string providerId,
        int seasonNumber,
        string language,
        CancellationToken cancellationToken = default,
        string? fallbackLanguage = null)
    {
        var seriesId = await ResolveSeriesIdAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException($"Invalid TVDB series id '{providerId}'.");

        var series = await _apiClient.GetSeriesExtendedAsync(seriesId, cancellationToken)
            ?? throw new InvalidOperationException($"TVDB returned no data for series {seriesId}.");

        CacheSeriesOriginalLanguage(seriesId, series.OriginalLanguage);

        var seasonRef = series.Seasons?.FirstOrDefault(s => s.Number == seasonNumber)
            ?? throw new InvalidOperationException($"TVDB season {seasonNumber} not found for series {seriesId}.");

        var season = await _apiClient.GetSeasonExtendedAsync(seasonRef.Id, cancellationToken);
        var defaultTitle = season?.Name ?? seasonRef.Name ?? (seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}");
        var (title, overview) = await TvdbTranslationResolver.ResolveSeasonTextAsync(
            _apiClient,
            seasonRef.Id,
            defaultTitle,
            season?.Overview,
            series.OriginalLanguage,
            language,
            fallbackLanguage,
            cancellationToken);
        var pictures = new List<MetadataPicture>();

        var posterUrl = TvdbImageUrlHelper.BuildImageUrl(season?.Image ?? seasonRef.Image);
        if (posterUrl is not null)
        {
            var posterPicture = CreatePicture(posterUrl, MetadataPictureType.Poster);
            if (posterPicture is not null)
                pictures.Add(posterPicture);
        }

        foreach (var artwork in season?.Artwork ?? [])
        {
            if (artwork.Type != TvdbArtworkTypes.Season.Poster)
                continue;

            var url = TvdbImageUrlHelper.BuildImageUrl(artwork.Image);
            if (url is null)
                continue;

            var artworkPicture = CreatePicture(url, MetadataPictureType.Poster);
            if (artworkPicture is not null)
                pictures.Add(artworkPicture);
        }

        var episodes = await GetCachedEpisodesAsync(seriesId, cancellationToken);

        return new ExternalSeasonMetadata
        {
            SeasonNumber = seasonNumber,
            Title = title,
            SortTitle = MediaSortTitleHelper.Compute(title),
            Overview = overview,
            AirDate = ParseDate(season?.Year),
            EpisodeCount = episodes.Count(e => e.SeasonNumber == seasonNumber),
            ExternalIds = [],
            Pictures = pictures
        };
    }

    public async Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(
        string providerId,
        int seasonNumber,
        int episodeNumber,
        string language,
        CancellationToken cancellationToken = default,
        string? fallbackLanguage = null)
    {
        var seriesId = await ResolveSeriesIdAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException($"Invalid TVDB series id '{providerId}'.");

        var originalLanguage = await GetSeriesOriginalLanguageAsync(seriesId, cancellationToken);

        var episodes = await GetCachedEpisodesAsync(seriesId, cancellationToken);
        var episodeRef = episodes.FirstOrDefault(e => e.SeasonNumber == seasonNumber && e.Number == episodeNumber)
            ?? throw new InvalidOperationException($"TVDB episode S{seasonNumber}E{episodeNumber} not found for series {seriesId}.");

        var episode = await _apiClient.GetEpisodeExtendedAsync(episodeRef.Id, cancellationToken);
        var defaultTitle = episode?.Name ?? episodeRef.Name ?? $"Episode {episodeNumber}";
        var defaultOverview = episode?.Overview ?? episodeRef.Overview;

        var (title, overview) = await TvdbTranslationResolver.ResolveEpisodeTextAsync(
            _apiClient,
            episodeRef.Id,
            defaultTitle,
            defaultOverview,
            originalLanguage,
            language,
            fallbackLanguage,
            cancellationToken);

        var stillUrl = TvdbImageUrlHelper.BuildImageUrl(episode?.Image ?? episodeRef.Image);

        return new ExternalEpisodeMetadata
        {
            EpisodeNumber = episodeNumber,
            SeasonNumber = seasonNumber,
            Title = title,
            SortTitle = MediaSortTitleHelper.Compute(title),
            Overview = overview,
            AirDate = ParseDate(episode?.Aired ?? episodeRef.Aired),
            Runtime = episode?.Runtime ?? episodeRef.Runtime,
            StillImageUrl = stillUrl,
            ExternalIds = BuildExternalIds(episodeRef.Id.ToString(), episode?.RemoteIds),
            PersonRoles = MapCharactersToPersonRoles(episode?.Characters)
        };
    }

    public async Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(
        string providerId,
        int absoluteNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var seriesId = await ResolveSeriesIdAsync(providerId, cancellationToken);
            if (!seriesId.HasValue)
                return null;

            var episodes = await GetCachedEpisodesAsync(seriesId.Value, cancellationToken);
            var match = episodes.FirstOrDefault(e => e.AbsoluteNumber == absoluteNumber);
            if (match is not null)
                return (match.SeasonNumber, match.Number);

            var ordered = episodes
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.Number)
                .ToList();

            if (absoluteNumber >= 1 && absoluteNumber <= ordered.Count)
            {
                var fallback = ordered[absoluteNumber - 1];
                return (fallback.SeasonNumber, fallback.Number);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving absolute episode {AbsoluteNumber} for TVDB serie {ProviderId}", absoluteNumber, providerId);
        }

        return (1, absoluteNumber);
    }

    public bool SupportsMediaType(MediaType mediaType) =>
        mediaType is MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    public async Task<IReadOnlyList<ProviderImageDto>> GetImagesAsync(
        ImageProviderContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
            return [];

        var results = new List<ProviderImageDto>();

        try
        {
            var seriesId = await ResolveSeriesIdAsync(context.ProviderId, cancellationToken);
            if (!seriesId.HasValue)
                return results;

            switch (context.MediaType)
            {
                case MediaType.Serie:
                    await AddSeriesImagesAsync(seriesId.Value, context.Language, results, cancellationToken);
                    break;
                case MediaType.SerieSeason when context.SeasonNumber.HasValue:
                    await AddSeasonImagesAsync(seriesId.Value, context.SeasonNumber.Value, results, cancellationToken);
                    break;
                case MediaType.SerieEpisode when context.SeasonNumber.HasValue && context.EpisodeNumber.HasValue:
                    await AddEpisodeImagesAsync(seriesId.Value, context.SeasonNumber.Value, context.EpisodeNumber.Value, results, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TVDB image fetch failed for provider id {ProviderId}", context.ProviderId);
        }

        return MetadataImageUrlHelper.FilterProviderImages(results);
    }

    private async Task AddSeriesImagesAsync(
        int seriesId,
        string language,
        List<ProviderImageDto> results,
        CancellationToken cancellationToken)
    {
        var artworks = await _apiClient.GetSeriesArtworksAsync(seriesId, cancellationToken);
        AddArtworkImages(results, artworks, language);
    }

    private async Task AddSeasonImagesAsync(
        int seriesId,
        int seasonNumber,
        List<ProviderImageDto> results,
        CancellationToken cancellationToken)
    {
        var series = await _apiClient.GetSeriesExtendedAsync(seriesId, cancellationToken);
        var seasonRef = series?.Seasons?.FirstOrDefault(s => s.Number == seasonNumber);
        if (seasonRef is null)
            return;

        var season = await _apiClient.GetSeasonExtendedAsync(seasonRef.Id, cancellationToken);
        if (season?.Artwork is not null)
            AddArtworkImages(results, season.Artwork, null, TvdbArtworkTypes.Season.Poster);
    }

    private async Task AddEpisodeImagesAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        List<ProviderImageDto> results,
        CancellationToken cancellationToken)
    {
        var episodes = await GetCachedEpisodesAsync(seriesId, cancellationToken);
        var episodeRef = episodes.FirstOrDefault(e => e.SeasonNumber == seasonNumber && e.Number == episodeNumber);
        if (episodeRef is null)
            return;

        var episode = await _apiClient.GetEpisodeExtendedAsync(episodeRef.Id, cancellationToken);
        var artworks = episode?.Artworks ?? [];
        if (artworks.Count == 0)
        {
            var stillUrl = TvdbImageUrlHelper.BuildImageUrl(episode?.Image ?? episodeRef.Image);
            if (stillUrl is not null)
            {
                results.Add(new ProviderImageDto
                {
                    Url = stillUrl,
                    ThumbnailUrl = stillUrl,
                    Type = MetadataPictureType.Still,
                    Provider = ProviderName
                });
            }

            return;
        }

        AddArtworkImages(results, artworks, null);
    }

    private async Task<IReadOnlyList<TvdbEpisodeBase>> GetCachedEpisodesAsync(int seriesId, CancellationToken cancellationToken)
    {
        if (_episodeCache.TryGetValue(seriesId, out var cached))
            return cached;

        var episodes = await _apiClient.GetAllSeriesEpisodesAsync(seriesId, cancellationToken);
        _episodeCache[seriesId] = episodes;
        return episodes;
    }

    private async Task<string?> GetSeriesOriginalLanguageAsync(int seriesId, CancellationToken cancellationToken)
    {
        if (_seriesOriginalLanguageCache.TryGetValue(seriesId, out var cached))
            return cached;

        var series = await _apiClient.GetSeriesExtendedAsync(seriesId, cancellationToken);
        CacheSeriesOriginalLanguage(seriesId, series?.OriginalLanguage);
        return series?.OriginalLanguage;
    }

    private void CacheSeriesOriginalLanguage(int seriesId, string? originalLanguage) =>
        _seriesOriginalLanguageCache[seriesId] = originalLanguage;

    private async Task<int?> ResolveSeriesIdAsync(string providerId, CancellationToken cancellationToken)
    {
        var trimmed = providerId.Trim();
        if (int.TryParse(trimmed, out var seriesId))
            return seriesId;

        if (trimmed.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
            return await _apiClient.FindSeriesIdByRemoteIdAsync(trimmed, cancellationToken);

        return null;
    }

    private async Task<MetadataSearchResult> MapSeriesToSearchResultAsync(
        TvdbSeriesExtended series,
        string language,
        CancellationToken cancellationToken)
    {
        var (title, overview) = await TvdbTranslationResolver.ResolveSeriesTextAsync(
            _apiClient,
            series.Id,
            series.Name,
            series.Overview,
            series.OriginalLanguage,
            language,
            fallbackLanguage: null,
            cancellationToken);

        return new MetadataSearchResult
        {
            Provider = ProviderName,
            ExternalId = series.Id.ToString(),
            Title = title,
            Year = ParseYear(series.FirstAired, series.Year),
            PosterUrl = TvdbImageUrlHelper.BuildImageUrl(series.Image),
            Overview = overview
        };
    }

    private static string ResolveSearchResultId(TvdbSearchResult result) =>
        result.TvdbId ?? result.ObjectId ?? result.Id ?? string.Empty;

    private static int? ParseYear(string? firstAirTime, string? year)
    {
        if (int.TryParse(year, out var parsedYear))
            return parsedYear;

        if (DateOnly.TryParse(firstAirTime, out var date))
            return date.Year;

        return null;
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, out var date) ? date : null;

    private static string? ExtractContentRating(IReadOnlyList<TvdbContentRating>? ratings, string language)
    {
        if (ratings is null || ratings.Count == 0)
            return null;

        var langUpper = language.Length >= 2 ? language[..2].ToUpperInvariant() : language.ToUpperInvariant();
        foreach (var country in new[] { langUpper, "US" })
        {
            var match = ratings.FirstOrDefault(r => string.Equals(r.Country, country, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match?.Name))
                return match.Name;
        }

        return ratings.Select(r => r.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
    }

    private static List<TrailerInfo> MapTrailers(IReadOnlyList<TvdbTrailer>? trailers) =>
        [.. trailers?
            .Where(t => !string.IsNullOrWhiteSpace(t.Url))
            .Select(t => new TrailerInfo
            {
                Key = ExtractYouTubeKey(t.Url!) ?? t.Url!,
                Name = t.Name ?? "Trailer",
                Site = t.Url!.Contains("youtube", StringComparison.OrdinalIgnoreCase) ? "YouTube" : "External",
                Type = "Trailer",
                Language = t.Language
            }) ?? []];

    private static string? ExtractYouTubeKey(string url)
    {
        if (!url.Contains("youtube", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.TrimStart('/');

        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("v=", StringComparison.Ordinal))
                return part[2..];
        }

        return null;
    }

    private static IList<BasePersonRole> MapCharactersToPersonRoles(IReadOnlyList<TvdbCharacter>? characters)
    {
        var roles = new List<BasePersonRole>();
        if (characters is null)
            return roles;

        var order = 0;
        foreach (var character in characters.OrderBy(c => c.Sort))
        {
            if (string.IsNullOrWhiteSpace(character.PersonName) && string.IsNullOrWhiteSpace(character.Name))
                continue;

            var personName = character.PersonName ?? character.Name ?? string.Empty;
            var actor = new Actor
            {
                Order = order++,
                CharacterName = character.Name ?? string.Empty,
                ExternalIds =
                [
                    new ExternalId
                    {
                        ProviderName = "tvdb",
                        Value = character.PeopleId?.ToString() ?? character.Id.ToString()
                    }
                ],
                Person = new Person
                {
                    Name = personName,
                    ExternalIds =
                    [
                        new ExternalId
                        {
                            ProviderName = "tvdb",
                            Value = character.PeopleId?.ToString() ?? character.Id.ToString()
                        }
                    ]
                }
            };

            var portraitUrl = TvdbImageUrlHelper.BuildImageUrl(character.PersonImgUrl ?? character.Image);
            if (MetadataImageUrlHelper.TryCreateRemoteUri(portraitUrl, out var portraitUri))
            {
                actor.PortraitPicture = new MetadataPicture
                {
                    OriginalRemoteUri = portraitUri,
                    Type = MetadataPictureType.Portrait
                };
                actor.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(actor.PortraitPicture));
            }

            roles.Add(actor);
        }

        return roles;
    }

    private static List<ExternalId> BuildExternalIds(string primaryId, IReadOnlyList<TvdbRemoteId>? remoteIds) =>
        TvdbExternalIdMapper.BuildExternalIds(primaryId, remoteIds);

    private async Task<List<TvdbArtwork>> ResolveSeriesArtworksAsync(
        int seriesId,
        IReadOnlyList<TvdbArtwork>? extendedArtworks,
        CancellationToken cancellationToken)
    {
        var fromExtended = extendedArtworks ?? [];

        static bool HasLogoTypes(IReadOnlyList<TvdbArtwork> list) =>
            list.Any(a => a.Type is TvdbArtworkTypes.Series.ClearLogo or TvdbArtworkTypes.Series.ClearArt);

        static bool HasBackdrop(IReadOnlyList<TvdbArtwork> list) =>
            list.Any(a => a.Type == TvdbArtworkTypes.Series.Background);

        if (fromExtended.Count > 0 && HasLogoTypes(fromExtended) && HasBackdrop(fromExtended))
            return [.. fromExtended];

        var fromEndpoint = await _apiClient.GetSeriesArtworksAsync(seriesId, cancellationToken);
        if (fromExtended.Count == 0)
            return [.. fromEndpoint];

        if (fromEndpoint.Count == 0)
            return [.. fromExtended];

        return [.. fromExtended
            .Concat(fromEndpoint)
            .GroupBy(a => a.Id)
            .Select(g => g.First())];
    }

    private static List<MetadataPicture> MapArtworksToPictures(
        IReadOnlyList<TvdbArtwork> artworks,
        string language,
        string? fallbackImage)
    {
        var pictures = new List<MetadataPicture>();
        var tvdbLanguage = TvdbLanguageHelper.ToTvdbLanguage(language);

        var backdrop = SelectBestArtwork(artworks, TvdbArtworkTypes.Series.Background, tvdbLanguage);
        var poster = SelectBestArtwork(artworks, TvdbArtworkTypes.Series.Poster, tvdbLanguage);
        var logo = SelectBestArtwork(artworks, TvdbArtworkTypes.Series.ClearLogo, tvdbLanguage)
            ?? SelectBestArtwork(artworks, TvdbArtworkTypes.Series.ClearArt, tvdbLanguage);

        if (backdrop is not null)
            AddArtworkPicture(pictures, backdrop, MetadataPictureType.Backdrop);
        if (logo is not null)
            AddArtworkPicture(pictures, logo, MetadataPictureType.Logo);
        if (poster is not null)
            AddArtworkPicture(pictures, poster, MetadataPictureType.Poster);

        if (pictures.Count == 0)
        {
            var fallbackUrl = TvdbImageUrlHelper.BuildImageUrl(fallbackImage);
            if (fallbackUrl is not null)
            {
                var fallbackPicture = CreatePicture(fallbackUrl, MetadataPictureType.Poster);
                if (fallbackPicture is not null)
                    pictures.Add(fallbackPicture);
            }
        }

        return pictures;
    }

    private static TvdbArtwork? SelectBestArtwork(IReadOnlyList<TvdbArtwork> artworks, int type, string language) =>
        artworks
            .Where(a => a.Type == type && !string.IsNullOrWhiteSpace(a.Image))
            .OrderByDescending(a => string.Equals(a.Language, language, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => string.Equals(a.Language, "eng", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Score ?? 0)
            .FirstOrDefault();

    private static void AddArtworkPicture(List<MetadataPicture> pictures, TvdbArtwork artwork, MetadataPictureType type)
    {
        var url = TvdbImageUrlHelper.BuildImageUrl(artwork.Image);
        if (url is null)
            return;

        var picture = CreatePicture(url, type);
        if (picture is not null)
            pictures.Add(picture);
    }

    private static MetadataPicture? CreatePicture(string url, MetadataPictureType type)
    {
        if (!MetadataImageUrlHelper.TryCreateRemoteUri(url, out var remoteUri))
            return null;

        var picture = new MetadataPicture
        {
            OriginalRemoteUri = remoteUri,
            Type = type
        };
        picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
        return picture;
    }

    private static void AddArtworkImages(
        List<ProviderImageDto> results,
        IReadOnlyList<TvdbArtwork> artworks,
        string? language,
        int? typeFilter = null)
    {
        var tvdbLanguage = language is null ? null : TvdbLanguageHelper.ToTvdbLanguage(language);
        var filtered = artworks.Where(a => !string.IsNullOrWhiteSpace(a.Image));
        if (typeFilter.HasValue)
            filtered = filtered.Where(a => a.Type == typeFilter.Value);

        foreach (var artwork in filtered
                     .OrderByDescending(a => tvdbLanguage is not null && string.Equals(a.Language, tvdbLanguage, StringComparison.OrdinalIgnoreCase))
                     .ThenByDescending(a => a.Score ?? 0))
        {
            var url = TvdbImageUrlHelper.BuildImageUrl(artwork.Image);
            var thumb = TvdbImageUrlHelper.BuildImageUrl(artwork.Thumbnail) ?? url;
            if (url is null || thumb is null)
                continue;

            var pictureType = TvdbArtworkTypes.MapToPictureType(artwork.Type);
            if (pictureType is null)
                continue;

            var normalized = MetadataImageUrlHelper.NormalizeProviderImage(new ProviderImageDto
            {
                Url = url,
                ThumbnailUrl = thumb,
                Type = pictureType.Value,
                Provider = "tvdb",
                Width = artwork.Width is null ? 0 : (int)artwork.Width,
                Height = artwork.Height is null ? 0 : (int)artwork.Height,
                Language = artwork.Language
            });

            if (normalized is not null)
                results.Add(normalized);
        }
    }
}
