using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class ApiKeyRevokedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(ApiKeyRevokedEvent);
    public string DisplayNameKey => "EventApiKeyRevokedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Security;
    public string DefaultTitleTemplate => "API key revoked";
    public string DefaultBodyTemplate => "API key {{ApiKey.Name}} ({{ApiKey.Scope}}) was revoked.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.ApiKeyName,
        NotificationParams.ApiKeyScope,
        NotificationParams.ApiKeyKeyPrefix,
        NotificationParams.UserName,
        NotificationParams.UserId
    ];
}
