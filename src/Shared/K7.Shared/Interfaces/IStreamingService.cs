using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IStreamingService
{
    Task<IndexedFileStreamUri?> GetIndexedFileStreamUriAsync(GetIndexedFileStreamsUriQuery query, CancellationToken cancellationToken = default);
    Task<StreamingSessionDto?> CreateStreamSessionAsync(CreateStreamSessionRequest request, CancellationToken cancellationToken = default);
    Task<StreamingSessionDto?> CreateRemoteStreamSessionAsync(CreateRemoteStreamSessionRequest request, CancellationToken cancellationToken = default);
    Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, Guid referenceId, double position, double duration, int state, Guid? deviceId = null, Guid? playlistId = null, CancellationToken cancellationToken = default);
    Task<string?> GenerateEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
    Task RevokeEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
}
