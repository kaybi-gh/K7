using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
public record CreateFileMetadatasCommand : IRequest
{
    public required Guid Id { get; set; }
    public required FileType FileType { get; set; }
}

public class CreateFileMetadatasCommandHandler : IRequestHandler<CreateFileMetadatasCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAnalysisService _mediaAnalysisService;

    public CreateFileMetadatasCommandHandler(IApplicationDbContext context, IMediaAnalysisService mediaAnalysisService)
    {
        _context = context;
        _mediaAnalysisService = mediaAnalysisService;
    }

    public async Task Handle(CreateFileMetadatasCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            
        Guard.Against.NotFound(request.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var file = new FileInfo(indexedFile.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", indexedFile.Path);
        }

        var fileMetadata = request.FileType switch
        {
            FileType.Audio => throw new NotImplementedException(),
            FileType.Video => await _mediaAnalysisService.GetVideoFileMetadataAsync(indexedFile.Path, cancellationToken),
            _ => throw new NotImplementedException(),
        };

        // Clear existing file metadatas
        if (indexedFile.FileMetadata is VideoFileMetadata vfm)
        {
            await _context.FileMetadatas.Entry(vfm).Collection(x => ((VideoFileMetadata)x).AudioTracks).LoadAsync(cancellationToken);
            await _context.FileMetadatas.Entry(vfm).Collection(x => ((VideoFileMetadata)x).SubtitleTracks).LoadAsync(cancellationToken);
            await _context.FileMetadatas.Entry(vfm).Collection(x => ((VideoFileMetadata)x).VideoTracks).LoadAsync(cancellationToken);
            await _context.FileMetadatas.Entry(vfm).Collection(x => ((VideoFileMetadata)x).HlsSegments).LoadAsync(cancellationToken);
            await _context.FileMetadatas.Entry(vfm).Reference(x => ((VideoFileMetadata)x).Thumbnails).LoadAsync(cancellationToken);
            _context.FileMetadatas.Remove(indexedFile.FileMetadata);
        }

        await _context.FileMetadatas.AddAsync(fileMetadata, cancellationToken);
        indexedFile.FileMetadata = fileMetadata;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
