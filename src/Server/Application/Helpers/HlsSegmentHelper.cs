using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

public static class HlsSegmentHelper
{
    public const string FallbackTranscodingVideoCodec = "h264";

    public static async Task<IReadOnlyList<HlsSegment>> LoadSegmentsAsync(
        IApplicationDbContext context,
        Guid indexedFileId,
        CancellationToken cancellationToken = default)
    {
        return await context.HlsSegments
            .Where(s => s.IndexedFileId == indexedFileId)
            .OrderBy(s => s.Number)
            .ToListAsync(cancellationToken);
    }

    public static async Task<bool> HasSegmentsAsync(
        IApplicationDbContext context,
        Guid indexedFileId,
        CancellationToken cancellationToken = default)
    {
        return await context.HlsSegments
            .AnyAsync(s => s.IndexedFileId == indexedFileId, cancellationToken);
    }

    public static async Task QueueSegmentComputationIfMissingAsync(
        ISender sender,
        Guid indexedFileId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "HLS segments not available for IndexedFile {Id}, queuing segmentation",
            indexedFileId);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new ComputeHlsSegmentsCommand
            {
                Id = indexedFileId,
                SegmentsDuration = TimeSpan.FromSeconds(2)
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = indexedFileId,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 5,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }
}
