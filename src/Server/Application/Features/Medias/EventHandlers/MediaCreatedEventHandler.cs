using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler(
    ISender sender,
    ILogger<MediaCreatedEventHandler> logger,
    IApplicationDbContext context) : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        if (notification.Media is not MusicTrack track)
            return;

        var libraryId = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == track.Id)
            .Select(f => f.LibraryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (libraryId == Guid.Empty)
            return;

        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == libraryId, cancellationToken);

        if (library is null || !library.MusicAudioAnalysisEnabled)
            return;

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
}
