using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface IStreamUriService
{
    Task<StreamingSessionDto> GetOrCreateSessionAsync(Guid indexedFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default);

    Task<StreamingSessionDto?> GetOrCreateRemoteSessionAsync(Guid remoteFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default);
}
