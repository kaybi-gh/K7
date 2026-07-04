using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Devices.Commands;

public class DeviceDeletedEventHandler : INotificationHandler<DeviceDeletedEvent>
{
    private readonly ILogger<DeviceDeletedEvent> _logger;

    public DeviceDeletedEventHandler(ILogger<DeviceDeletedEvent> logger)
    {
        _logger = logger;
    }

    public Task Handle(DeviceDeletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
