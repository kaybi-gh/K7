using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Deletes movies that no longer have files and carry no user data.
/// </summary>
public static class MovieOrphanCleanupHelper
{
    /// <summary>
    /// Removes <paramref name="movieId"/> when it has no local/remote files and no watch/review/playlist state.
    /// </summary>
    public static async Task<bool> TryDeleteIfOrphanAsync(
        IApplicationDbContext context,
        Guid movieId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var movie = await context.Medias
            .OfType<Movie>()
            .Include(m => m.IndexedFiles)
            .Include(m => m.RemoteIndexedFiles)
            .FirstOrDefaultAsync(m => m.Id == movieId, cancellationToken);

        if (movie is null)
            return false;

        if (movie.IndexedFiles.Count > 0 || movie.RemoteIndexedFiles.Count > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, movieId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan movie {MovieId} ({Title}) because user data exists",
                movieId,
                movie.Title);
            return false;
        }

        context.Medias.Remove(movie);
        logger.LogInformation(
            "Deleted orphan movie {MovieId} with no files and no user data",
            movieId);

        return true;
    }
}
