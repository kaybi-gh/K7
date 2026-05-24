using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PlaybackStateChangedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PlaybackStateChangedEvent);
    public string DisplayName => "Playback State Changed";
    public NotificationEventCategory Category => NotificationEventCategory.Playback;
    public string DefaultTitleTemplate => "{{UserName}} - {{State}}";
    public string DefaultBodyTemplate => "{{MediaTitle}} on {{DeviceName}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("State", "Playback State", "String"),
        new("PreviousState", "Previous State", "String"),
        new("UserName", "User Name", "String"),
        new("UserId", "User ID", "String"),
        new("MediaTitle", "Media Title", "String"),
        new("MediaType", "Media Type", "String"),
        new("LibraryTitle", "Library Name", "String"),
        new("DeviceName", "Device Name", "String"),
        new("DeviceType", "Device Type", "String"),
        new("Position", "Position (seconds)", "Float"),
        new("Duration", "Duration (seconds)", "Float"),
    ];
}
