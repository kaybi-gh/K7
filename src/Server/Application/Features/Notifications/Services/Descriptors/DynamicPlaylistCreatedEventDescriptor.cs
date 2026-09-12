using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DynamicPlaylistCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DynamicPlaylistCreatedEvent);
    public string DisplayNameKey => "EventDynamicPlaylistCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Dynamic playlist created";
    public string DefaultBodyTemplate => "Dynamic playlist {{DynamicPlaylist.Title}} ({{DynamicPlaylist.MediaType}}) was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DynamicPlaylistTitle,
        NotificationParams.DynamicPlaylistDescription,
        NotificationParams.DynamicPlaylistMediaType,
        NotificationParams.DynamicPlaylistLimit,
        NotificationParams.DynamicPlaylistOrderBy,
        NotificationParams.DynamicPlaylistOrderDirection
    ];
}
