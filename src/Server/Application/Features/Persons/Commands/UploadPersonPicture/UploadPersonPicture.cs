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

namespace K7.Server.Application.Features.Persons.Commands.UploadPersonPicture;

[Authorize(Roles = Roles.Administrator)]
public record UploadPersonPictureCommand : IRequest<Guid>
{
    public required Guid PersonId { get; init; }
    public required MetadataPictureType PictureType { get; init; }
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public class UploadPersonPictureCommandHandler : IRequestHandler<UploadPersonPictureCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly ILogger<UploadPersonPictureCommandHandler> _logger;

    public UploadPersonPictureCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<UploadPersonPictureCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
    }

    public async Task<Guid> Handle(UploadPersonPictureCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.Persons
            .Include(p => p.PortraitPicture)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        var ext = Path.GetExtension(request.FileName);
        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "persons", $"{request.PersonId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}{ext}");

        await using (var fs = File.Create(filePath))
        {
            await request.FileStream.CopyToAsync(fs, cancellationToken);
        }

        _logger.LogInformation("Saved uploaded picture for person {PersonId} to {Path}", request.PersonId, filePath);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = request.PictureType,
            PersonId = request.PersonId,
            LocalPath = filePath
        };

        person.PortraitPicture = picture;
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
