using System.Collections.Frozen;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using MediaServer.Domain.ValueObjects;
using TMDbLib.Client;
using TMDbLib.Objects.Movies;

namespace MediaServer.Application.Services;
public class TMDbMetadataProvider : IMovieMetadataProvider
{
    private const string Token = "8e7586ad850237f5d506d8789f4c3936";
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

    public async Task<string?> SearchMetadataProviderExternalIdAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken)
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

    public async Task<MovieMetadata?> FetchMovieMetadata(Guid movieId, string metadataProviderExternalId, string language, CancellationToken cancellationToken)
    {
        try
        {
            var tmdbMovie = await _tdmbClient.GetMovieAsync(metadataProviderExternalId, language, extraMethods: MovieMethods.ExternalIds | MovieMethods.Credits, cancellationToken: cancellationToken);

            var movieMetadataId = Guid.NewGuid();
            var movieMetadata = new MovieMetadata
            {
                Id = movieMetadataId,
                MediaId = movieId,
                Title = tmdbMovie.Title,
                OriginalTitle = tmdbMovie.OriginalTitle,
                ReleaseDate = tmdbMovie.ReleaseDate.HasValue ? DateOnly.FromDateTime(tmdbMovie.ReleaseDate.Value) : null,
                Genres = tmdbMovie.Genres.Select(g => g.Name).ToList(),
                OriginalLanguage = tmdbMovie.OriginalLanguage,
                Overview = tmdbMovie.Overview,
                TagLine = tmdbMovie.Tagline,
                PersonRoles = await ConvertToPersonRolesAsync(tmdbMovie.Credits),
                ExternalIds =
                [
                    new ExternalId()
                    {
                        Platform = "tmdb",
                        Value = metadataProviderExternalId
                    }
                ]
            };

            if (!string.IsNullOrEmpty(tmdbMovie.ImdbId))
            {
                movieMetadata.ExternalIds.Add(new ExternalId()
                {
                    Platform = "imdb",
                    Value = tmdbMovie.ImdbId
                });
            }

            return movieMetadata;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching movie metadata for movieId {movieId} (TMDbId {metadataProviderExternalId}): {ex.Message}");
            return null;
        }
    }

    public async Task<IList<MetadataPicture>> FetchMetadataPictures(Guid metadataId, string metadataProviderExternalId, string language, CancellationToken cancellationToken, string? fallbackLanguage = "en")
    {
        try
        {
            int tmdbId = int.Parse(metadataProviderExternalId);
            var images = await _tdmbClient.GetMovieImagesAsync(tmdbId, language, cancellationToken: cancellationToken);

            if (images.Backdrops.Count == 0 || images.Logos.Count == 0 || images.Posters.Count == 0)
            {
                var fallbackLangageImages = await _tdmbClient.GetMovieImagesAsync(tmdbId, fallbackLanguage, cancellationToken: cancellationToken);
                if (images.Backdrops.Count == 0)
                    images.Backdrops = fallbackLangageImages.Backdrops;
                if (images.Logos.Count == 0)
                    images.Logos = fallbackLangageImages.Logos;
                if (images.Posters.Count == 0)
                    images.Posters = fallbackLangageImages.Posters;
            }

            List<MetadataPicture> metadataPictures = [];
            if (images != null)
            {
                var bestBackdrop = images.Backdrops.OrderBy(b => b.Iso_639_1 == language).ThenByDescending(b => b.VoteAverage).FirstOrDefault();
                var bestLogo = images.Logos.OrderBy(b => b.Iso_639_1 == language).ThenByDescending(b => b.VoteAverage).FirstOrDefault();
                var bestPoster = images.Posters.OrderBy(b => b.Iso_639_1 == language).ThenByDescending(b => b.VoteAverage).FirstOrDefault();

                if (bestBackdrop != null)
                {
                    var uri = _tdmbClient.GetImageUrl("original", bestBackdrop.FilePath);
                    if (uri != null)
                    {
                        var metadataPicture = new MetadataPicture()
                        {
                            MetadataId = metadataId,
                            OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestBackdrop.FilePath, true),
                            Type = MetadataPictureType.Backdrop
                        };
                        metadataPictures.Add(metadataPicture);
                        metadataPicture.AddDomainEvent(new MetadataPictureCreatedEvent(metadataPicture));
                    }
                }

                if (bestLogo != null)
                {
                    var uri = _tdmbClient.GetImageUrl("original", bestLogo.FilePath);
                    if (uri != null)
                    {
                        var metadataPicture = new MetadataPicture()
                        {
                            MetadataId = metadataId,
                            OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestLogo.FilePath, true),
                            Type = MetadataPictureType.Logo
                        };
                        metadataPictures.Add(metadataPicture);
                        metadataPicture.AddDomainEvent(new MetadataPictureCreatedEvent(metadataPicture));
                    }
                }

                if (bestPoster != null)
                {
                    var uri = _tdmbClient.GetImageUrl("original", bestPoster.FilePath);
                    if (uri != null)
                    {
                        var metadataPicture = new MetadataPicture()
                        {
                            MetadataId = metadataId,
                            OriginalRemoteUri = _tdmbClient.GetImageUrl("original", bestPoster.FilePath, true),
                            Type = MetadataPictureType.Poster
                        };
                        metadataPictures.Add(metadataPicture);
                        metadataPicture.AddDomainEvent(new MetadataPictureCreatedEvent(metadataPicture));
                    }
                }
            }

            foreach (var picture in metadataPictures)
            {
                picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            }

            return metadataPictures;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching media pictures for metadataId {metadataId} (TMDbId {metadataProviderExternalId}): {ex.Message}");
            return [];
        }
    }

    private async Task<IList<BasePersonRole>> ConvertToPersonRolesAsync(Credits credits)
    {
        var roles = new List<BasePersonRole>();
        foreach (var item in credits.Cast)
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(item.Id);
            var actor = new Actor()
            {
                Order = item.Order,
                CharacterName = item.Character,
                ExternalIds =
                [
                    new ExternalId()
                    {
                        Platform = "tmdb",
                        Value = item.CastId.ToString()
                    }
                ],
                Person = ConvertToPerson(imdbPerson)
            };

            if (!string.IsNullOrEmpty(item.ProfilePath))
            {
                var uri = _tdmbClient.GetImageUrl("original", item.ProfilePath);
                if (uri != null)
                {
                    actor.PortraitPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = _tdmbClient.GetImageUrl("original", item.ProfilePath, true),
                        Type = MetadataPictureType.Portrait
                    };
                    actor.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(actor.PortraitPicture));
                }
            }
            roles.Add(actor);
        }

        foreach (var item in credits.Crew.Where(x => _wantedCrewRoles.Contains((x.Department, x.Job))))
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(item.Id);
            var crewMember = new CrewMember()
            {
                Department = item.Department,
                Job = item.Job,
                ExternalIds =
                [
                    new ExternalId()
                    {
                        Platform = "tmdb",
                        Value = item.CreditId
                    }
                ],
                Person = ConvertToPerson(imdbPerson)
            };

            if (!string.IsNullOrEmpty(item.ProfilePath))
            {
                var uri = _tdmbClient.GetImageUrl("original", item.ProfilePath);
                if (uri != null)
                {
                    crewMember.PortraitPicture = new MetadataPicture()
                    {
                        OriginalRemoteUri = _tdmbClient.GetImageUrl("original", item.ProfilePath, true),
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
            var duplicateRoles = group.OrderBy(x => x.ExternalIds.First(x => x.Platform == "tmdb").Value).Skip(1);
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
                    Platform = "tmdb",
                    Value = tmdbPerson.Id.ToString()
                }
            ]
        };

        if (!string.IsNullOrEmpty(tmdbPerson.ImdbId))
        {
            person.ExternalIds.Add(new ExternalId()
            {
                Platform = "imdb",
                Value = tmdbPerson.ImdbId
            });
        }

        if (!string.IsNullOrEmpty(tmdbPerson.ProfilePath))
        {
            var uri = _tdmbClient.GetImageUrl("original", tmdbPerson.ProfilePath);
            if (uri != null)
            {
                person.PortraitPicture = new MetadataPicture()
                {
                    OriginalRemoteUri = _tdmbClient.GetImageUrl("original", tmdbPerson.ProfilePath, true),
                    Type = MetadataPictureType.Portrait
                };
                person.PortraitPicture.AddDomainEvent(new MetadataPictureCreatedEvent(person.PortraitPicture));
            }
        }

        return person;
    }
}
