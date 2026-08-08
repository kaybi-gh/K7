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

        var hasUserData = await context.UserMediaStates
                .AnyAsync(s => s.MediaId == movieId, cancellationToken)
            || await context.MediaReviews
                .AnyAsync(r => r.MediaId == movieId, cancellationToken)
            || await context.PlaylistItems
                .AnyAsync(p => p.MediaId == movieId, cancellationToken)
            || await context.SharedProfileMediaStates
                .AnyAsync(s => s.MediaId == movieId, cancellationToken);

        if (hasUserData)
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
