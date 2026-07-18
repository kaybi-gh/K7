using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;

public record CreateFileMetadatasCommand : IRequest
{
    public required Guid Id { get; set; }
    public required FileType FileType { get; set; }
}

public class CreateFileMetadatasCommandHandler(
    IApplicationDbContext context,
    IMediaAnalysisService mediaAnalysisService,
    ISender sender,
    ILogger<CreateFileMetadatasCommandHandler> logger) : IRequestHandler<CreateFileMetadatasCommand>
{
    public async Task Handle(CreateFileMetadatasCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        if (!File.Exists(indexedFile.Path))
        {
            logger.LogInformation(
                "Indexed file {IndexedFileId} no longer exists on disk at {Path}; removing entry",
                indexedFile.Id,
                indexedFile.Path);
            await sender.Send(new DeleteIndexedFileCommand(indexedFile.Id), cancellationToken);
            return;
        }

        BaseFileMetadata fileMetadata = request.FileType switch
        {
            FileType.Audio => await mediaAnalysisService.GetAudioFileMetadataAsync(indexedFile.Path, cancellationToken),
            FileType.Video => await mediaAnalysisService.GetVideoFileMetadataAsync(indexedFile.Path, cancellationToken),
            _ => throw new NotImplementedException(),
        };

        if (fileMetadata is VideoFileMetadata videoMetadata)
        {
            var library = await context.Libraries
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == indexedFile.LibraryId, cancellationToken);

            if (library?.ChapterExtractionEnabled == true)
            {
                try
                {
                    videoMetadata.Chapters = await mediaAnalysisService.GetChaptersAsync(indexedFile.Path, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Chapter extraction failed for IndexedFile {IndexedFileId}", indexedFile.Id);
                    videoMetadata.Chapters = [];
                }
            }
        }

        // Clear existing file metadatas
        if (indexedFile.FileMetadata is AudioFileMetadata afm)
        {
            await context.Entry(afm).Reference(x => x.AudioTrack).LoadAsync(cancellationToken);
            await context.Entry(afm).Collection(x => x.HlsSegments).LoadAsync(cancellationToken);
            context.FileMetadatas.Remove(indexedFile.FileMetadata);
        }
        else if (indexedFile.FileMetadata is VideoFileMetadata vfm)
        {
            await context.Entry(vfm).Collection(x => x.AudioTracks).LoadAsync(cancellationToken);
            await context.Entry(vfm).Collection(x => x.SubtitleTracks).LoadAsync(cancellationToken);
            await context.Entry(vfm).Collection(x => x.VideoTracks).LoadAsync(cancellationToken);
            await context.Entry(vfm).Collection(x => x.HlsSegments).LoadAsync(cancellationToken);
            await context.Entry(vfm).Reference(x => x.Thumbnails).LoadAsync(cancellationToken);
            context.FileMetadatas.Remove(indexedFile.FileMetadata);
        }

        await context.FileMetadatas.AddAsync(fileMetadata, cancellationToken);
        indexedFile.FileMetadata = fileMetadata;
        indexedFile.AddDomainEvent(new FileMetadataCreatedEvent(indexedFile, request.FileType));
        await context.SaveChangesAsync(cancellationToken);
    }
}
