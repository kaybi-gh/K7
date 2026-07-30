using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DynamicPlaylistCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DynamicPlaylistCreatedEvent);
    public string DisplayName => "Dynamic Playlist Created";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Dynamic Playlist Created";
    public string DefaultBodyTemplate => "{{DynamicPlaylist.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("DynamicPlaylist.Title", "Title", "String"),
        new("DynamicPlaylist.Description", "Description", "String"),
        new("DynamicPlaylist.MediaType", "Media Type", "String"),
        new("DynamicPlaylist.Limit", "Limit", "Int"),
        new("DynamicPlaylist.OrderBy", "Order By", "String"),
        new("DynamicPlaylist.OrderDirection", "Order Direction", "String"),
    ];
}
