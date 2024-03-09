using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Interfaces;
using MediaServer.Domain.ValueObjects;
using MediaServer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;

namespace MediaServer.Application.Services;
public class TMDbMetadataProvider : IMetadataProviderService
{
    private const string Token = "8e7586ad850237f5d506d8789f4c3936";
    private readonly TMDbClient _tdmbClient;
    private readonly PathsConfiguration _pathsConfiguration;

    public TMDbMetadataProvider(IOptions<PathsConfiguration> pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration.Value;
        _tdmbClient = new(Token);
        _tdmbClient.SetConfig((_tdmbClient.GetConfigAsync()).Result);
    }

    public async Task<MovieMetadata> FetchMovieMetadata(MovieIdentification movieIdentification, Domain.Entities.Medias.Movie movie, CancellationToken cancellationToken)
    {
        var movieMetadata = new MovieMetadata()
        {
            Media = movie,
            MediaId = movie.Id,
            Title = movieIdentification.Title,
            ReleaseDate = movieIdentification.ReleaseYear
        };

        try
        {
            var searchResult = await _tdmbClient.SearchMovieAsync(movieIdentification.Title,
                year: movieIdentification.ReleaseYear.HasValue ? movieIdentification.ReleaseYear.Value.Year : 0,
                cancellationToken: cancellationToken);
            var tmdbId = searchResult.Results.FirstOrDefault()?.Id;

            if (tmdbId == null)
            {
                return movieMetadata;
            }

            var tmdbMovie = await _tdmbClient.GetMovieAsync(tmdbId.Value, "fr", extraMethods: MovieMethods.Images | MovieMethods.ExternalIds | MovieMethods.Credits, cancellationToken: cancellationToken);
            if (tmdbMovie.Images.Backdrops.Count == 0 || tmdbMovie.Images.Logos.Count == 0 || tmdbMovie.Images.Posters.Count == 0)
            {
                var fallbackLangageImages = await _tdmbClient.GetMovieImagesAsync(tmdbId.Value, "en", cancellationToken: cancellationToken);
                if (tmdbMovie.Images.Backdrops.Count == 0)
                    tmdbMovie.Images.Backdrops = fallbackLangageImages.Backdrops;
                if (tmdbMovie.Images.Logos.Count == 0)
                    tmdbMovie.Images.Logos = fallbackLangageImages.Logos;
                if (tmdbMovie.Images.Posters.Count == 0)
                    tmdbMovie.Images.Posters = fallbackLangageImages.Posters;
            }

            movieMetadata.Genres = tmdbMovie.Genres.Select(g => g.Name).ToList();
            movieMetadata.OriginalLanguage = tmdbMovie.OriginalLanguage;
            movieMetadata.Overview = tmdbMovie.Overview;
            movieMetadata.ReleaseDate = tmdbMovie.ReleaseDate.HasValue ? DateOnly.FromDateTime(tmdbMovie.ReleaseDate.Value) : movieMetadata.ReleaseDate;
            movieMetadata.TagLine = tmdbMovie.Tagline;
            movieMetadata.Title = tmdbMovie.Title;
            movieMetadata.Pictures = await TryDownloadPictures(tmdbMovie.Images, movie);

            return movieMetadata;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching movie metadata {movie.Id}: {ex.Message}");
            return movieMetadata;
        }
    }

    private async Task<IEnumerable<MediaPicture>> TryDownloadPictures(Images images, BaseMedia media)
    {
        List<MediaPicture> mediaPictures = [];
        if (images != null)
        {
            var bestBackdrop = images.Backdrops.OrderBy(b => b.Iso_639_1 == "fr").ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestLogo = images.Logos.OrderBy(b => b.Iso_639_1 == "fr").ThenByDescending(b => b.VoteAverage).FirstOrDefault();
            var bestPoster = images.Posters.OrderBy(b => b.Iso_639_1 == "fr").ThenByDescending(b => b.VoteAverage).FirstOrDefault();

            if (bestBackdrop != null)
            {
                var test = new FileInfo(bestBackdrop.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, media.Id.ToString(), $"{Guid.NewGuid()}{test.Extension}");
                if (await TryDownloadPictureAsync(bestBackdrop, filePath))
                {
                    mediaPictures.Add(new MediaPicture()
                    {
                        MediaId = media.Id,
                        Path = filePath,
                        Type = MediaPictureType.Backdrop
                    });
                }
            }

            if (bestLogo != null)
            {
                var test = new FileInfo(bestLogo.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, media.Id.ToString(), $"{Guid.NewGuid()}{test.Extension}");
                if (await TryDownloadPictureAsync(bestLogo, filePath))
                {
                    mediaPictures.Add(new MediaPicture()
                    {
                        MediaId = media.Id,
                        Path = filePath,
                        Type = MediaPictureType.Backdrop
                    });
                }
            }

            if (bestPoster != null)
            {
                var test = new FileInfo(bestPoster.FilePath);
                var filePath = Path.Combine(_pathsConfiguration.Metadatas, media.Id.ToString(), $"{Guid.NewGuid()}{test.Extension}");
                if (await TryDownloadPictureAsync(bestPoster, filePath))
                {
                    mediaPictures.Add(new MediaPicture()
                    {
                        MediaId = media.Id,
                        Path = filePath,
                        Type = MediaPictureType.Backdrop
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

    public record MovieSearch()
    {
        public required string Title { get; init; }
        public DateOnly? ReleaseYear { get; init; }
    }
}
