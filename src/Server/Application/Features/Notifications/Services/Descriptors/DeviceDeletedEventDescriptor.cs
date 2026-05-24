using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceDeletedEvent);
    public string DisplayName => "Device Removed";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "Device Removed";
    public string DefaultBodyTemplate => "{{Device.DeviceName}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Device.DeviceName", "Device Name", "String"),
        new("Device.DeviceType", "Device Type", "String"),
        new("Device.ClientType", "Client Type", "String"),
        new("Device.OperatingSystem", "Operating System", "String"),
        new("Device.OperatingSystemVersion", "OS Version", "String"),
    ];
}
