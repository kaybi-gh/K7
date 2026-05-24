using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaPlaybackCompletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => "MediaPlaybackCompletedEvent";
    public string DisplayName => "Media Playback Completed";
    public NotificationEventCategory Category => NotificationEventCategory.Playback;
    public string DefaultTitleTemplate => "Playback Completed";
    public string DefaultBodyTemplate => "{{Media.Title}} - watched {{Session.WatchedDurationSeconds}}s";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Session.UserId", "User ID", "String"),
        new("Session.MediaId", "Media ID", "String"),
        new("Session.DurationSeconds", "Duration (seconds)", "Float"),
        new("Session.WatchedDurationSeconds", "Watched Duration (seconds)", "Float"),
        new("Session.State", "Playback State", "String"),
        new("Session.DeviceId", "Device ID", "String"),
        new("Media.Title", "Media Title", "String"),
        new("Media.Type", "Media Type", "String"),
    ];
}
