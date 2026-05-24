using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class CollectionCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(CollectionCreatedEvent);
    public string DisplayName => "Collection Created";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Collection Created";
    public string DefaultBodyTemplate => "{{Collection.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Collection.Title", "Title", "String"),
        new("Collection.Description", "Description", "String"),
        new("Collection.IsPublic", "Is Public", "String"),
        new("Collection.MediaType", "Media Type", "String"),
        new("Collection.Items.Count", "Items Count", "Int"),
    ];
}
