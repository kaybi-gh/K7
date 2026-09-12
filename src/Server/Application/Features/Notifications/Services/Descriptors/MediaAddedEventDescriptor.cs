using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaAddedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaAddedEvent);
    public string DisplayNameKey => "EventMediaAddedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "Media ready";
    public string DefaultBodyTemplate => "{{Media.Title}} ({{Media.Type}}) is ready in the library.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        ..NotificationParams.MediaCore,
        NotificationParams.MediaIndexedFilesCount
    ];
}
