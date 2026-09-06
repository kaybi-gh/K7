using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceCreatedEvent);
    public string DisplayName => "New Device Connected";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "New Device";
    public string DefaultBodyTemplate => "{{Device.DeviceName}} ({{Device.DeviceType}})";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Device.DeviceName", "Device Name", "String"),
        new("Device.DeviceType", "Device Type", "String"),
        new("Device.ClientType", "Client Type", "String"),
        new("Device.OperatingSystem", "Operating System", "String"),
        new("Device.OperatingSystemVersion", "OS Version", "String"),
        new("Device.DisplayScreenWidth", "Display Screen Width", "Float"),
        new("Device.DisplayScreenHeight", "Display Screen Height", "Float"),
        new("Device.DisplayResolutionWidth", "Display Resolution Width", "Float"),
        new("Device.DisplayResolutionHeight", "Display Resolution Height", "Float"),
        new("Device.DeviceUniqueId", "Device Unique ID", "String"),
    ];
}
