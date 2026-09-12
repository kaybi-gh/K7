using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Scrobbling.EventHandlers;

public class ScrobblePlaybackCompletedHandler<TMedia>(
    IServiceScopeFactory scopeFactory,
    ILogger<ScrobblePlaybackCompletedHandler<TMedia>> logger)
    : INotificationHandler<MediaPlaybackCompletedEvent<TMedia>>
    where TMedia : BaseMedia
{
    public Task Handle(MediaPlaybackCompletedEvent<TMedia> notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ScrobbleDispatcher>();
            try
            {
                await dispatcher.DispatchCompletedAsync(
                    notification.Session.SessionId,
                    notification.Session.UserId,
                    notification.Session.User?.UserName,
                    notification.Media.Id,
                    notification.Session.PositionSeconds,
                    notification.Session.DurationSeconds,
                    notification.Session.SharedProfileId,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scrobble dispatch failed for completed playback");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }
}
