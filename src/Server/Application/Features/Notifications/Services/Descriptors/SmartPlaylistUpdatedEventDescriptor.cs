using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class SmartPlaylistUpdatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(SmartPlaylistUpdatedEvent);
    public string DisplayName => "Smart Playlist Updated";
    public NotificationEventCategory Category => NotificationEventCategory.Playlist;
    public string DefaultTitleTemplate => "Smart Playlist Updated";
    public string DefaultBodyTemplate => "{{SmartPlaylist.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("SmartPlaylist.Title", "Title", "String"),
        new("SmartPlaylist.Description", "Description", "String"),
        new("SmartPlaylist.MediaType", "Media Type", "String"),
        new("SmartPlaylist.Limit", "Limit", "Int"),
        new("SmartPlaylist.OrderBy", "Order By", "String"),
    ];
}
