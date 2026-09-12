using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class UserDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(UserDeletedEvent);
    public string DisplayNameKey => "EventUserDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.User;
    public string DefaultTitleTemplate => "User deleted";
    public string DefaultBodyTemplate => "User {{User.Name}} ({{User.Role}}) was deleted.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        NotificationParams.UserEmail,
        NotificationParams.UserRole
    ];
}
