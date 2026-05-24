using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistItemAddedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistItemAddedEvent);
    public string DisplayName => "Track Added to Playlist";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Track Added";
    public string DefaultBodyTemplate => "New track added to {{Playlist.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Playlist.Title", "Playlist Title", "String"),
        new("Playlist.Description", "Description", "String"),
        new("Playlist.MediaType", "Media Type", "String"),
        new("Playlist.Items.Count", "Items Count", "Int"),
        new("Item.MediaId", "Media ID", "String"),
        new("Item.Order", "Position", "Int"),
    ];
}
