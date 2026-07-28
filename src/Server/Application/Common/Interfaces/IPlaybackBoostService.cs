namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Raises the scheduling priority of the pending work a user is actually waiting on.
/// </summary>
public interface IPlaybackBoostService
{
    /// <summary>
    /// Boosts the pending tasks targeting a file and, when known, its media, then wakes the workers.
    /// </summary>
    /// <param name="indexedFileId">File the user asked to play.</param>
    /// <param name="mediaId">Media the file belongs to, when it is already linked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BoostPendingWorkAsync(Guid indexedFileId, Guid? mediaId, CancellationToken cancellationToken = default);
}
