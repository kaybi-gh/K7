using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DynamicPlaylistUpdatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DynamicPlaylistUpdatedEvent);
    public string DisplayNameKey => "EventDynamicPlaylistUpdatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Dynamic playlist updated";
    public string DefaultBodyTemplate => "Dynamic playlist {{DynamicPlaylist.Title}} was updated.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DynamicPlaylistTitle,
        NotificationParams.DynamicPlaylistDescription,
        NotificationParams.DynamicPlaylistMediaType,
        NotificationParams.DynamicPlaylistLimit,
        NotificationParams.DynamicPlaylistOrderBy
    ];
}
