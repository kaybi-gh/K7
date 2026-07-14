namespace K7.Server.Application.Common.Interfaces;

public interface IMediaLibraryAvailabilityService
{
    Task RebuildForLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default);

    Task RebuildAllAsync(CancellationToken cancellationToken = default);

    Task EnsurePopulatedAsync(CancellationToken cancellationToken = default);
}
