using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class ClientAppPasswordRevokedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(ClientAppPasswordRevokedEvent);
    public string DisplayNameKey => "EventClientAppPasswordRevokedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Security;
    public string DefaultTitleTemplate => "App password revoked";
    public string DefaultBodyTemplate => "Subsonic app password {{ClientAppPassword.Name}} was revoked for {{User.Name}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.ClientAppPasswordName,
        NotificationParams.UserName,
        NotificationParams.UserId
    ];
}
