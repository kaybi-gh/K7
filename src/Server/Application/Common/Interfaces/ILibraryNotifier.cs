namespace K7.Server.Application.Common.Interfaces;

public interface ILibraryNotifier
{
    Task NotifyMediaAddedAsync(Guid mediaId, string? title, string mediaType, CancellationToken cancellationToken = default);
    Task NotifyLibraryScanCompletedAsync(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount, CancellationToken cancellationToken = default);
}
