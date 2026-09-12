using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Scrobbling.EventHandlers;

public class ScrobblePlaybackStateChangedHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<ScrobblePlaybackStateChangedHandler> logger)
    : INotificationHandler<PlaybackStateChangedEvent>
{
    public Task Handle(PlaybackStateChangedEvent notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ScrobbleDispatcher>();
            try
            {
                await dispatcher.DispatchAsync(notification, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scrobble dispatch failed for playback state change");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }
}
