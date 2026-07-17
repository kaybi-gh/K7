using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;

public static class GetHlsVideoStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/segments/{segmentNumber}.m4s";

    public static string Build(GetHlsVideoStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.Quality)
        .Replace("{segmentNumber}", query.SegmentNumber.ToString());

    public static string Build(Guid id, string quality, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", quality)
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.m4s";

    public static string BuildInitSegmentPath() => "segments/init.m4s";
}

public record GetHlsVideoStreamSegmentQuery(
    Guid Id,
    string Quality,
    int SegmentNumber,
    Guid StreamSessionId,
    string? TranscodingVideoCodec = null,
    int? SubtitleBurnInStreamIndex = null) : IRequest<HttpContentResult>;

public class GetHlsVideoStreamSegmentQueryHandler(
    IStreamPlaybackService streamPlaybackService)
    : IRequestHandler<GetHlsVideoStreamSegmentQuery, HttpContentResult>
{
    public Task<HttpContentResult> Handle(GetHlsVideoStreamSegmentQuery query, CancellationToken cancellationToken)
        => streamPlaybackService.GetHlsVideoSegmentAsync(query, cancellationToken);
}
