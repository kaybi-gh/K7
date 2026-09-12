using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistDeletedEvent);
    public string DisplayNameKey => "EventPlaylistDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist deleted";
    public string DefaultBodyTemplate => "Playlist {{Playlist.Title}} was removed.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PlaylistTitle,
        NotificationParams.PlaylistDescription,
        NotificationParams.PlaylistMediaType,
        NotificationParams.PlaylistItemsCount
    ];
}
