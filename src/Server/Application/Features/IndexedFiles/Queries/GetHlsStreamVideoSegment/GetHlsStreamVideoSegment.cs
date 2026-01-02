using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;

public static class GetHlsVideoStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/segments/{segmentId}.ts";

    public static string Build(GetHlsVideoStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.VideoResolutionIdentifier)
        .Replace("{segmentId}", $"{query.SegmentId}");

    public static string Build(Guid id, string videoResolutionIdentifier, int segmentId) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", videoResolutionIdentifier)
        .Replace("{segmentId}", $"{segmentId}");

    public static string BuildPlaylistRelativePath(int segmentId) => Route
        .Replace("{id}/hls-stream/video/{quality}/", "")
        .Replace("{segmentId}", $"{segmentId}");
}

public record GetHlsVideoStreamSegmentQuery(Guid Id, string VideoResolutionIdentifier, int SegmentId) : IRequest<IResult>;

public class GetHlsVideoStreamSegmentQueryHandler : IRequestHandler<GetHlsVideoStreamSegmentQuery, IResult>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _segmentLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly IMediaTranscoder _mediaTranscodingService;

    public GetHlsVideoStreamSegmentQueryHandler(
        IApplicationDbContext context,
        IOptions<PathsConfiguration> pathsConfiguration,
        IMediaTranscoder mediaTranscodingService)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
        _mediaTranscodingService = mediaTranscodingService;
    }

    public async Task<IResult> Handle(GetHlsVideoStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        // TODO - Create specific enum
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Constants.VideoQualities.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.VideoResolutionIdentifier), $"Provided quality '{query.VideoResolutionIdentifier}' is not valid.");
        }

        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "video", query.VideoResolutionIdentifier, $"{query.SegmentId}.ts");

        var file = new FileInfo(segmentPath);
        if (file.Exists)
        {
            return Results.File(file.OpenRead(), contentType: "video/mp2t");
        }

        var semaphore = _segmentLocks.GetOrAdd(segmentPath, new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return Results.Conflict("Segment generation already in progress.");
        }

        try
        {
            var segments = await _context.HlsSegments
                .Where(x => x.IndexedFileId == query.Id)
                .Where(x => x.Number >= query.SegmentId)
                .Take(2)
                .ToListAsync(cancellationToken: cancellationToken);

            var indexedFile = await _context.IndexedFiles
                .Where(x => x.Id == segments.First().IndexedFileId)
                .SingleOrDefaultAsync(cancellationToken: cancellationToken);

            // Check if file is video?
            Guard.Against.Null(indexedFile);

            var tempDirectory = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "video", query.VideoResolutionIdentifier, $"{query.SegmentId}");
            Directory.CreateDirectory(tempDirectory);

            if (query.VideoResolutionIdentifier == "original")
            {
                await _mediaTranscodingService.GenerateRemuxedVideoHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments);
            }
            else
            {
                var quality = Constants.VideoQualities.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
                await _mediaTranscodingService.GenerateTranscodedVideoHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments, query.VideoResolutionIdentifier);
            }
        }
        finally
        {
            semaphore.Release();
            _segmentLocks.TryRemove(segmentPath, out _);
        }

        return Results.File(file.OpenRead(), contentType: "video/mp2t");
    }
}
