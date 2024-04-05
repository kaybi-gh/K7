using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas.Medias;
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

    public async Task<MovieMetadata?> FetchMovieMetadata(int movieId, string metadataProviderExternalId, string language, CancellationToken cancellationToken)
    {
        try
        {
            var tmdbMovie = await _tdmbClient.GetMovieAsync(metadataProviderExternalId, language, extraMethods: MovieMethods.ExternalIds | MovieMethods.Credits, cancellationToken: cancellationToken);

            var movieMetadata = new MovieMetadata
            {
                MediaId = movieId,
                Title = tmdbMovie.Title,
                OriginalTitle = tmdbMovie.OriginalTitle,
                ReleaseDate = tmdbMovie.ReleaseDate.HasValue ? DateOnly.FromDateTime(tmdbMovie.ReleaseDate.Value) : null,
                Genres = tmdbMovie.Genres.Select(g => g.Name).ToList(),
                OriginalLanguage = tmdbMovie.OriginalLanguage,
                Overview = tmdbMovie.Overview,
                TagLine = tmdbMovie.Tagline,
                
                //ExternalIds = ConvertToExternalIds(tmdbMovie.ExternalIds)
            };

            return movieMetadata;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching movie metadata for movieId {movieId} (TMDbId {metadataProviderExternalId}): {ex.Message}");
            return null;
        }
    }

    public async Task<ICollection<MetadataPicture>?> FetchMetadataPictures(int metadataId, string metadataProviderExternalId, string language, CancellationToken cancellationToken, string? fallbackLanguage = "en")
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
            return await TryDownloadPictures(images, metadataId, language);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching media pictures for metadataId {metadataId} (TMDbId {metadataProviderExternalId}): {ex.Message}");
            return null;
        }
    }

    private async Task<ICollection<MetadataPicture>> TryDownloadPictures(Images images, int metadataId, string preferredLanguage)
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
                if (await TryDownloadPictureAsync(bestBackdrop, filePath))
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
                if (await TryDownloadPictureAsync(bestLogo, filePath))
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
                if (await TryDownloadPictureAsync(bestPoster, filePath))
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

    private async Task<bool> TryDownloadPictureAsync(ImageData imageData, string path)
    {
        try
        {
            var bytes = await _tdmbClient.GetImageBytesAsync("original", imageData.FilePath);
            FileInfo file = new(path);
            file.Directory!.Create();
            File.WriteAllBytes(path, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private ICollection<ExternalId> ConvertToExternalIds(ExternalIdsMovie externalIdsMovie)
    {
        var externalIds = new List<ExternalId>
        {
            new()
            {
                MetadataId = -1,
                Platform = "tmdb",
                Value = externalIdsMovie.Id.ToString()
            }
        };

        if (!string.IsNullOrEmpty(externalIdsMovie.ImdbId))
        {
            externalIds.Add(new ExternalId()
            {
                MetadataId = -1,
                Platform = "imdb",
                Value = externalIdsMovie.ImdbId
            });
        }

        return externalIds;
    }
}
