using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Interfaces;
using TMDbLib.Client;

namespace MediaServer.Application.Services;
public class TMDbMetadataProvider : IMetadataFetcherService
{
    private const string Token = "8e7586ad850237f5d506d8789f4c3936";
    private readonly TMDbClient _tdmbClient;

    public TMDbMetadataProvider()
    {
        _tdmbClient = new(Token);
    }

    public async Task FetchMovie(Movie movie, CancellationToken cancellationToken)
    {
        try
        {
            if (movie.ReleaseYear.HasValue)
            {
                var test = await _tdmbClient.SearchMovieAsync(movie.Title, year: movie.ReleaseYear.Value.Year, cancellationToken: cancellationToken);
            }
            else
            {
                var test2 = await _tdmbClient.SearchMovieAsync(movie.Title, cancellationToken: cancellationToken);
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching movie metadata {movie.Id}: {ex.Message}");
        }
    }
}
