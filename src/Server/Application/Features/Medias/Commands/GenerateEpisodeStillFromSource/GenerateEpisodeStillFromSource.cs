using FluentValidation.Results;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Medias.Commands.GenerateEpisodeStillFromSource;

public record GenerateEpisodeStillFromSourceCommand : IRequest<Guid?>
{
    public required Guid MediaId { get; init; }
}

public class GenerateEpisodeStillFromSourceCommandHandler : IRequestHandler<GenerateEpisodeStillFromSourceCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IEpisodeStillGenerator _episodeStillGenerator;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly MetadataPictureDeletionService _pictureDeletionService;
    private readonly ILibraryNotifier _libraryNotifier;
    private readonly ILogger<GenerateEpisodeStillFromSourceCommandHandler> _logger;

    public GenerateEpisodeStillFromSourceCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IEpisodeStillGenerator episodeStillGenerator,
        IOptions<PathsConfiguration> pathsConfiguration,
        MetadataPictureDeletionService pictureDeletionService,
        ILibraryNotifier libraryNotifier,
        ILogger<GenerateEpisodeStillFromSourceCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _episodeStillGenerator = episodeStillGenerator;
        _pathsConfiguration = pathsConfiguration.Value;
        _pictureDeletionService = pictureDeletionService;
        _libraryNotifier = libraryNotifier;
        _logger = logger;
    }

    public async Task<Guid?> Handle(GenerateEpisodeStillFromSourceCommand request, CancellationToken cancellationToken)
    {
        var episode = await _context.Medias
            .OfType<SerieEpisode>()
            .Include(e => e.Pictures)
            .Include(e => e.Segments)
            .FirstOrDefaultAsync(e => e.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, episode);

        if (episode.IsPictureTypeLocked(MetadataPictureType.Still))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(SerieEpisode.Pictures), "Still pictures are locked for this episode.")
            ]);
        }

        var indexedFile = await _context.IndexedFiles
            .Include(f => f.FileMetadata)
            .Where(f => f.MediaId == episode.Id && f.FileMetadata != null)
            .OrderByDescending(f => f.LastModified)
            .FirstOrDefaultAsync(cancellationToken);

        if (indexedFile is null || string.IsNullOrWhiteSpace(indexedFile.Path))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(IndexedFile), "No indexed video file is linked to this episode.")
            ]);
        }

        if (indexedFile.FileMetadata is not VideoFileMetadata videoMetadata)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(IndexedFile), "The linked file is not a video.")
            ]);
        }

        if (!File.Exists(indexedFile.Path))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(IndexedFile), "The linked video file was not found on disk.")
            ]);
        }

        var durationSeconds = videoMetadata.Duration.TotalSeconds;
        if (durationSeconds < 30)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(VideoFileMetadata.Duration), "The video is too short to generate a still.")
            ]);
        }

        var introEndSeconds = episode.Segments
            .Where(segment => segment.Type == MediaSegmentType.Intro)
            .Select(segment => segment.EndMs / 1000.0)
            .DefaultIfEmpty()
            .Max();

        double? introEnd = introEndSeconds > 0 ? introEndSeconds : null;

        await _pictureDeletionService.RemoveMediaPicturesByTypeAsync(
            episode.Id,
            MetadataPictureType.Still,
            cancellationToken);

        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "medias", $"{episode.Id}");
        var filePath = Path.Combine(directory, $"{pictureId}.jpg");

        var generationResult = await _episodeStillGenerator.GenerateAsync(
            indexedFile.Path,
            filePath,
            durationSeconds,
            introEnd,
            cancellationToken);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = MetadataPictureType.Still,
            MediaId = episode.Id,
            LocalPath = generationResult.FilePath,
            OriginalWidth = generationResult.Width > 0 ? generationResult.Width : null,
            OriginalHeight = generationResult.Height > 0 ? generationResult.Height : null
        };

        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Generated episode still for {MediaId} at {TimestampSeconds}s ({Width}x{Height})",
            episode.Id,
            generationResult.TimestampSeconds,
            generationResult.Width,
            generationResult.Height);

        await _libraryNotifier.NotifyMediaPicturesUpdatedAsync(episode.Id, cancellationToken);

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
