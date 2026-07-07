using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Persons.Commands.ImportPersonPictureFromUrl;

[Authorize(Roles = Roles.Administrator)]
public record ImportPersonPictureFromUrlCommand : IRequest<Guid>
{
    public required Guid PersonId { get; init; }
    public required string Url { get; init; }
    public required MetadataPictureType PictureType { get; init; }
}

public class ImportPersonPictureFromUrlCommandHandler : IRequestHandler<ImportPersonPictureFromUrlCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetadataPictureDeletionService _pictureDeletionService;
    private readonly ILogger<ImportPersonPictureFromUrlCommandHandler> _logger;

    public ImportPersonPictureFromUrlCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        IHttpClientFactory httpClientFactory,
        MetadataPictureDeletionService pictureDeletionService,
        ILogger<ImportPersonPictureFromUrlCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _httpClientFactory = httpClientFactory;
        _pictureDeletionService = pictureDeletionService;
        _logger = logger;
    }

    public async Task<Guid> Handle(ImportPersonPictureFromUrlCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.Persons
            .Include(p => p.PortraitPicture)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        await _pictureDeletionService.RemovePersonPortraitAsync(request.PersonId, cancellationToken);
        person.PortraitPicture = null;

        using var httpClient = _httpClientFactory.CreateClient();
        await using var responseStream = await httpClient.GetStreamAsync(request.Url, cancellationToken);

        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "persons", $"{request.PersonId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}.jpg");

        await using (var fs = File.Create(filePath))
        {
            await responseStream.CopyToAsync(fs, cancellationToken);
        }

        _logger.LogInformation("Imported picture from URL for person {PersonId} to {Path}", request.PersonId, filePath);

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
