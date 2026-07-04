using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;

public record GenerateThumbnailsCommand : IRequest
{
    public required Guid Id { get; set; }
}

public class GenerateThumbnailsCommandHandler : IRequestHandler<GenerateThumbnailsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAnalysisService _mediaAnalysisService;

    public GenerateThumbnailsCommandHandler(
        IApplicationDbContext context,
        IMediaAnalysisService mediaAnalysisService)
    {
        _context = context;
        _mediaAnalysisService = mediaAnalysisService;
    }

    public async Task Handle(GenerateThumbnailsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => (x as VideoFileMetadata)!.Thumbnails)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is not VideoFileMetadata videoFileMetadata)
        {
            throw new InvalidOperationException("Can't generate thumbnails on non-video files.");
        }

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", entity.Path);
        }

        var thumbnails = await _mediaAnalysisService.GenerateThumbnailsAsync(entity, cancellationToken: cancellationToken);

        if (videoFileMetadata.Thumbnails is not null)
        {
            _context.MetadataPictures.Remove(videoFileMetadata.Thumbnails);
        }
        videoFileMetadata.Thumbnails = thumbnails;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
