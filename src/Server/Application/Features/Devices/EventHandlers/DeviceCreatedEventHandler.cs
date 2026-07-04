using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Devices.Commands;

public class DeviceCreatedEventHandler : INotificationHandler<DeviceCreatedEvent>
{
    private readonly ILogger<DeviceCreatedEventHandler> _logger;

    public DeviceCreatedEventHandler(ILogger<DeviceCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DeviceCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
