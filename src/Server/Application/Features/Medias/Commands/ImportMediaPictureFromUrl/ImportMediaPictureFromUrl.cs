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

namespace K7.Server.Application.Features.Medias.Commands.ImportMediaPictureFromUrl;

[Authorize(Roles = Roles.Administrator)]
public record ImportMediaPictureFromUrlCommand : IRequest<Guid>
{
    public required Guid MediaId { get; init; }
    public required string Url { get; init; }
    public required MetadataPictureType PictureType { get; init; }
}

public class ImportMediaPictureFromUrlCommandHandler : IRequestHandler<ImportMediaPictureFromUrlCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetadataPictureDeletionService _pictureDeletionService;
    private readonly ILibraryNotifier _libraryNotifier;
    private readonly ILogger<ImportMediaPictureFromUrlCommandHandler> _logger;

    public ImportMediaPictureFromUrlCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        IHttpClientFactory httpClientFactory,
        MetadataPictureDeletionService pictureDeletionService,
        ILibraryNotifier libraryNotifier,
        ILogger<ImportMediaPictureFromUrlCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _httpClientFactory = httpClientFactory;
        _pictureDeletionService = pictureDeletionService;
        _libraryNotifier = libraryNotifier;
        _logger = logger;
    }

    public async Task<Guid> Handle(ImportMediaPictureFromUrlCommand request, CancellationToken cancellationToken)
    {
        var media = await _context.Medias
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        await _pictureDeletionService.RemoveMediaPicturesByTypeAsync(
            request.MediaId,
            request.PictureType,
            cancellationToken);

        using var httpClient = _httpClientFactory.CreateClient();
        await using var responseStream = await httpClient.GetStreamAsync(request.Url, cancellationToken);

        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "medias", $"{request.MediaId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}.jpg");

        await using (var fs = File.Create(filePath))
        {
            await responseStream.CopyToAsync(fs, cancellationToken);
        }

        _logger.LogInformation("Imported picture from provider for media {MediaId} to {Path}", request.MediaId, filePath);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = request.PictureType,
            MediaId = request.MediaId,
            LocalPath = filePath
        };

        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        await _libraryNotifier.NotifyMediaPicturesUpdatedAsync(request.MediaId, cancellationToken);

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
