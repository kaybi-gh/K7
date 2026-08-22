using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class SeriesPlaybackBookmarkRefreshEventHandler(
    IPlaybackBookmarkService bookmarkService,
    IApplicationDbContext context,
    ILogger<SeriesPlaybackBookmarkRefreshEventHandler> logger)
    : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Media is not SerieEpisode episode)
            return;

        var timeNow = DateTime.UtcNow;
        await bookmarkService.RefreshSeriesBookmarksForSerieAsync(episode.SerieId, timeNow, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogDebug(
            "Series playback bookmarks refreshed for new episode {EpisodeId} in serie {SerieId}",
            episode.Id,
            episode.SerieId);
    }
}
