using System.Globalization;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas.Files;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;


public static class GetHlsVideoStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/index.m3u8";

    public static string Build(GetHlsVideoStreamIndexQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.VideoResolutionIdentifier);

    public static string Build(Guid id, string videoResolutionIdentifier) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", videoResolutionIdentifier);

    public static string BuildManifestRelativePath(string videoResolutionIdentifier) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{quality}", $"{videoResolutionIdentifier}");
}
public record GetHlsVideoStreamIndexQuery(
    Guid Id,
    string VideoResolutionIdentifier,
    Guid StreamSessionId,
    string? TranscodingVideoCodec = null,
    int? SubtitleBurnInStreamIndex = null,
    double? StartSeconds = null) : IRequest<HttpContentResult>;

public class GetHlsVideoStreamIndexQueryHandler : IRequestHandler<GetHlsVideoStreamIndexQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILogger<GetHlsVideoStreamIndexQueryHandler> _logger;

    public GetHlsVideoStreamIndexQueryHandler(
        IApplicationDbContext context,
        ISender sender,
        ILogger<GetHlsVideoStreamIndexQueryHandler> logger)
    {
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public async Task<HttpContentResult> Handle(GetHlsVideoStreamIndexQuery query, CancellationToken cancellationToken)
    {
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.VideoResolutionIdentifier);
            Guard.Against.Null(quality, nameof(query.VideoResolutionIdentifier), $"Provided quality '{query.VideoResolutionIdentifier}' is not valid.");
        }

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return new EmptyHttpContentResult(404);
        }

        var isTransmuxing = query.VideoResolutionIdentifier == "original"
            && string.IsNullOrEmpty(query.TranscodingVideoCodec)
            && !query.SubtitleBurnInStreamIndex.HasValue;

        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(_context, query.Id, cancellationToken);
        var effectiveTranscodingVideoCodec = query.TranscodingVideoCodec;

        if (isTransmuxing && hlsSegments.Count == 0)
        {
            await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(
                _sender,
                query.Id,
                _logger,
                cancellationToken);

            effectiveTranscodingVideoCodec ??= HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        // Shared keyframe timeline for original + transcoded variants.
        // Equal-length fallback only when keyframe rows are missing (forced transcode path).
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata is VideoFileMetadata v
                ? (long)v.Duration.TotalMilliseconds
                : throw new InvalidOperationException("Cannot determine duration for HLS video playlist");

        var streamingSegments = HlsSegmentHelper.ResolveVideoStreamingSegments(hlsSegments, totalDurationMs);
        var segmentDurations = HlsSegmentHelper.ToDurationSeconds(streamingSegments);

        var queryString = HlsMediaPlaylistBuilder.BuildQueryString(
            query.StreamSessionId,
            ("TranscodingVideoCodec", effectiveTranscodingVideoCodec),
            ("SubtitleBurnInStreamIndex", query.SubtitleBurnInStreamIndex?.ToString(CultureInfo.InvariantCulture)));

        var indexPlaylist = HlsMediaPlaylistBuilder.Build(
            segmentDurations,
            queryString,
            GetHlsVideoStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath,
            query.StartSeconds,
            independentSegments: !isTransmuxing);

        return new TextHttpContentResult(indexPlaylist, "application/vnd.apple.mpegurl");
    }
}
