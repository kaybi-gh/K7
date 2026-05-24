using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class SmartPlaylistDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(SmartPlaylistDeletedEvent);
    public string DisplayName => "Smart Playlist Deleted";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Smart Playlist Deleted";
    public string DefaultBodyTemplate => "{{SmartPlaylist.Title}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("SmartPlaylist.Title", "Title", "String"),
        new("SmartPlaylist.Description", "Description", "String"),
        new("SmartPlaylist.MediaType", "Media Type", "String"),
    ];
}
