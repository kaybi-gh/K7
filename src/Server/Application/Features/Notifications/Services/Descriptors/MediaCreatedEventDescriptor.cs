using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaCreatedEvent);
    public string DisplayNameKey => "EventMediaCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "Media created";
    public string DefaultBodyTemplate => "{{Media.Title}} ({{Media.Type}}) was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.MediaTitle,
        NotificationParams.MediaOriginalTitle,
        NotificationParams.MediaType,
        NotificationParams.MediaReleaseDate,
        NotificationParams.MediaYear,
        NotificationParams.MediaGenresCount,
        NotificationParams.MediaIndexedFilesCount
    ];
}
