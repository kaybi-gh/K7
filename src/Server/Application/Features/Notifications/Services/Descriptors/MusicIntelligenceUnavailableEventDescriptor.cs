using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MusicIntelligenceUnavailableEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MusicIntelligenceUnavailableEvent);
    public string DisplayName => "Music Intelligence Unavailable";
    public NotificationEventCategory Category => NotificationEventCategory.Health;
    public string DefaultTitleTemplate => "Music intelligence unavailable";
    public string DefaultBodyTemplate => "AudioMuse is unreachable{{Reason}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Reason", "Reason", "String"),
    ];
}
