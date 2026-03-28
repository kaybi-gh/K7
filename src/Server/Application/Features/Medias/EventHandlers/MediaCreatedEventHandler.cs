using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler(ISender sender, ILogger<MediaCreatedEventHandler> logger)
    : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        if (notification.Media is MusicTrack track)
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new AnalyzeMusicTrackAudioCommand { TrackId = track.Id },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = track.Id,
                TargetEntityTypeName = nameof(MusicTrack),
                MaxAttempts = 2
            }, cancellationToken);
        }
    }
}
