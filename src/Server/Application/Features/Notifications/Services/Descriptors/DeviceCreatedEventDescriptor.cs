using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceCreatedEvent);
    public string DisplayNameKey => "EventDeviceCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "New device";
    public string DefaultBodyTemplate => "Device {{Device.DeviceName}} ({{Device.DeviceType}} / {{Device.ClientType}}) was registered.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DeviceName,
        NotificationParams.DeviceDeviceType,
        NotificationParams.DeviceClientType,
        NotificationParams.DeviceOs,
        NotificationParams.DeviceOsVersion,
        NotificationParams.DeviceScreenWidth,
        NotificationParams.DeviceScreenHeight,
        NotificationParams.DeviceResWidth,
        NotificationParams.DeviceResHeight,
        NotificationParams.DeviceUniqueId
    ];
}
