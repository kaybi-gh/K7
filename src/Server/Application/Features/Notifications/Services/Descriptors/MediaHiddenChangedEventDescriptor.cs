using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaHiddenChangedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaHiddenChangedEvent);
    public string DisplayNameKey => "EventMediaHiddenChangedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.User;
    public string DefaultTitleTemplate => "Media visibility changed";
    public string DefaultBodyTemplate => "{{User.Name}} hide state for {{Media.Title}}: hidden={{Hidden.IsHidden}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        NotificationParams.HiddenIsHidden,
        NotificationParams.HiddenIsSelfExcluded,
        NotificationParams.HiddenIsAdminExcluded,
        ..NotificationParams.MediaCore
    ];
}
