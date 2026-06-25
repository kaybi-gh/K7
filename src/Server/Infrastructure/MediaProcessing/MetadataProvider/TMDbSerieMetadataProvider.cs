using System.Collections.Frozen;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.Extensions.Logging;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using MediaType = K7.Server.Domain.Enums.MediaType;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class TMDbSerieMetadataProvider : ISerieMetadataProvider, ISearchableMetadataProvider, IMetadataImageProvider
{
    private readonly TMDbClient _tmdbClient;
    private readonly ILogger<TMDbSerieMetadataProvider> _logger;

    public string ProviderName => "tmdb";

    private readonly FrozenSet<(string Department, string Job)> _wantedCrewRoles = new List<(string, string)>
    {
        ("Production", "Producer"),
        ("Production", "Executive Producer"),
        ("Directing", "Director"),
        ("Writing", "Characters"),
        ("Writing", "Story"),
        ("Writing", "Screenplay")
    }.ToFrozenSet();

    public TMDbSerieMetadataProvider(TMDbClient tmdbClient, ILogger<TMDbSerieMetadataProvider> logger)
    {
        _tmdbClient = tmdbClient;
        _logger = logger;
    }

    public async Task<string?> SearchSerieAsync(MediaIdentification identification, CancellationToken cancellationToken = default)
    {
        try
        {
            var year = identification.ReleaseYear.HasValue ? identification.ReleaseYear.Value.Year : 0;
            var searchResult = await _tmdbClient.SearchTvShowAsync(
                identification.SeriesTitle ?? identification.Title,
                firstAirDateYear: year,
                cancellationToken: cancellationToken);

            return searchResult.Results.FirstOrDefault()?.Id.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching TMDb for serie {Title}", identification.SeriesTitle ?? identification.Title);
            return null;
        }
    }

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(
        string query, int? year, string? providerId, K7.Server.Domain.Enums.MediaType? mediaType, string language, CancellationToken cancellationToken)
    {
        if (mediaType.HasValue && mediaType != K7.Server.Domain.Enums.MediaType.Serie)
            return [];

        var results = new List<MetadataSearchResult>();

        try
        {
            if (!string.IsNullOrWhiteSpace(providerId) && int.TryParse(providerId, out var tmdbId))
            {
                var show = await _tmdbClient.GetTvShowAsync(tmdbId, language: language, cancellationToken: cancellationToken);
                if (show is not null)
                {
                    results.Add(MapToSearchResult(show.Id, show.Name, show.FirstAirDate, show.PosterPath, show.Overview));
                }
                return results;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResult = await _tmdbClient.SearchTvShowAsync(
                    query, language: language, firstAirDateYear: year ?? 0, cancellationToken: cancellationToken);

                if (searchResult?.Results is not null)
                {
                    results.AddRange(searchResult.Results.Select(show =>
                        MapToSearchResult(show.Id, show.Name, show.FirstAirDate, show.PosterPath, show.Overview)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching TMDb series for {Query}", query);
        }

        return results;
    }

    public async Task<ExternalSerieMetadata> FetchSerieMetadataAsync(
        string providerId, string language, CancellationToken cancellationToken = default)
    {
        var tmdbId = await ResolveTmdbIdAsync(providerId, cancellationToken);
        var show = await _tmdbClient.GetTvShowAsync(
            tmdbId,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvShowMethods.Credits | TvShowMethods.Images | TvShowMethods.ContentRatings | TvShowMethods.ExternalIds | TvShowMethods.Videos | TvShowMethods.Recommendations,
            cancellationToken: cancellationToken);

        var contentRating = ExtractContentRating(show.ContentRatings, language);

        var metadata = new ExternalSerieMetadata
        {
            Title = show.Name,
            OriginalTitle = show.OriginalName,
            ReleaseDate = show.FirstAirDate.HasValue ? DateOnly.FromDateTime(show.FirstAirDate.Value) : null,
            Overview = show.Overview,
            Status = show.Status,
            OriginalLanguage = show.OriginalLanguage,
            ContentRating = contentRating,
            Network = show.Networks?.FirstOrDefault()?.Name,
            TotalSeasons = show.NumberOfSeasons,
            Genres = [.. show.Genres?.Select(g => g.Name) ?? []],
            Studios = [.. show.ProductionCompanies?.Select(c => c.Name) ?? []],
            Trailers = [.. show.Videos?.Results?
                .Where(v => v.Site == "YouTube" && v.Type is "Trailer" or "Teaser")
                .Select(v => new TrailerInfo
                {
                    Key = v.Key,
                    Name = v.Name,
                    Site = v.Site,
                    Type = v.Type,
                    Language = v.Iso_639_1
                }) ?? []],
            RecommendedExternalIds = [.. show.Recommendations?.Results?.Select(r => r.Id.ToString()) ?? []],
            PersonRoles = await ConvertToPersonRolesAsync(show.Credits, language, cancellationToken),
            ExternalIds = BuildExternalIds(providerId, show.ExternalIds),
            Pictures = FetchMetadataPictures(show.Images, language),
            Ratings = show.VoteCount > 0
                ? [new MetadataProviderRating { MetadataProvider = Domain.Enums.MetadataProvider.TMDb, Value = show.VoteAverage, MinimumValue = 0, MaximumValue = 10, RatingCount = show.VoteCount }]
                : []
        };

        return metadata;
    }

    public async Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(
        string providerId, int seasonNumber, string language, CancellationToken cancellationToken = default)
    {
        var tmdbId = await ResolveTmdbIdAsync(providerId, cancellationToken);
        var season = await _tmdbClient.GetTvSeasonAsync(
            tmdbId,
            seasonNumber,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvSeasonMethods.Images | TvSeasonMethods.ExternalIds,
            cancellationToken: cancellationToken);

        if (season is null)
        {
            throw new InvalidOperationException($"TMDb returned null for series {tmdbId} season {seasonNumber}.");
        }

        var pictures = new List<MetadataPicture>();
        if (!string.IsNullOrEmpty(season.PosterPath))
        {
            var uri = _tmdbClient.GetImageUrl("original", season.PosterPath, true);
            if (uri is not null)
            {
                var poster = new MetadataPicture
                {
                    OriginalRemoteUri = uri,
                    Type = MetadataPictureType.Poster
                };
                poster.AddDomainEvent(new MetadataPictureCreatedEvent(poster));
                pictures.Add(poster);
            }
        }

        return new ExternalSeasonMetadata
        {
            SeasonNumber = season.SeasonNumber,
            Title = season.Name,
            Overview = season.Overview,
            AirDate = season.AirDate.HasValue ? DateOnly.FromDateTime(season.AirDate.Value) : null,
            EpisodeCount = season.Episodes?.Count,
            ExternalIds = [],
            Pictures = pictures
        };
    }

    public async Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(
        string providerId, int seasonNumber, int episodeNumber, string language, CancellationToken cancellationToken = default)
    {
        var tmdbId = await ResolveTmdbIdAsync(providerId, cancellationToken);
        var episode = await _tmdbClient.GetTvEpisodeAsync(
            tmdbId,
            seasonNumber,
            episodeNumber,
            language: language,
            extraMethods: TvEpisodeMethods.Images | TvEpisodeMethods.ExternalIds | TvEpisodeMethods.Credits,
            cancellationToken: cancellationToken);

        string? stillUrl = null;
        if (!string.IsNullOrEmpty(episode.StillPath))
        {
            stillUrl = _tmdbClient.GetImageUrl("original", episode.StillPath, true)?.ToString();
        }

        var externalIds = new List<ExternalId>();
        if (!string.IsNullOrEmpty(episode.ExternalIds?.ImdbId))
            externalIds.Add(new ExternalId { ProviderName = "imdb", Value = episode.ExternalIds.ImdbId });
        if (!string.IsNullOrEmpty(episode.ExternalIds?.TvdbId))
            externalIds.Add(new ExternalId { ProviderName = "tvdb", Value = episode.ExternalIds.TvdbId });

        return new ExternalEpisodeMetadata
        {
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Title = episode.Name,
            Overview = episode.Overview,
            AirDate = episode.AirDate.HasValue ? DateOnly.FromDateTime(episode.AirDate.Value) : null,
            Runtime = episode.Runtime,
            StillImageUrl = stillUrl,
            ExternalIds = externalIds,
            PersonRoles = await ConvertToPersonRolesAsync(episode.Credits, language, cancellationToken)
        };
    }

    public async Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(
        string providerId, int absoluteNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var tmdbId = await ResolveTmdbIdAsync(providerId, cancellationToken);
            var show = await _tmdbClient.GetTvShowAsync(
                tmdbId,
                extraMethods: TvShowMethods.EpisodeGroups,
                cancellationToken: cancellationToken);

            if (show.EpisodeGroups?.Results is not null)
            {
                var absoluteGroup = show.EpisodeGroups.Results
                    .FirstOrDefault(g => g.Type == TvGroupType.Absolute);

                if (absoluteGroup is not null)
                {
                    var groupDetails = await _tmdbClient.GetTvEpisodeGroupsAsync(
                        absoluteGroup.Id, cancellationToken: cancellationToken);

                    if (groupDetails?.Groups is not null)
                    {
                        var counter = 0;
                        foreach (var group in groupDetails.Groups.OrderBy(g => g.Order))
                        {
                            foreach (var ep in group.Episodes.OrderBy(e => e.Order))
                            {
                                counter++;
                                if (counter == absoluteNumber)
                                {
                                    return (ep.SeasonNumber, ep.EpisodeNumber);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving absolute episode {AbsoluteNumber} for TMDb serie {ProviderId}", absoluteNumber, providerId);
        }

        // Fallback: assume season 1
        return (1, absoluteNumber);
    }

    private MetadataSearchResult MapToSearchResult(int id, string name, DateTime? firstAirDate, string posterPath, string overview)
    {
        var posterUrl = !string.IsNullOrEmpty(posterPath)
            ? _tmdbClient.GetImageUrl("w500", posterPath, true)?.ToString()
            : null;

        return new MetadataSearchResult
        {
            Provider = ProviderName,
            ExternalId = id.ToString(),
            Title = name,
            Year = firstAirDate?.Year,
            PosterUrl = posterUrl,
            Overview = overview
        };
    }

    private List<ExternalId> BuildExternalIds(string tmdbId, ExternalIdsTvShow? externalIds)
    {
        var ids = new List<ExternalId>
        {
            new() { ProviderName = "tmdb", Value = tmdbId }
        };

        if (!string.IsNullOrEmpty(externalIds?.ImdbId))
        {
            ids.Add(new ExternalId { ProviderName = "imdb", Value = externalIds.ImdbId });
        }

        if (!string.IsNullOrEmpty(externalIds?.TvdbId))
        {
            ids.Add(new ExternalId { ProviderName = "tvdb", Value = externalIds.TvdbId });
        }

        return ids;
    }

    private List<MetadataPicture> FetchMetadataPictures(TMDbLib.Objects.General.Images? images, string language)
    {
        var pictures = new List<MetadataPicture>();
        if (images is null) return pictures;

        var bestBackdrop = images.Backdrops?
            .OrderByDescending(b => b.Iso_639_1 is null)
            .ThenByDescending(b => b.VoteAverage)
            .FirstOrDefault();

        var bestLogo = images.Logos?
            .OrderByDescending(b => b.Iso_639_1 == language)
            .ThenByDescending(x => x.Iso_639_1 == "en")
            .ThenByDescending(b => b.VoteAverage)
            .FirstOrDefault();

        var bestPoster = images.Posters?
            .OrderByDescending(b => b.Iso_639_1 == language)
            .ThenByDescending(x => x.Iso_639_1 == "en")
            .ThenByDescending(b => b.VoteAverage)
            .FirstOrDefault();

        if (bestBackdrop is not null)
        {
            var uri = _tmdbClient.GetImageUrl("original", bestBackdrop.FilePath, true);
            if (uri is not null)
            {
                pictures.Add(new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Backdrop });
            }
        }

        if (bestLogo is not null)
        {
            var uri = _tmdbClient.GetImageUrl("original", bestLogo.FilePath, true);
            if (uri is not null)
            {
                pictures.Add(new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Logo });
            }
        }

        if (bestPoster is not null)
        {
            var uri = _tmdbClient.GetImageUrl("original", bestPoster.FilePath, true);
            if (uri is not null)
            {
                pictures.Add(new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Poster });
            }
        }

        foreach (var picture in pictures)
        {
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
        }

        return pictures;
    }

    private async Task<IList<BasePersonRole>> ConvertToPersonRolesAsync(Credits? credits, string language, CancellationToken cancellationToken)
    {
        var roles = new List<BasePersonRole>();
        if (credits is null) return roles;

        foreach (var role in credits.Cast)
        {
            var tmdbPerson = await _tmdbClient.GetPersonAsync(role.Id, language, cancellationToken: cancellationToken);
            var actor = new Actor
            {
                Order = role.Order,
                CharacterName = role.Character,
                ExternalIds =
                [
                    new ExternalId { ProviderName = "tmdb", Value = role.CreditId ?? $"{role.Id}:{role.Order}:{role.Character}" }
                ],
                Person = ConvertToPerson(tmdbPerson)
            };

            if (!string.IsNullOrEmpty(role.ProfilePath))
            {
                var uri = _tmdbClient.GetImageUrl("original", role.ProfilePath, true);
                if (uri is not null && uri != actor.Person.PortraitPicture?.OriginalRemoteUri)
                {
                    actor.PortraitPicture = new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Portrait };
                    actor.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(actor.PortraitPicture));
                }
            }

            roles.Add(actor);
        }

        foreach (var role in credits.Crew.Where(x => _wantedCrewRoles.Contains((x.Department, x.Job))))
        {
            var tmdbPerson = await _tmdbClient.GetPersonAsync(role.Id, language, cancellationToken: cancellationToken);
            var crewMember = new CrewMember
            {
                Department = role.Department,
                Job = role.Job,
                ExternalIds =
                [
                    new ExternalId { ProviderName = "tmdb", Value = role.CreditId }
                ],
                Person = ConvertToPerson(tmdbPerson)
            };

            if (!string.IsNullOrEmpty(role.ProfilePath))
            {
                var uri = _tmdbClient.GetImageUrl("original", role.ProfilePath, true);
                if (uri is not null && uri != crewMember.Person.PortraitPicture?.OriginalRemoteUri)
                {
                    crewMember.PortraitPicture = new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Portrait };
                    crewMember.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(crewMember.PortraitPicture));
                }
            }

            roles.Add(crewMember);
        }

        PersonRoleImportHelper.DedupByTmdbCreditId(roles);
        return roles;
    }

    private Person ConvertToPerson(TMDbLib.Objects.People.Person tmdbPerson)
    {
        var person = new Person
        {
            Biography = tmdbPerson.Biography,
            Birthday = tmdbPerson.Birthday.HasValue ? DateOnly.FromDateTime(tmdbPerson.Birthday.Value) : null,
            BirthPlace = tmdbPerson.PlaceOfBirth,
            Gender = tmdbPerson.Gender switch
            {
                TMDbLib.Objects.People.PersonGender.Female => PersonGender.Female,
                TMDbLib.Objects.People.PersonGender.Male => PersonGender.Male,
                TMDbLib.Objects.People.PersonGender.NonBinary => PersonGender.NonBinary,
                _ => PersonGender.NotSpecified,
            },
            Name = tmdbPerson.Name,
            Deathday = tmdbPerson.Deathday.HasValue ? DateOnly.FromDateTime(tmdbPerson.Deathday.Value) : null,
            ExternalIds =
            [
                new ExternalId { ProviderName = "tmdb", Value = tmdbPerson.Id.ToString() }
            ]
        };

        if (!string.IsNullOrEmpty(tmdbPerson.ImdbId))
        {
            person.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = tmdbPerson.ImdbId });
        }

        if (!string.IsNullOrEmpty(tmdbPerson.ProfilePath))
        {
            var uri = _tmdbClient.GetImageUrl("original", tmdbPerson.ProfilePath, true);
            if (uri is not null)
            {
                person.PortraitPicture = new MetadataPicture { OriginalRemoteUri = uri, Type = MetadataPictureType.Portrait };
                person.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(person.PortraitPicture));
            }
        }

        return person;
    }

    private static string? ExtractContentRating(ResultContainer<ContentRating>? contentRatings, string language)
    {
        if (contentRatings?.Results is null) return null;

        var langUpper = language.Length >= 2 ? language[..2].ToUpperInvariant() : language.ToUpperInvariant();

        var countries = new[] { langUpper, "US" };
        foreach (var iso in countries)
        {
            var rating = contentRatings.Results
                .FirstOrDefault(r => string.Equals(r.Iso_3166_1, iso, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(rating?.Rating)) return rating.Rating;
        }

        return contentRatings.Results
            .Select(r => r.Rating)
            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
    }

    public bool SupportsMediaType(MediaType mediaType) => mediaType is MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    public async Task<IReadOnlyList<ProviderImageDto>> GetImagesAsync(ImageProviderContext context, CancellationToken cancellationToken = default)
    {
        var results = new List<ProviderImageDto>();
        var tmdbId = await ResolveTmdbIdAsync(context.ProviderId, cancellationToken);

        try
        {
            switch (context.MediaType)
            {
                case MediaType.Serie:
                    await FetchShowImagesAsync(tmdbId, context.Language, results, cancellationToken);
                    break;
                case MediaType.SerieSeason when context.SeasonNumber.HasValue:
                    await FetchSeasonImagesAsync(tmdbId, context.SeasonNumber.Value, context.Language, results, cancellationToken);
                    break;
                case MediaType.SerieEpisode when context.SeasonNumber.HasValue && context.EpisodeNumber.HasValue:
                    await FetchEpisodeImagesAsync(tmdbId, context.SeasonNumber.Value, context.EpisodeNumber.Value, context.Language, results, cancellationToken);
                    break;
            }
        }
        catch (Exception)
        {
            // Provider unavailable - return empty list
        }

        return results;
    }

    private async Task FetchShowImagesAsync(int tmdbId, string language, List<ProviderImageDto> results, CancellationToken cancellationToken)
    {
        var show = await _tmdbClient.GetTvShowAsync(tmdbId,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvShowMethods.Images,
            cancellationToken: cancellationToken);

        if (show?.Images is null)
            return;

        AddImages(results, show.Images.Posters, MetadataPictureType.Poster, "w300");
        AddImages(results, show.Images.Backdrops, MetadataPictureType.Backdrop, "w780");
        AddImages(results, show.Images.Logos, MetadataPictureType.Logo, "w300");
    }

    private async Task FetchSeasonImagesAsync(int tmdbId, int seasonNumber, string language, List<ProviderImageDto> results, CancellationToken cancellationToken)
    {
        var season = await _tmdbClient.GetTvSeasonAsync(tmdbId, seasonNumber,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvSeasonMethods.Images,
            cancellationToken: cancellationToken);

        if (season?.Images is null)
            return;

        AddImages(results, season.Images.Posters, MetadataPictureType.Poster, "w300");
    }

    private async Task FetchEpisodeImagesAsync(int tmdbId, int seasonNumber, int episodeNumber, string language, List<ProviderImageDto> results, CancellationToken cancellationToken)
    {
        var episode = await _tmdbClient.GetTvEpisodeAsync(tmdbId, seasonNumber, episodeNumber,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvEpisodeMethods.Images,
            cancellationToken: cancellationToken);

        if (episode?.Images is null)
            return;

        AddImages(results, episode.Images.Stills, MetadataPictureType.Still, "w780");
    }

    private void AddImages(List<ProviderImageDto> results, IEnumerable<ImageData>? images, MetadataPictureType type, string thumbSize)
    {
        if (images is null)
            return;

        foreach (var img in images.OrderByDescending(x => x.VoteAverage))
        {
            var url = _tmdbClient.GetImageUrl("original", img.FilePath, true)?.ToString();
            var thumbUrl = _tmdbClient.GetImageUrl(thumbSize, img.FilePath, true)?.ToString();
            if (url is null || thumbUrl is null) continue;

            results.Add(new ProviderImageDto
            {
                Url = url,
                ThumbnailUrl = thumbUrl,
                Type = type,
                Width = img.Width,
                Height = img.Height,
                VoteAverage = img.VoteAverage,
                Language = img.Iso_639_1
            });
        }
    }

    private async Task<int> ResolveTmdbIdAsync(string providerId, CancellationToken cancellationToken)
    {
        if (int.TryParse(providerId, out var tmdbId))
            return tmdbId;

        // Assume it's an IMDb ID (tt...) - resolve via TMDb Find API
        var findResult = await _tmdbClient.FindAsync(TMDbLib.Objects.Find.FindExternalSource.Imdb, providerId, cancellationToken: cancellationToken);
        var tvResult = findResult?.TvResults?.FirstOrDefault()
            ?? throw new InvalidOperationException($"No TMDb series found for external ID '{providerId}'");

        return tvResult.Id;
    }
}
