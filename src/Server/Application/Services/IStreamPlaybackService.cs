using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Shared.Dtos;

namespace K7.Server.Application.Services;

public interface IStreamPlaybackService
{
    Task<IndexedFileStreamUri> GetStreamUriAsync(
        GetStreamUriQuery query,
        CancellationToken cancellationToken = default);

    Task<HttpContentResult> GetHlsVideoSegmentAsync(
        GetHlsVideoStreamSegmentQuery query,
        CancellationToken cancellationToken = default);

    Task<HttpContentResult> GetHlsAudioSegmentAsync(
        GetHlsAudioStreamSegmentQuery query,
        CancellationToken cancellationToken = default);
}
