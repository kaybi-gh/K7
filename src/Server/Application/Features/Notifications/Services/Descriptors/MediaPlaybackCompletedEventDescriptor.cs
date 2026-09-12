using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaPlaybackCompletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => "MediaPlaybackCompletedEvent";
    public string DisplayNameKey => "EventMediaPlaybackCompletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playback;
    public string DefaultTitleTemplate => "Playback completed";
    public string DefaultBodyTemplate => "{{User.Name}} finished {{Media.Title}} after {{Session.WatchedDurationSeconds}}s ({{Session.ProgressPercent}}%).";
    public IReadOnlyList<NotificationParameterInfo> Parameters => NotificationParams.PlaybackCompleted;
}
