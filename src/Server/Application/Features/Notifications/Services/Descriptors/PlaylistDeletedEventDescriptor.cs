using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistDeletedEvent);
    public string DisplayName => "Playlist Deleted";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist Deleted";
    public string DefaultBodyTemplate => "{{Playlist.Title}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Playlist.Title", "Title", "String"),
        new("Playlist.Description", "Description", "String"),
        new("Playlist.MediaType", "Media Type", "String"),
        new("Playlist.Items.Count", "Items Count", "Int"),
    ];
}
