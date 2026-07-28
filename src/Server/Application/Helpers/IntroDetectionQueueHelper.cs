using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Queues intro/outro detection for the season an episode belongs to.
/// </summary>
/// <remarks>
/// Detection needs both the probed file metadata and the episode media to exist, and those two are
/// produced by tasks that run in different lanes. It is therefore triggered from both sides: when a
/// file is probed and when an episode media is created. Whichever happens last wins; the earlier
/// attempt is a no-op because <see cref="CreateBackgroundTaskCommand"/> deduplicates pending tasks
/// on name plus target entity.
/// </remarks>
public static class IntroDetectionQueueHelper
{
    private const int MinimumEpisodesPerSeason = 2;

    /// <summary>
    /// Queues detection for the season of <paramref name="mediaId"/> when it is an episode of a
    /// season holding at least two episodes.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="sender">Mediator used to create the background task.</param>
    /// <param name="mediaId">Identifier of the media expected to be a <see cref="SerieEpisode"/>.</param>
    /// <param name="logger">Logger used to trace skipped cases.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task TryQueueForEpisodeAsync(
        IApplicationDbContext context,
        ISender sender,
        Guid mediaId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == mediaId, cancellationToken);

        if (episode is null)
        {
            logger.LogDebug("Intro detection skipped: media {MediaId} is not a SerieEpisode", mediaId);
            return;
        }

        var episodeCount = await context.Medias
            .OfType<SerieEpisode>()
            .CountAsync(e => e.SeasonId == episode.SeasonId, cancellationToken);

        if (episodeCount < MinimumEpisodesPerSeason)
        {
            logger.LogDebug("Intro detection skipped: season {SeasonId} has only {Count} episode(s)", episode.SeasonId, episodeCount);
            return;
        }

        logger.LogInformation("Queuing intro detection for season {SeasonId} ({EpisodeCount} episodes)", episode.SeasonId, episodeCount);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = episode.SeasonId },
            TargetEntityId = episode.SeasonId,
            TargetEntityTypeName = nameof(SerieSeason),
            Lane = BackgroundTaskLane.MediaAnalysis,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 2
        }, cancellationToken);
    }
}
