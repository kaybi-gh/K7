using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class ApiKeyCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(ApiKeyCreatedEvent);
    public string DisplayNameKey => "EventApiKeyCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Security;
    public string DefaultTitleTemplate => "API key created";
    public string DefaultBodyTemplate => "API key {{ApiKey.Name}} ({{ApiKey.Scope}}, {{ApiKey.KeyPrefix}}) was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.ApiKeyName,
        NotificationParams.ApiKeyScope,
        NotificationParams.ApiKeyKeyPrefix,
        NotificationParams.UserName,
        NotificationParams.UserId
    ];
}
