using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DeviceUpdatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DeviceUpdatedEvent);
    public string DisplayNameKey => "EventDeviceUpdatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Device;
    public string DefaultTitleTemplate => "Device updated";
    public string DefaultBodyTemplate => "Device {{Device.DeviceName}} was updated ({{Device.Os}} {{Device.OsVersion}}).";
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
        NotificationParams.DeviceLastSeen
    ];
}
