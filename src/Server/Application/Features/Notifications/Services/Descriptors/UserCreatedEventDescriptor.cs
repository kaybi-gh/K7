using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class UserCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(UserCreatedEvent);
    public string DisplayNameKey => "EventUserCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.User;
    public string DefaultTitleTemplate => "User created";
    public string DefaultBodyTemplate => "User {{User.Name}} ({{User.Role}}) was created via {{User.Origin}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        NotificationParams.UserEmail,
        NotificationParams.UserRole,
        NotificationParams.UserOrigin
    ];
}
