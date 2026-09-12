using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistItemRemovedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistItemRemovedEvent);
    public string DisplayNameKey => "EventPlaylistItemRemovedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist item removed";
    public string DefaultBodyTemplate => "An item was removed from playlist {{Playlist.Title}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PlaylistTitle,
        NotificationParams.PlaylistDescription,
        NotificationParams.PlaylistMediaType,
        NotificationParams.PlaylistItemsCount
    ];
}
