using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaylistCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaylistCreatedEvent);
    public string DisplayName => "Playlist Created";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Playlist.Title", "Playlist Title", "String"),
        new("Playlist.IsPublic", "Is Public", "String"),
    ];
}
