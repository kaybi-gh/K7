using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Notifications.Services;

public record NotificationParameterInfo(string Name, string DisplayName, string ValueType);

public interface INotificationEventDescriptor
{
    string EventTypeName { get; }
    string DisplayName { get; }
    NotificationEventCategory Category { get; }
    string DefaultTitleTemplate { get; }
    string DefaultBodyTemplate { get; }
    IReadOnlyList<NotificationParameterInfo> Parameters { get; }
}
