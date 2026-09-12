using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistCreatedEvent);
    public string DisplayNameKey => "EventPlaylistCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist created";
    public string DefaultBodyTemplate => "Playlist {{Playlist.Title}} was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PlaylistTitle,
        NotificationParams.PlaylistDescription,
        NotificationParams.PlaylistMediaType,
        NotificationParams.PlaylistItemsCount
    ];
}
