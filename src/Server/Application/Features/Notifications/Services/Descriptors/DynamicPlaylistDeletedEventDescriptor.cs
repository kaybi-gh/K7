using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DynamicPlaylistDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DynamicPlaylistDeletedEvent);
    public string DisplayName => "Dynamic Playlist Deleted";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Dynamic Playlist Deleted";
    public string DefaultBodyTemplate => "{{DynamicPlaylist.Title}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("DynamicPlaylist.Title", "Title", "String"),
        new("DynamicPlaylist.Description", "Description", "String"),
        new("DynamicPlaylist.MediaType", "Media Type", "String"),
    ];
}
