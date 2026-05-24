using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistItemRemovedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistItemRemovedEvent);
    public string DisplayName => "Track Removed from Playlist";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Track Removed";
    public string DefaultBodyTemplate => "Track removed from {{Playlist.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Playlist.Title", "Playlist Title", "String"),
        new("Playlist.Description", "Description", "String"),
        new("Playlist.MediaType", "Media Type", "String"),
        new("Playlist.Items.Count", "Items Count", "Int"),
    ];
}
