using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistItemAddedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistItemAddedEvent);
    public string DisplayNameKey => "EventPlaylistItemAddedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist item added";
    public string DefaultBodyTemplate => "An item was added to playlist {{Playlist.Title}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PlaylistTitle,
        NotificationParams.PlaylistDescription,
        NotificationParams.PlaylistMediaType,
        NotificationParams.PlaylistItemsCount,
        NotificationParams.PlaylistItemMediaId,
        NotificationParams.PlaylistItemOrder
    ];
}
