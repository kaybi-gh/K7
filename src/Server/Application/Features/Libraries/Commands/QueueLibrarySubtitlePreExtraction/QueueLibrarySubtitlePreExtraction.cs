using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.PreExtractSubtitles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Libraries.Commands.QueueLibrarySubtitlePreExtraction;

/// <summary>
/// Queues a pre-extraction task for every video in the library that has a text subtitle track.
/// Used when the capability is turned on for a library that already has files.
/// </summary>
[Authorize(Roles = Roles.Administrator)]
public record QueueLibrarySubtitlePreExtractionCommand(Guid LibraryId) : IRequest<int>;

public class QueueLibrarySubtitlePreExtractionCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    ILogger<QueueLibrarySubtitlePreExtractionCommandHandler> logger)
    : IRequestHandler<QueueLibrarySubtitlePreExtractionCommand, int>
{
    public async Task<int> Handle(QueueLibrarySubtitlePreExtractionCommand request, CancellationToken cancellationToken)
    {
        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LibraryId, cancellationToken);

        Guard.Against.NotFound(request.LibraryId, library);

        if (!library.SubtitlePreExtractionEnabled)
            return 0;

        var fileIds = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.LibraryId == library.Id && f.FileMetadata is VideoFileMetadata)
            .Where(f => context.FileTracks.OfType<SubtitleFileTrack>().Any(t =>
                t.VideoFileMetadataId == f.FileMetadata!.Id && t.IsTextBased))
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        if (fileIds.Count == 0)
            return 0;

        var items = fileIds.Select(fileId => new CreateBackgroundTasksBatchItem
        {
            Request = new PreExtractSubtitlesCommand { Id = fileId },
            TargetEntityId = fileId,
            TargetEntityTypeName = nameof(IndexedFile),
            Lane = BackgroundTaskLane.FfmpegPrepare,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 1,
            TimeoutSeconds = PreExtractSubtitlesCommand.TimeoutSeconds
        }).ToList();

        await sender.Send(new CreateBackgroundTasksBatchCommand(items), cancellationToken);

        logger.LogInformation(
            "Queued subtitle pre-extraction for {Count} files in library {LibraryId}",
            fileIds.Count,
            library.Id);

        return fileIds.Count;
    }
}
