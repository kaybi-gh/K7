using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Extensions;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;

public static class GetHlsAudioStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audio/{audioTrackIndex}/segments/{segmentNumber}.m4s";

    public static string Build(Guid id, int audioTrackIndex, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}")
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.m4s";
}

public record GetHlsAudioStreamSegmentQuery(
    Guid Id,
    int AudioTrackIndex,
    int SegmentNumber,
    Guid StreamSessionId,
    string? TranscodingAudioCodec = null) : IRequest<HttpContentResult>;

public class GetHlsAudioStreamSegmentQueryHandler(
    IStreamPlaybackService streamPlaybackService)
    : IRequestHandler<GetHlsAudioStreamSegmentQuery, HttpContentResult>
{
    public Task<HttpContentResult> Handle(GetHlsAudioStreamSegmentQuery query, CancellationToken cancellationToken)
        => streamPlaybackService.GetHlsAudioSegmentAsync(query, cancellationToken);
}
