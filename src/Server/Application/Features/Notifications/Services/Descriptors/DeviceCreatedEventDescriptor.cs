using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceCreatedEvent);
    public string DisplayName => "New Device Connected";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Device.Name", "Device Name", "String"),
        new("Device.DeviceType", "Device Type", "String"),
        new("Device.ClientName", "Client Name", "String"),
    ];
}
