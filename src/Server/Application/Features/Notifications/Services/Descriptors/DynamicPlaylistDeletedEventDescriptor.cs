using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DynamicPlaylistDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DynamicPlaylistDeletedEvent);
    public string DisplayNameKey => "EventDynamicPlaylistDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Dynamic playlist deleted";
    public string DefaultBodyTemplate => "Dynamic playlist {{DynamicPlaylist.Title}} was removed.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DynamicPlaylistTitle,
        NotificationParams.DynamicPlaylistDescription,
        NotificationParams.DynamicPlaylistMediaType
    ];
}
