using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

public static class ChapterExtractionHelper
{
    public static bool NeedsExtraction(VideoFileMetadata? metadata) =>
        metadata is not null && metadata.Chapters is null;

    public static async Task EnsureChaptersAsync(
        IApplicationDbContext context,
        ISender sender,
        Guid indexedFileId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.IndexedFiles
            .Include(f => f.FileMetadata)
            .FirstOrDefaultAsync(f => f.Id == indexedFileId, cancellationToken);

        if (entity?.FileMetadata is not VideoFileMetadata videoMetadata)
            return;

        if (!NeedsExtraction(videoMetadata))
            return;

        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == entity.LibraryId, cancellationToken);

        if (library is null || !library.ChapterExtractionEnabled)
            return;

        logger.LogInformation(
            "Chapters not available for IndexedFile {Id}, extracting synchronously",
            indexedFileId);

        await sender.Send(new ExtractChaptersCommand { Id = indexedFileId }, cancellationToken);
    }

    public static async Task QueueExtractionIfMissingAsync(
        ISender sender,
        Guid indexedFileId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Chapters not available for IndexedFile {Id}, queuing extraction",
            indexedFileId);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new ExtractChaptersCommand { Id = indexedFileId },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = indexedFileId,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 3,
            ConcurrencyGroup = "ffprobe"
        }, cancellationToken);
    }
}
