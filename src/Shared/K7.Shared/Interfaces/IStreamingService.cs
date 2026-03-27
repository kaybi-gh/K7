using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IStreamingService
{
    Task<IndexedFileStreamUri?> GetIndexedFileStreamUriAsync(GetIndexedFileStreamsUriQuery query, CancellationToken cancellationToken = default);
    Task<StreamingSessionDto?> CreateStreamSessionAsync(CreateStreamSessionRequest request, CancellationToken cancellationToken = default);
    Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, double position, double duration, CancellationToken cancellationToken = default);
}
