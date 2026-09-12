using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaybackStateChangedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaybackStateChangedEvent);
    public string DisplayNameKey => "EventPlaybackStateChangedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Playback;
    public string DefaultTitleTemplate => "Playback";
    public string DefaultBodyTemplate => "{{UserName}} {{State}} {{MediaTitle}} on {{DeviceName}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters => NotificationParams.PlaybackStateChanged;
}
