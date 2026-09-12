using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceDeletedEvent);
    public string DisplayNameKey => "EventDeviceDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "Device removed";
    public string DefaultBodyTemplate => "Device {{Device.DeviceName}} was removed.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DeviceName,
        NotificationParams.DeviceDeviceType,
        NotificationParams.DeviceClientType,
        NotificationParams.DeviceOs,
        NotificationParams.DeviceOsVersion
    ];
}
