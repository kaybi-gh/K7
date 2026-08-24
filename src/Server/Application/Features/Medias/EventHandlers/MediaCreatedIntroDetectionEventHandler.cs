using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

/// <summary>
/// Queues intro/outro detection when an episode media is created after its file has been probed.
/// </summary>
/// <remarks>
/// A first library scan enqueues probes while files are being persisted and media creation only once
/// identification has grouped files, so an episode is usually probed before its media exists. In that
/// order <see cref="IndexedFiles.EventHandlers.FileMetadataCreatedEventHandler"/> cannot queue
/// detection because the file has no MediaId yet. Attaching a probed file to an existing episode
/// stub (metadata refresh created the episode first) is handled from CreateMedia / reidentify.
/// </remarks>
public class MediaCreatedIntroDetectionEventHandler(
    ILogger<MediaCreatedIntroDetectionEventHandler> logger,
    ISender sender,
    IApplicationDbContext context) : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Media is not SerieEpisode episode)
            return;

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(context, sender, episode.Id, logger, cancellationToken);
    }
}
