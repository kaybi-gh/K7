using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Interfaces;
using MediaServer.Domain.ValueObjects;
using MediaServer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;

namespace MediaServer.Application.Services;
public class TMDbMetadataProvider : IMovieMetadataProvider
{
    private const string Token = "8e7586ad850237f5d506d8789f4c3936";
    private readonly TMDbClient _tdmbClient;
    private readonly PathsConfiguration _pathsConfiguration;

    public TMDbMetadataProvider(IOptions<PathsConfiguration> pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration.Value;
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
            return await TryDownloadPicturesAsync(images, metadataId, language);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching media pictures for metadataId {metadataId} (TMDbId {metadataProviderExternalId}): {ex.Message}");
            return [];
        }
    }

    private async Task<IList<MetadataPicture>> TryDownloadPicturesAsync(Images images, Guid metadataId, string preferredLanguage)
    {
        List<MetadataPicture> mediaPictures = [];
        if (images != null)
        {
            var bestBackdrop = images.Backdrops.OrderBy(b => b.Iso_639_1 == preferredLanguage).ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestLogo = images.Logos.OrderBy(b => b.Iso_639_1 == preferredLanguage).ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestPoster = images.Posters.OrderBy(b => b.Iso_639_1 == preferredLanguage).ThenByDescending(b => b.VoteAverage).FirstOrDefault();

            if (bestBackdrop != null)
            {
                var distantFilepath = new FileInfo(bestBackdrop.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, metadataId.ToString(), $"{Guid.NewGuid()}{distantFilepath.Extension}");
                if (await TryDownloadPictureAsync(bestBackdrop.FilePath, filePath))
                {
                    mediaPictures.Add(new MetadataPicture()
                    {
                        MetadataId = metadataId,
                        Path = filePath,
                        Type = MetadataPictureType.Backdrop
                    });
                }
            }

            if (bestLogo != null)
            {
                var distantFilepath = new FileInfo(bestLogo.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, metadataId.ToString(), $"{Guid.NewGuid()}{distantFilepath.Extension}");
                if (await TryDownloadPictureAsync(bestLogo.FilePath, filePath))
                {
                    mediaPictures.Add(new MetadataPicture()
                    {
                        MetadataId = metadataId,
                        Path = filePath,
                        Type = MetadataPictureType.Logo
                    });
                }
            }

            if (bestPoster != null)
            {
                var distantFilepath = new FileInfo(bestPoster.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, metadataId.ToString(), $"{Guid.NewGuid()}{distantFilepath.Extension}");
                if (await TryDownloadPictureAsync(bestPoster.FilePath, filePath))
                {
                    mediaPictures.Add(new MetadataPicture()
                    {
                        MetadataId = metadataId,
                        Path = filePath,
                        Type = MetadataPictureType.Poster
                    });
                }
            }
        }
        return mediaPictures;
    }

    private async Task<bool> TryDownloadPictureAsync(string uri, string targetPath)
    {
        try
        {
            var bytes = await _tdmbClient.GetImageBytesAsync("original", uri);
            FileInfo file = new(targetPath);
            file.Directory!.Create();
            File.WriteAllBytes(targetPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IList<BasePersonRole>> ConvertToPersonRolesAsync(Credits credits)
    {
        var roles = new List<BasePersonRole>();
        foreach (var item in credits.Cast.OrderBy(x => x.Order))
        {
            var imdbPerson = await _tdmbClient.GetPersonAsync(item.Id);
            var personRoleId = Guid.NewGuid();
            var actor = new Actor()
            {
                Id = personRoleId,
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
                Person = await ConvertToPerson(imdbPerson),
                Job = PersonJob.Actor
            };

            if (!string.IsNullOrEmpty(item.ProfilePath))
            {
                var distantFilepath = new FileInfo(item.ProfilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, "personRole", $"{actor.Id}", $"{Guid.NewGuid()}{distantFilepath.Extension}");
                if (await TryDownloadPictureAsync(item.ProfilePath, filePath))
                {
                    actor.PortraitPicture = new MetadataPicture()
                    {
                        PersonRoleId = actor.Id,
                        Path = filePath,
                        Type = MetadataPictureType.Portrait
                    };
                }
            }
            roles.Add(actor);
        }

        foreach (var item in credits.Crew)
        {
            // TODO
        }

        return roles;
    }

    private async Task<Person> ConvertToPerson(TMDbLib.Objects.People.Person tmdbPerson)
    {
        var personId = Guid.NewGuid();
        var person = new Person()
        {
            Id = personId,
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
            var distantFilepath = new FileInfo(tmdbPerson.ProfilePath);
            var filePath = Path.Combine(_pathsConfiguration.Metadatas, "person", $"{person.Id}", $"{Guid.NewGuid()}{distantFilepath.Extension}");
            if (await TryDownloadPictureAsync(tmdbPerson.ProfilePath, filePath))
            {
                person.PortraitPicture = new MetadataPicture()
                {
                    PersonId = person.Id,
                    Path = filePath,
                    Type = MetadataPictureType.Portrait
                };
            }
        }

        return person;
    }
}
