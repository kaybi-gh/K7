using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Medias.Commands.UploadMediaPicture;

[Authorize(Roles = Roles.Administrator)]
public record UploadMediaPictureCommand : IRequest<Guid>
{
    public required Guid MediaId { get; init; }
    public required MetadataPictureType PictureType { get; init; }
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public class UploadMediaPictureCommandHandler : IRequestHandler<UploadMediaPictureCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly ILogger<UploadMediaPictureCommandHandler> _logger;

    public UploadMediaPictureCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<UploadMediaPictureCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
    }

    public async Task<Guid> Handle(UploadMediaPictureCommand request, CancellationToken cancellationToken)
    {
        var media = await _context.Medias
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var ext = Path.GetExtension(request.FileName);
        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "medias", $"{request.MediaId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}{ext}");

        await using (var fs = File.Create(filePath))
        {
            await request.FileStream.CopyToAsync(fs, cancellationToken);
        }

        _logger.LogInformation("Saved uploaded picture for media {MediaId} to {Path}", request.MediaId, filePath);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = request.PictureType,
            MediaId = request.MediaId,
            LocalPath = filePath
        };

        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture)
        }, cancellationToken);

        return picture.Id;
    }
}
