using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Metadatas.Files;
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
                .ThenInclude(x => x!.HlsSegments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

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
            case AudioFileMetadata audioFileMetadata:
                // TODO - Compute audio segments
                //var segments = ComputeHlsSegments(request, (long)audioFileMetadata.Duration.TotalMilliseconds);
                // TODO - Does it clear current segments?
                //audioFileMetadata.HlsSegments = segments;
                throw new InvalidOperationException("File metadata must be computed first.");

            case VideoFileMetadata videoFileMetadata:
                var segments = await _mediaAnalysisService.ComputeKeyframeBasedHlsSegmentsAsync(
                    entity,
                    request.SegmentsDuration,
                    (long)videoFileMetadata.Duration.TotalMilliseconds,
                    cancellationToken);
                // TODO - Does it clear current segments?
                videoFileMetadata.HlsSegments = segments;
                break;

            default:
                throw new InvalidOperationException();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
