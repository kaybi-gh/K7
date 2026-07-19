using System.Collections.Frozen;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using TMDbLib.Client;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.Movies;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;
public class TMDbMetadataProvider : IMetadataProvider<ExternalMovieMetadata>, ISearchableMetadataProvider, IMetadataProviderInfo, IPersonMetadataProvider, IMetadataImageProvider, IPersonImageProvider
{
    public string ProviderName => "tmdb";
    public IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; } = [LibraryMediaType.Movie, LibraryMediaType.Serie];
    private readonly TMDbClient _tdmbClient;

    private readonly FrozenSet<(string Department, string Job)> _wantedCrewRoles = new List<(string, string)>
    {
        ("Production", "Producer"),
        ("Production", "Executive Producer"),
        ("Directing", "Director"),
        ("Writing", "Characters"),
        ("Writing", "Story"),
        ("Writing", "Screenplay")
    }.ToFrozenSet();

    public TMDbMetadataProvider(TMDbClient tmdbClient)
    {
        _tdmbClient = tmdbClient;
    }

    public async Task<string?> SearchAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        try
        {
            var searchResult = await _tdmbClient.SearchMovieAsync(movieIdentification.Title,
                year: movieIdentification.ReleaseYear.HasValue ? movieIdentification.ReleaseYear.Value.Year : 0,
                cancellationToken: cancellationToken);
            var year = movieIdentification.ReleaseYear?.Year;
            var bestMatch = MetadataTitleMatchHelper.PickBest(
                movieIdentification.Title,
                year,
                searchResult.Results,
                result => result.Title,
                result => result.ReleaseDate?.Year,
                result => [result.OriginalTitle]);
            return bestMatch?.Id.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching TMDbId for {movieIdentification}: {ex.Message}");
            return null;
        }
    }

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year, string? providerId, MediaType? mediaType, string language, string? fallbackLanguage, CancellationToken cancellationToken)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        if (mediaType.HasValue && mediaType != K7.Server.Domain.Enums.MediaType.Movie)
            return [];

        var results = new List<MetadataSearchResult>();

        try
        {
            var trimmedProviderId = providerId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedProviderId))
            {
                var tmdbId = await ResolveMovieTmdbIdAsync(trimmedProviderId, cancellationToken);
                var movie = await _tdmbClient.GetMovieAsync(tmdbId.ToString(), language, cancellationToken: cancellationToken);
                if (movie != null)
                {
                    results.Add(MapToSearchResult(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterPath, movie.Overview));
                }

                return results;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResult = await _tdmbClient.SearchMovieAsync(query, language: language, year: year ?? 0, cancellationToken: cancellationToken);
                if (searchResult?.Results != null)
                {
                    var ranked = MetadataTitleMatchHelper.OrderByBestMatch(
                        query,
                        year,
                        searchResult.Results,
                        movie => movie.Title,
                        movie => movie.ReleaseDate?.Year,
                        movie => [movie.OriginalTitle]);
                    results.AddRange(ranked.Select(movie =>
                        MapToSearchResult(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterPath, movie.Overview)));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while searching TMDb for {query}: {ex.Message}");
        }

        return results;
    }

    private MetadataSearchResult MapToSearchResult(int id, string title, DateTime? releaseDate, string posterPath, string overview)
    {
        var posterUrl = !string.IsNullOrEmpty(posterPath) 
            ? _tdmbClient.GetImageUrl("w500", posterPath, true)?.ToString() 
            : null;

        return new MetadataSearchResult
        {
            Provider = ProviderName,
            ExternalId = id.ToString(),
            Title = title,
            Year = releaseDate?.Year,
            PosterUrl = posterUrl,
            Overview = overview
        };
    }

    public async Task<ExternalMovieMetadata> FetchMetadata(string metadataProviderExternalId, string language, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        try
        {
            var tmdbMovie = await _tdmbClient.GetMovieAsync(metadataProviderExternalId, language, includeImageLanguage: $"{language},en,null", extraMethods: MovieMethods.ExternalIds | MovieMethods.Credits | MovieMethods.Images | MovieMethods.ReleaseDates | MovieMethods.Videos | MovieMethods.Recommendations, cancellationToken: cancellationToken);

            var contentRating = ExtractContentRating(tmdbMovie.ReleaseDates, language);

            var movie = new ExternalMovieMetadata
            {
                Title = tmdbMovie.Title,
                SortTitle = MediaSortTitleHelper.Compute(tmdbMovie.Title),
                OriginalTitle = tmdbMovie.OriginalTitle,
                ReleaseDate = tmdbMovie.ReleaseDate.HasValue ? DateOnly.FromDateTime(tmdbMovie.ReleaseDate.Value) : null,
                Genres = [.. tmdbMovie.Genres.Select(g => g.Name)],
                Studios = [.. tmdbMovie.ProductionCompanies?.Select(c => c.Name) ?? []],
                OriginalLanguage = tmdbMovie.OriginalLanguage,
                Overview = tmdbMovie.Overview,
                Tagline = tmdbMovie.Tagline,
                ContentRating = contentRating,
                Budget = tmdbMovie.Budget > 0 ? tmdbMovie.Budget : null,
                Revenue = tmdbMovie.Revenue > 0 ? tmdbMovie.Revenue : null,
                Trailers = [.. tmdbMovie.Videos?.Results?
                    .Where(v => v.Site == "YouTube" && v.Type is "Trailer" or "Teaser")
                    .Select(v => new TrailerInfo
                    {
                        Key = v.Key,
                        Name = v.Name,
                        Site = v.Site,
                        Type = v.Type,
                        Language = v.Iso_639_1
                    }) ?? []],
                RecommendedExternalIds = [.. tmdbMovie.Recommendations?.Results?.Select(r => r.Id.ToString()) ?? []],
                PersonRoles = await ConvertToPersonRolesAsync(tmdbMovie.Credits, language),
                ExternalIds =
                [
                    new ExternalId()
                    {
                        ProviderName = "tmdb",
                        Value = metadataProviderExternalId
                    }
                ],
                Pictures = FetchMetadataPictures(tmdbMovie.Images, language),
                Ratings = tmdbMovie.VoteCount > 0
                    ? [new MetadataProviderRating { MetadataProvider = Domain.Enums.MetadataProvider.TMDb, Value = tmdbMovie.VoteAverage, MinimumValue = 0, MaximumValue = 10, RatingCount = tmdbMovie.VoteCount }]
                    : []
            };

            if (!string.IsNullOrEmpty(tmdbMovie.ImdbId))
            {
                movie.ExternalIds.Add(new ExternalId()
                {
                    ProviderName = "imdb",
                    Value = tmdbMovie.ImdbId
                });
            }

            return movie;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while fetching movie metadata for TMDbId {metadataProviderExternalId}: {ex.Message}");
        }
    }

    private List<MetadataPicture> FetchMetadataPictures(TMDbLib.Objects.General.Images images, string language)
    {
        List<MetadataPicture> metadataPictures = [];
        if (images != null)
        {
            var bestBackdrop = images.Backdrops.OrderByDescending(b => b.Iso_639_1 is null).ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestLogo = images.Logos.OrderByDescending(b => b.Iso_639_1 == language).ThenByDescending(x => x.Iso_639_1 == "en").ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestPoster = images.Posters.OrderByDescending(b => b.Iso_639_1 == language).ThenByDescending(x => x.Iso_639_1 == "en").ThenByDescending(b => b.VoteAverage).FirstOrDefault();

            if (bestBackdrop != null)
            {
                var uri = _tdmbClient.GetImageUrl("original", bestBackdrop.FilePath);
                if (uri != null)
                {
                    var metadataPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestBackdrop.FilePath, true),
                        Type = MetadataPictureType.Backdrop
                    };
                    metadataPictures.Add(metadataPicture);
                }
            }

            if (bestLogo != null)
            {
                var uri = _tdmbClient.GetImageUrl("original", bestLogo.FilePath);
                if (uri != null)
                {
                    var metadataPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestLogo.FilePath, true),
                        Type = MetadataPictureType.Logo
                    };
                    metadataPictures.Add(metadataPicture);
                }
            }

            if (bestPoster != null)
            {
                var uri = _tdmbClient.GetImageUrl("original", bestPoster.FilePath);
                if (uri != null)
                {
                    var metadataPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestPoster.FilePath, true),
                        Type = MetadataPictureType.Poster
                    };
                    metadataPictures.Add(metadataPicture);
                }
            }
        }

        foreach (var picture in metadataPictures)
        {
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
        }

        return metadataPictures;
    }

    private async Task<IList<BasePersonRole>> ConvertToPersonRolesAsync(Credits credits, string language)
    {
        var roles = new List<BasePersonRole>();
        foreach (var role in credits.Cast)
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(role.Id, language);
            var actor = new Actor()
            {
                Order = role.Order,
                CharacterName = role.Character,
                ExternalIds =
                [
                    new ExternalId()
                    {
                        ProviderName = "tmdb",
                        Value = role.CastId.ToString()
                    }
                ],
                Person = ConvertToPerson(imdbPerson)
            };

            if (!string.IsNullOrEmpty(role.ProfilePath))
            {
                var uri = _tdmbClient.GetImageUrl("original", role.ProfilePath, true);
                if (uri != null && uri != actor.Person.PortraitPicture?.OriginalRemoteUri)
                {
                    actor.PortraitPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = uri,
                        Type = MetadataPictureType.Portrait
                    };
                    actor.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(actor.PortraitPicture));
                }
            }
            roles.Add(actor);
        }

        foreach (var role in credits.Crew.Where(x => _wantedCrewRoles.Contains((x.Department, x.Job))))
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(role.Id, language);
            var crewMember = new CrewMember()
            {
                Department = role.Department,
                Job = role.Job,
                ExternalIds =
                [
                    new ExternalId()
                    {
                        ProviderName = "tmdb",
                        Value = role.CreditId
                    }
                ],
                Person = ConvertToPerson(imdbPerson)
            };

            if (!string.IsNullOrEmpty(role.ProfilePath))
            {
                var uri = _tdmbClient.GetImageUrl("original", role.ProfilePath, true);
                if (uri != null && uri != crewMember.Person.PortraitPicture?.OriginalRemoteUri)
                {
                    crewMember.PortraitPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = uri,
                        Type = MetadataPictureType.Portrait
                    };
                    crewMember.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(crewMember.PortraitPicture));
                }
            }
            roles.Add(crewMember);
        }

        PersonRoleImportHelper.DedupByTmdbCreditId(roles);
        return roles;
    }

    public async Task<ExternalPersonDetails?> FetchPersonAsync(string providerId, string language, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        if (!int.TryParse(providerId, out var tmdbId)) return null;

        try
        {
            var tmdbPerson = await _tdmbClient.GetPersonAsync(tmdbId, language, cancellationToken: cancellationToken);
            if (tmdbPerson is null) return null;

            var imageUrl = !string.IsNullOrEmpty(tmdbPerson.ProfilePath)
                ? _tdmbClient.GetImageUrl("original", tmdbPerson.ProfilePath, true)?.ToString()
                : null;

            var additionalIds = new List<ExternalIdEntry>();
            if (!string.IsNullOrEmpty(tmdbPerson.ImdbId))
                additionalIds.Add(new ExternalIdEntry("imdb", tmdbPerson.ImdbId));

            return new ExternalPersonDetails
            {
                Biography = tmdbPerson.Biography,
                Birthday = tmdbPerson.Birthday.HasValue ? DateOnly.FromDateTime(tmdbPerson.Birthday.Value) : null,
                Deathday = tmdbPerson.Deathday.HasValue ? DateOnly.FromDateTime(tmdbPerson.Deathday.Value) : null,
                BirthPlace = tmdbPerson.PlaceOfBirth,
                Gender = tmdbPerson.Gender switch
                {
                    TMDbLib.Objects.People.PersonGender.Female => PersonGender.Female,
                    TMDbLib.Objects.People.PersonGender.Male => PersonGender.Male,
                    TMDbLib.Objects.People.PersonGender.NonBinary => PersonGender.NonBinary,
                    _ => PersonGender.NotSpecified,
                },
                ImageUrl = imageUrl,
                AdditionalExternalIds = additionalIds
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Person ConvertToPerson(TMDbLib.Objects.People.Person tmdbPerson)
    {
        var person = new Person()
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
                new ExternalId()
                {
                    ProviderName = "tmdb",
                    Value = tmdbPerson.Id.ToString()
                }
            ]
        };

        if (!string.IsNullOrEmpty(tmdbPerson.ImdbId))
        {
            person.ExternalIds.Add(new ExternalId()
            {
                ProviderName = "imdb",
                Value = tmdbPerson.ImdbId
            });
        }

        if (!string.IsNullOrEmpty(tmdbPerson.ProfilePath))
        {
            var uri = _tdmbClient.GetImageUrl("original", tmdbPerson.ProfilePath, true);
            if (uri != null)
            {
                person.PortraitPicture = new MetadataPicture()
                {
                    OriginalRemoteUri = uri,
                    Type = MetadataPictureType.Portrait
                };
                person.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(person.PortraitPicture));
            }
        }

        return person;
    }

    private static string? ExtractContentRating(TMDbLib.Objects.General.ResultContainer<TMDbLib.Objects.Movies.ReleaseDatesContainer>? releaseDates, string language)
    {
        if (releaseDates?.Results is null) return null;

        var langUpper = language.Length >= 2 ? language[..2].ToUpperInvariant() : language.ToUpperInvariant();

        // Try requested language country first, then US as fallback
        var countries = new[] { langUpper, "US" };
        foreach (var iso in countries)
        {
            var country = releaseDates.Results.FirstOrDefault(r =>
                string.Equals(r.Iso_3166_1, iso, StringComparison.OrdinalIgnoreCase));
            var cert = country?.ReleaseDates?
                .Select(r => r.Certification)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            if (cert is not null) return cert;
        }

        // Fallback: any certification from any country
        return releaseDates.Results
            .SelectMany(r => r.ReleaseDates ?? [])
            .Select(r => r.Certification)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    public bool SupportsMediaType(MediaType mediaType) => mediaType is MediaType.Movie;

    public async Task<IReadOnlyList<ProviderImageDto>> GetImagesAsync(ImageProviderContext context, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        var results = new List<ProviderImageDto>();

        try
        {
            var movie = await _tdmbClient.GetMovieAsync(context.ProviderId, context.Language,
                includeImageLanguage: $"{context.Language},en,null",
                extraMethods: MovieMethods.Images,
                cancellationToken: cancellationToken);

            if (movie?.Images is null)
                return results;

            foreach (var img in movie.Images.Posters.OrderByDescending(x => x.VoteAverage))
            {
                var url = _tdmbClient.GetImageUrl("original", img.FilePath, true)?.ToString();
                var thumbUrl = _tdmbClient.GetImageUrl("w300", img.FilePath, true)?.ToString();
                if (url is null || thumbUrl is null) continue;

                results.Add(new ProviderImageDto
                {
                    Url = url,
                    ThumbnailUrl = thumbUrl,
                    Type = MetadataPictureType.Poster,
                    Width = img.Width,
                    Height = img.Height,
                    VoteAverage = img.VoteAverage,
                    Language = img.Iso_639_1
                });
            }

            foreach (var img in movie.Images.Backdrops.OrderByDescending(x => x.VoteAverage))
            {
                var url = _tdmbClient.GetImageUrl("original", img.FilePath, true)?.ToString();
                var thumbUrl = _tdmbClient.GetImageUrl("w780", img.FilePath, true)?.ToString();
                if (url is null || thumbUrl is null) continue;

                results.Add(new ProviderImageDto
                {
                    Url = url,
                    ThumbnailUrl = thumbUrl,
                    Type = MetadataPictureType.Backdrop,
                    Width = img.Width,
                    Height = img.Height,
                    VoteAverage = img.VoteAverage,
                    Language = img.Iso_639_1
                });
            }

            foreach (var img in movie.Images.Logos.OrderByDescending(x => x.VoteAverage))
            {
                var url = _tdmbClient.GetImageUrl("original", img.FilePath, true)?.ToString();
                var thumbUrl = _tdmbClient.GetImageUrl("w300", img.FilePath, true)?.ToString();
                if (url is null || thumbUrl is null) continue;

                results.Add(new ProviderImageDto
                {
                    Url = url,
                    ThumbnailUrl = thumbUrl,
                    Type = MetadataPictureType.Logo,
                    Width = img.Width,
                    Height = img.Height,
                    VoteAverage = img.VoteAverage,
                    Language = img.Iso_639_1
                });
            }
        }
        catch (Exception)
        {
            // Provider unavailable - return empty list
        }

        return results;
    }

    public async Task<IReadOnlyList<ProviderImageDto>> GetPersonImagesAsync(string providerId, string language, CancellationToken cancellationToken = default)
    {
        await TmdbClientConfiguration.EnsureConfiguredAsync(_tdmbClient, cancellationToken);
        var results = new List<ProviderImageDto>();

        if (!int.TryParse(providerId, out var tmdbId))
            return results;

        try
        {
            var person = await _tdmbClient.GetPersonAsync(tmdbId,
                TMDbLib.Objects.People.PersonMethods.Images,
                cancellationToken: cancellationToken);

            if (person?.Images?.Profiles is null)
                return results;

            foreach (var img in person.Images.Profiles.OrderByDescending(x => x.VoteAverage))
            {
                var url = _tdmbClient.GetImageUrl("original", img.FilePath, true)?.ToString();
                var thumbUrl = _tdmbClient.GetImageUrl("w185", img.FilePath, true)?.ToString();
                if (url is null || thumbUrl is null) continue;

                results.Add(new ProviderImageDto
                {
                    Url = url,
                    ThumbnailUrl = thumbUrl,
                    Type = MetadataPictureType.Portrait,
                    Width = img.Width,
                    Height = img.Height,
                    VoteAverage = img.VoteAverage,
                    Language = img.Iso_639_1
                });
            }
        }
        catch (Exception)
        {
            // Provider unavailable
        }

        return results;
    }

    private async Task<int> ResolveMovieTmdbIdAsync(string providerId, CancellationToken cancellationToken)
    {
        var trimmed = providerId.Trim();
        if (int.TryParse(trimmed, out var tmdbId))
            return tmdbId;

        var findResult = await _tdmbClient.FindAsync(FindExternalSource.Imdb, trimmed, cancellationToken: cancellationToken);
        var movieResult = findResult?.MovieResults?.FirstOrDefault()
            ?? throw new InvalidOperationException($"No TMDb movie found for external ID '{trimmed}'");

        return movieResult.Id;
    }
}
