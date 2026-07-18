using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;

public record ExtractChaptersCommand : IRequest
{
    public required Guid Id { get; init; }
}

public class ExtractChaptersCommandHandler(
    IApplicationDbContext context,
    IMediaAnalysisService mediaAnalysisService) : IRequestHandler<ExtractChaptersCommand>
{
    public async Task Handle(ExtractChaptersCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is not VideoFileMetadata videoMetadata)
            throw new InvalidOperationException("Chapters can only be extracted for video files with metadata.");

        if (!File.Exists(entity.Path))
            throw new FileNotFoundException("File not found.", entity.Path);

        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == entity.LibraryId, cancellationToken);

        if (library is null || !library.ChapterExtractionEnabled)
        {
            videoMetadata.Chapters = null;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        videoMetadata.Chapters = await mediaAnalysisService.GetChaptersAsync(entity.Path, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
