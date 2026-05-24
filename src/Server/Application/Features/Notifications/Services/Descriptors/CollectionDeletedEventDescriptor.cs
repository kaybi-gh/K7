using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class CollectionDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(CollectionDeletedEvent);
    public string DisplayName => "Collection Deleted";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Collection Deleted";
    public string DefaultBodyTemplate => "{{Collection.Title}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Collection.Title", "Title", "String"),
        new("Collection.Description", "Description", "String"),
        new("Collection.MediaType", "Media Type", "String"),
    ];
}
