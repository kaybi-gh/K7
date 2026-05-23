using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaPlaybackCompletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => "MediaPlaybackCompletedEvent";
    public string DisplayName => "Media Playback Completed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Session.UserId", "User ID", "String"),
        new("Session.MediaId", "Media ID", "String"),
        new("Session.Duration", "Duration (seconds)", "Float"),
        new("Media.Title", "Media Title", "String"),
        new("Media.MediaType", "Media Type", "String"),
    ];
}
