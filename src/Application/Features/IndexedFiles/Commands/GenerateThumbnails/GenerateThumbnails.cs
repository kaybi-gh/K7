using System.Drawing;
using FFMpegCore;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.IndexedFiles.Commands.GenerateThumbnails;
public record GenerateThumbnailsCommand : IRequest
{
    public required Guid Id { get; set; }
}

public class GenerateThumbnailsCommandHandler : IRequestHandler<GenerateThumbnailsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;

    public GenerateThumbnailsCommandHandler(IApplicationDbContext context, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task Handle(GenerateThumbnailsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata == null || entity.FileMetadata.Type == FileType.Video)
        {
            throw new InvalidOperationException("Can't generate thumbnails on anything else than video files.");
        }

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            //return Results.NotFound();
        }

        var mediaInfo = await FFProbe.AnalyseAsync(entity.Path, cancellationToken: cancellationToken);
        var computedWidth = (double)144 * mediaInfo.PrimaryVideoStream!.Width / mediaInfo.PrimaryVideoStream!.Height;
        var thumbnails = new List<MetadataPicture>();
        for (int i = 1; i < mediaInfo.Duration.TotalSeconds / 10; i++)
        {
            var basePath = Path.Combine(_pathsConfiguration.Metadatas, "thumbnails", $"{entity.FileMetadata.Id}");
            var thumbnailPath = Path.Combine(basePath, $"{i}");
            Directory.CreateDirectory(basePath);

            if (FFMpeg.Snapshot(entity.Path, thumbnailPath, new Size((int)computedWidth, 144), TimeSpan.FromSeconds(i * 10)))
            {
                thumbnails.Add(new MetadataPicture()
                {
                    Type = MetadataPictureType.Thumbnail,
                    VideoFileMetadataId = entity.FileMetadata.Id,
                    LocalPath = thumbnailPath
                });
            }
        }

        _context.MetadataPictures.AddRange(thumbnails);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
