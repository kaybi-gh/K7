using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class ClientAppPasswordCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(ClientAppPasswordCreatedEvent);
    public string DisplayNameKey => "EventClientAppPasswordCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Security;
    public string DefaultTitleTemplate => "App password created";
    public string DefaultBodyTemplate => "Subsonic app password {{ClientAppPassword.Name}} was created for {{User.Name}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.ClientAppPasswordName,
        NotificationParams.UserName,
        NotificationParams.UserId
    ];
}
