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

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class TMDbSerieMetadataProvider : ISerieMetadataProvider, ISearchableMetadataProvider
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
        var tmdbId = int.Parse(providerId);
        var show = await _tmdbClient.GetTvShowAsync(
            tmdbId,
            language: language,
            includeImageLanguage: $"{language},en,null",
            extraMethods: TvShowMethods.Credits | TvShowMethods.Images | TvShowMethods.ContentRatings | TvShowMethods.ExternalIds,
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
        var tmdbId = int.Parse(providerId);
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
        var tmdbId = int.Parse(providerId);
        var episode = await _tmdbClient.GetTvEpisodeAsync(
            tmdbId,
            seasonNumber,
            episodeNumber,
            language: language,
            extraMethods: TvEpisodeMethods.Images,
            cancellationToken: cancellationToken);

        string? stillUrl = null;
        if (!string.IsNullOrEmpty(episode.StillPath))
        {
            stillUrl = _tmdbClient.GetImageUrl("original", episode.StillPath, true)?.ToString();
        }

        return new ExternalEpisodeMetadata
        {
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Title = episode.Name,
            Overview = episode.Overview,
            AirDate = episode.AirDate.HasValue ? DateOnly.FromDateTime(episode.AirDate.Value) : null,
            Runtime = episode.Runtime,
            StillImageUrl = stillUrl,
            ExternalIds = []
        };
    }

    public async Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(
        string providerId, int absoluteNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var tmdbId = int.Parse(providerId);
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
                    new ExternalId { ProviderName = "tmdb", Value = role.Id.ToString() }
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

        // Dedup persons by name + birthday
        var groupedRoles = roles.GroupBy(x => new { x.Person.Name, x.Person.Birthday });
        foreach (var group in groupedRoles.Where(x => x.Count() > 1))
        {
            var duplicateRoles = group.OrderBy(x => x.ExternalIds.First(e => e.ProviderName == "tmdb").Value).Skip(1);
            roles.RemoveAll(duplicateRoles.Contains);
        }

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
}
