using FFMpegCore;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas.Files;

namespace MediaServer.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
public record ComputeHlsSegmentsCommand : IRequest
{
    public required Guid Id { get; set; }
    public required TimeSpan SegmentsDuration { get; init; } = TimeSpan.FromSeconds(2);
}

public class ComputeHlsSegmentsCommandHandler : IRequestHandler<ComputeHlsSegmentsCommand>
{
    private readonly IApplicationDbContext _context;

    public ComputeHlsSegmentsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ComputeHlsSegmentsCommand request, CancellationToken cancellationToken)
    {        
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => x!.HlsSegments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", entity.Path);
        }

        if (entity.FileMetadata is VideoFileMetadata videoFileMetadata)
        {
            var keyframeTimestamps = await ExtractKeyframeTimestampsAsync(entity.Path, cancellationToken);
            var segments = ComputeKeyframeBasedHlsSegments(entity, request.SegmentsDuration, keyframeTimestamps, (long)videoFileMetadata.Duration.TotalMilliseconds);
            // TODO - Does it clear current segments?
            videoFileMetadata.HlsSegments = segments;
        }

        if (entity.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            throw new NotImplementedException();
            // TODO - Compute audio segments
            //var segments = ComputeHlsSegments(request, (long)audioFileMetadata.Duration.TotalMilliseconds);
            // TODO - Does it clear current segments?
            //audioFileMetadata.HlsSegments = segments;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async static Task<List<long>> ExtractKeyframeTimestampsAsync(string path, CancellationToken cancellationToken = default)
    {
        var packetsAnalysisResult = await FFProbe.GetPacketsAsync(path, cancellationToken: cancellationToken);
        return packetsAnalysisResult.Packets
            .Where(x => x.StreamIndex == 0
                && x.CodecType == "video"
                && x.Flags.Contains('K')) // 'K' for keyframe
            .Select(x => x.Pts) // Extract the presentation timestamp in milliseconds
            .ToList();
    }

    private static List<HlsSegment> ComputeKeyframeBasedHlsSegments(
        IndexedFile indexedFile,
        TimeSpan segmentsDuration,
        List<long> keyframeTimestamps,
        long totalVideoDuration
    )
    {
        if (keyframeTimestamps == null
            || keyframeTimestamps.Count == 0
            || indexedFile.FileMetadata == null)
        {
            return [];
        }

        var segments = new List<HlsSegment>();
        long segmentStart = 0;
        var nextSegmentBoundary = segmentStart + (long)segmentsDuration.TotalMilliseconds;

        for (int i = 1; i < keyframeTimestamps.Count; i++)
        {
            if (keyframeTimestamps[i] >= nextSegmentBoundary)
            {
                // Compare the keyframe timestamps to find the closest one to the boundary
                var segmentEnd = GetClosestTimestamp(keyframeTimestamps[i - 1], keyframeTimestamps[i], nextSegmentBoundary);

                // Ensure that the segment has a non-zero duration
                if (segmentEnd > segmentStart)
                {
                    segments.Add(new HlsSegment
                    {
                        FileMetadataId = indexedFile.FileMetadata.Id,
                        IndexedFileId = indexedFile.Id,
                        Number = segments.Count,
                        StartTimestamp = segmentStart,
                        Duration = segmentEnd - segmentStart
                    });

                    segmentStart = segmentEnd;
                    nextSegmentBoundary = segmentStart + (long)segmentsDuration.TotalMilliseconds;
                }
            }
        }

        // Handle the last segment and extend it to the totalVideoDuration
        long lastSegmentDuration = totalVideoDuration - segmentStart;

        if (lastSegmentDuration < segmentsDuration.TotalMilliseconds / 2 && segments.Count > 0)
        {
            // Merge the last short segment with the previous one
            var previousSegment = segments[^1];
            previousSegment.Duration = totalVideoDuration - previousSegment.StartTimestamp;
        }
        else
        {
            // Add the last segment
            segments.Add(new HlsSegment
            {
                FileMetadataId = indexedFile.FileMetadata.Id,
                IndexedFileId = indexedFile.Id,
                Number = segments.Count,
                StartTimestamp = segmentStart,
                Duration = lastSegmentDuration
            });
        }

        return segments;
    }

    private static long GetClosestTimestamp(long previousTimestamp, long nextTimestamp, long targetTimestamp)
    {
        long distanceToPreviousTimestamp = Math.Abs(previousTimestamp - targetTimestamp);
        long distanceToNextTimestamp = Math.Abs(nextTimestamp - targetTimestamp);
        return distanceToPreviousTimestamp <= distanceToNextTimestamp
            ? previousTimestamp
            : nextTimestamp;
    }
}
