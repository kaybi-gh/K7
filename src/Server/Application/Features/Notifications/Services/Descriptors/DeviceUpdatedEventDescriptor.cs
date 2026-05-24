using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceUpdatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceUpdatedEvent);
    public string DisplayName => "Device Updated";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "Device Updated";
    public string DefaultBodyTemplate => "{{Device.DeviceName}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Device.DeviceName", "Device Name", "String"),
        new("Device.DeviceType", "Device Type", "String"),
        new("Device.ClientType", "Client Type", "String"),
        new("Device.OperatingSystem", "Operating System", "String"),
        new("Device.OperatingSystemVersion", "OS Version", "String"),
        new("Device.DisplayWidth", "Display Width", "Float"),
        new("Device.DisplayHeight", "Display Height", "Float"),
        new("Device.LastSeen", "Last Seen", "String"),
    ];
}
