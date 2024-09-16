using FFMpegCore;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities.Metadatas.Files;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
public record CreateFileMetadatasCommand : IRequest
{
    public required Guid Id { get; set; }
    public required FileType FileType { get; set; }
}

public class CreateFileMetadatasCommandHandler : IRequestHandler<CreateFileMetadatasCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateFileMetadatasCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(CreateFileMetadatasCommand request, CancellationToken cancellationToken)
    {        
        var indexedFile = await _context.IndexedFiles
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var file = new FileInfo(indexedFile.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", indexedFile.Path);
        }

        var mediaAnalysis = await FFProbe.AnalyseAsync(indexedFile.Path, cancellationToken: cancellationToken);

        if (mediaAnalysis.PrimaryVideoStream == null)
        {
            throw new InvalidOperationException();
        }

        BaseFileMetadata fileMetadata = request.FileType switch
        {
            FileType.Audio => new AudioFileMetadata(),
            FileType.Video => new VideoFileMetadata()
            {
                VideoBitrate = mediaAnalysis.PrimaryVideoStream.BitRate,
                VideoFramerate = mediaAnalysis.PrimaryVideoStream.FrameRate,
                VideoResolution = Qualities.Video
                    .Any(x => x.Value.Height == mediaAnalysis.PrimaryVideoStream.Height) ?
                    Qualities.Video.First(x => x.Value.Height == mediaAnalysis.PrimaryVideoStream.Height).Key
                    : throw new InvalidOperationException()

            },
            _ => throw new NotImplementedException(),
        };

        indexedFile.FileMetadata = fileMetadata;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
