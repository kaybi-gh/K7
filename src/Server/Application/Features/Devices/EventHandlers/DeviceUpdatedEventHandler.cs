using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Devices.Commands;

public class DeviceUpdatedEventHandler : INotificationHandler<DeviceUpdatedEvent>
{
    private readonly ILogger<DeviceUpdatedEventHandler> _logger;

    public DeviceUpdatedEventHandler(ILogger<DeviceUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DeviceUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
