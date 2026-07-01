using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Extensions;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
public record ComputeHlsSegmentsCommand : IRequest
{
    public required Guid Id { get; set; }
    public required TimeSpan SegmentsDuration { get; init; } = TimeSpan.FromSeconds(2);
}

public class ComputeHlsSegmentsCommandHandler : IRequestHandler<ComputeHlsSegmentsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAnalysisService _mediaAnalysisService;

    public ComputeHlsSegmentsCommandHandler(IApplicationDbContext context, IMediaAnalysisService mediaAnalysisService)
    {
        _context = context;
        _mediaAnalysisService = mediaAnalysisService;
    }

    public async Task Handle(ComputeHlsSegmentsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            await _context.Entry(audioFileMetadata).Collection(x => x.HlsSegments).LoadAsync(cancellationToken);
        }
        else if (entity.FileMetadata is VideoFileMetadata videoFileMetadataForLoad)
        {
            await _context.Entry(videoFileMetadataForLoad).Collection(x => x.HlsSegments).LoadAsync(cancellationToken);
        }

        if (entity.FileMetadata == null)
        {
            throw new InvalidOperationException("File metadata must be computed first.");
        }

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", entity.Path);
        }

        switch (entity.FileMetadata)
        {
            case AudioFileMetadata audioMetadata:
                var audioSegments = ComputeTimeBasedHlsSegments(
                    (long)audioMetadata.Duration.TotalMilliseconds,
                    entity.FileMetadata.Id,
                    entity.Id);
                _context.HlsSegments.RemoveRange(audioMetadata.HlsSegments);
                audioMetadata.HlsSegments = audioSegments;
                break;

            case VideoFileMetadata videoFileMetadata:
                var segments = await _mediaAnalysisService.ComputeKeyframeBasedHlsSegmentsAsync(
                    entity,
                    request.SegmentsDuration,
                    (long)videoFileMetadata.Duration.TotalMilliseconds,
                    cancellationToken);
                _context.HlsSegments.RemoveRange(videoFileMetadata.HlsSegments);
                videoFileMetadata.HlsSegments = segments;
                break;

            default:
                throw new InvalidOperationException();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<HlsSegment> ComputeTimeBasedHlsSegments(long totalDurationMs, Guid fileMetadataId, Guid indexedFileId, int segmentLengthMs = 6000)
    {
        var segments = new List<HlsSegment>();
        long offset = 0;
        var index = 0;

        while (offset < totalDurationMs)
        {
            var duration = Math.Min(segmentLengthMs, totalDurationMs - offset);
            segments.Add(new HlsSegment
            {
                FileMetadataId = fileMetadataId,
                IndexedFileId = indexedFileId,
                Number = index,
                StartTimestamp = offset,
                Duration = duration
            });
            offset += segmentLengthMs;
            index++;
        }

        return segments;
    }
}
