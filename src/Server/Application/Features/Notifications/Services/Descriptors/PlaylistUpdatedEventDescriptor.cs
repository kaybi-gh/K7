using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistUpdatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistUpdatedEvent);
    public string DisplayNameKey => "EventPlaylistUpdatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Playlist updated";
    public string DefaultBodyTemplate => "Playlist {{Playlist.Title}} was updated.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PlaylistTitle,
        NotificationParams.PlaylistDescription,
        NotificationParams.PlaylistMediaType,
        NotificationParams.PlaylistItemsCount
    ];
}
