using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class CollectionCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(CollectionCreatedEvent);
    public string DisplayNameKey => "EventCollectionCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Collection created";
    public string DefaultBodyTemplate => "Collection {{Collection.Title}} was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.CollectionTitle,
        NotificationParams.CollectionDescription,
        NotificationParams.CollectionIsPublic,
        NotificationParams.CollectionMediaType,
        NotificationParams.CollectionItemsCount
    ];
}
