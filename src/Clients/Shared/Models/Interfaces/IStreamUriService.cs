using K7.Shared.Dtos;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface IStreamUriService
{
    /// <summary>
    /// Creates or retrieves a streaming session for the given indexed file and
    /// returns the associated session information, including the initial source
    /// URL (direct play or HLS session manifest).
    /// </summary>
    Task<StreamingSessionDto> GetOrCreateSessionAsync(Guid indexedFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default);
}
