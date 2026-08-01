namespace K7.Server.Application.Common.Interfaces;

public interface IMediaLibraryAvailabilityService
{
    Task RebuildForLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default);

    Task RebuildAllAsync(CancellationToken cancellationToken = default);

    Task EnsurePopulatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts missing library/media availability rows for media (and parents) linked
    /// through the given indexed files. Used after CreateMedia so browse queries that
    /// filter on MediaLibraryAvailabilities see media as soon as they are created,
    /// without waiting for a full library rebuild.
    /// </summary>
    Task EnsureFromIndexedFilesAsync(
        Guid libraryId,
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken = default);
}
