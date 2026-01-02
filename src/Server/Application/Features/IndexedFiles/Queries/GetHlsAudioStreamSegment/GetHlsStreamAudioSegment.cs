using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;

public static class GetHlsAudioStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audios/{index}/{quality}/segments/{segmentId}.ts";

    public static string Build(GetHlsAudioStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{index}", $"{query.Index}")
        .Replace("{quality}", query.AudioQualityIdentifier)
        .Replace("{segmentId}", $"{query.SegmentId}");
    
    // TODO - Use Build with original query params
    public static string Build(Guid id, int index, string audioQualityIdentifier, int segmentId) => Route
        .Replace("{id}", $"{id}")
        .Replace("{index}", $"{index}")
        .Replace("{quality}", audioQualityIdentifier)
        .Replace("{segmentId}", $"{segmentId}");

    public static string BuildPlaylistRelativePath(int segmentId) => Route
        .Replace("{id}/hls-stream/audios/{index}/{quality}/", "")
        .Replace("{segmentId}", $"{segmentId}");
}

public record GetHlsAudioStreamSegmentQuery(Guid Id, int Index, string AudioQualityIdentifier, int SegmentId) : IRequest<IResult>;

public class GetHlsAudioStreamSegmentQueryHandler : IRequestHandler<GetHlsAudioStreamSegmentQuery, IResult>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _segmentLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly IMediaTranscoder _mediaTranscodingService;

    public GetHlsAudioStreamSegmentQueryHandler(
        IApplicationDbContext context,
        IOptions<PathsConfiguration> pathsConfiguration,
        IMediaTranscoder mediaTranscodingService)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
        _mediaTranscodingService = mediaTranscodingService;
    }

    public async Task<IResult> Handle(GetHlsAudioStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        // TODO - Create specific enum
        if (query.AudioQualityIdentifier != "original")
        {
            var quality = Constants.VideoQualities.Where(kvp => kvp.Value.Name == query.AudioQualityIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.AudioQualityIdentifier), $"Provided quality '{query.AudioQualityIdentifier}' is not valid.");
        }

        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "audio", $"{query.Index}", query.AudioQualityIdentifier, $"{query.SegmentId}.ts");

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

            var tempDirectory = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "audio", $"{query.Index}", query.AudioQualityIdentifier, $"{query.SegmentId}");
            Directory.CreateDirectory(tempDirectory);

            if (query.AudioQualityIdentifier == "original")
            {
                await _mediaTranscodingService.GenerateRemuxedAudioHlsSegmentAsync(
                    indexedFile.Path,
                    tempDirectory,
                    segmentPath,
                    segments,
                    query.Index);
            }
            else
            {
                var quality = Constants.AudioQualities.Where(kvp => kvp.Value.Name == query.AudioQualityIdentifier).FirstOrDefault();
                await _mediaTranscodingService.GenerateTranscodedAudioHlsSegmentAsync(
                    indexedFile.Path,
                    tempDirectory,
                    segmentPath,
                    segments,
                    query.Index,
                    quality.Value);
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
