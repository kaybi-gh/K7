using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class CollectionDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(CollectionDeletedEvent);
    public string DisplayNameKey => "EventCollectionDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Collection deleted";
    public string DefaultBodyTemplate => "Collection {{Collection.Title}} was removed.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.CollectionTitle,
        NotificationParams.CollectionDescription,
        NotificationParams.CollectionMediaType
    ];
}
