using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler(
    ISender sender,
    ILogger<MediaCreatedEventHandler> logger,
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context)
    : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        var flags = await GetFeatureFlagsAsync(cancellationToken);

        if (notification.Media is MusicTrack track && flags.MusicAudioAnalysisEnabled)
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new AnalyzeMusicTrackAudioCommand { TrackId = track.Id },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = track.Id,
                TargetEntityTypeName = nameof(MusicTrack),
                MaxAttempts = 2,
                ConcurrencyGroup = "ffmpeg"
            }, cancellationToken);
        }

        if (notification.Media is SerieEpisode episode && flags.IntroDetectionEnabled)
        {
            var episodeCount = await context.Medias
                .OfType<SerieEpisode>()
                .CountAsync(e => e.SeasonId == episode.SeasonId, cancellationToken);

            if (episodeCount >= 2)
            {
                await sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new DetectMediaSegmentsCommand { SeasonId = episode.SeasonId },
                    Priority = BackgroundTaskPriority.Low,
                    TargetEntityId = episode.SeasonId,
                    TargetEntityTypeName = nameof(SerieSeason),
                    MaxAttempts = 2,
                    ConcurrencyGroup = "ffmpeg"
                }, cancellationToken);
            }
        }
    }

    private async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}
