using System.Collections.Frozen;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using TMDbLib.Client;
using TMDbLib.Objects.Movies;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;
public class TMDbMetadataProvider : IMetadataProvider<ExternalMovieMetadata>, ISearchableMetadataProvider, IMetadataProviderInfo
{
    private const string Token = "8e7586ad850237f5d506d8789f4c3936";
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

    public TMDbMetadataProvider()
    {
        _tdmbClient = new(Token);
        _tdmbClient.SetConfig(_tdmbClient.GetConfigAsync().Result);
    }

    public async Task<string?> SearchAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchResult = await _tdmbClient.SearchMovieAsync(movieIdentification.Title,
                year: movieIdentification.ReleaseYear.HasValue ? movieIdentification.ReleaseYear.Value.Year : 0,
                cancellationToken: cancellationToken);
            return searchResult.Results.FirstOrDefault()?.Id.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching TMDbId for {movieIdentification}: {ex.Message}");
            return null;
        }
    }

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year, string? providerId, CancellationToken cancellationToken)
    {
        var results = new List<MetadataSearchResult>();

        try
        {
            if (!string.IsNullOrWhiteSpace(providerId) && int.TryParse(providerId, out var tmdbId))
            {
                var movie = await _tdmbClient.GetMovieAsync(tmdbId, "fr", cancellationToken: cancellationToken);
                if (movie != null)
                {
                    results.Add(MapToSearchResult(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterPath, movie.Overview));
                }
                return results;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResult = await _tdmbClient.SearchMovieAsync(query, language: "fr", year: year ?? 0, cancellationToken: cancellationToken);
                if (searchResult?.Results != null)
                {
                    results.AddRange(searchResult.Results.Select(movie => 
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
        try
        {
            var tmdbMovie = await _tdmbClient.GetMovieAsync(metadataProviderExternalId, language, includeImageLanguage: "fr,en,null", extraMethods: MovieMethods.ExternalIds | MovieMethods.Credits | MovieMethods.Images | MovieMethods.ReleaseDates, cancellationToken: cancellationToken);

            var contentRating = ExtractContentRating(tmdbMovie.ReleaseDates, language);

            var movie = new ExternalMovieMetadata
            {
                Title = tmdbMovie.Title,
                OriginalTitle = tmdbMovie.OriginalTitle,
                ReleaseDate = tmdbMovie.ReleaseDate.HasValue ? DateOnly.FromDateTime(tmdbMovie.ReleaseDate.Value) : null,
                Genres = [.. tmdbMovie.Genres.Select(g => g.Name)],
                OriginalLanguage = tmdbMovie.OriginalLanguage,
                Overview = tmdbMovie.Overview,
                Tagline = tmdbMovie.Tagline,
                ContentRating = contentRating,
                Budget = tmdbMovie.Budget > 0 ? tmdbMovie.Budget : null,
                Revenue = tmdbMovie.Revenue > 0 ? tmdbMovie.Revenue : null,
                PersonRoles = await ConvertToPersonRolesAsync(tmdbMovie.Credits),
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

    private async Task<IList<BasePersonRole>> ConvertToPersonRolesAsync(Credits credits)
    {
        var roles = new List<BasePersonRole>();
        foreach (var role in credits.Cast)
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(role.Id);
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
            var imdbPerson = await _tdmbClient.GetPersonAsync(role.Id);
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

        var groupedRoles = roles.GroupBy(x => new { x.Person.Name, x.Person.Birthday });
        foreach (var group in groupedRoles.Where(x => x.Count() > 1))
        {
            var duplicateRoles = group.OrderBy(x => x.ExternalIds.First(x => x.ProviderName == "tmdb").Value).Skip(1);
            roles.RemoveAll(duplicateRoles.Contains);
        }
        return roles;
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
}
