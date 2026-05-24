using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.EventHandlers;

public class OutboundNotificationEventHandler<TEvent>(
    IServiceScopeFactory scopeFactory,
    NotificationEventDataSerializer serializer,
    ILogger<OutboundNotificationEventHandler<TEvent>> logger)
    : INotificationHandler<TEvent>
    where TEvent : BaseEvent
{
    public Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        var rawName = notification.GetType().Name;
        var eventTypeName = rawName.IndexOf('`') is var idx and >= 0 ? rawName[..idx] : rawName;
        var eventData = serializer.Serialize(notification);

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboundNotificationDispatcher>();

            try
            {
                await dispatcher.DispatchAsync(eventTypeName, eventData, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbound notification dispatch failed for event {EventType}", eventTypeName);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }
}
