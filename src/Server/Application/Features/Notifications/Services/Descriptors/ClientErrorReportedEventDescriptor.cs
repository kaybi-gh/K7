using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class ClientErrorReportedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(ClientErrorReportedEvent);
    public string DisplayNameKey => "EventClientErrorReportedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Health;
    public string DefaultTitleTemplate => "Client error";
    public string DefaultBodyTemplate =>
        "{{UserName}} on {{DeviceName}}: {{Message}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.ClientErrorMessage,
        NotificationParams.ClientErrorSource,
        NotificationParams.ClientErrorStackTrace,
        NotificationParams.ClientErrorDeviceId,
        NotificationParams.ClientErrorDeviceName,
        NotificationParams.ClientErrorUserName
    ];
}
