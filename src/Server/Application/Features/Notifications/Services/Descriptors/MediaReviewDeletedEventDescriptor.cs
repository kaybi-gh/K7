using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaReviewDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaReviewDeletedEvent);
    public string DisplayNameKey => "EventMediaReviewDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "Review removed";
    public string DefaultBodyTemplate => "{{User.Name}} removed their review of {{Media.Title}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        ..NotificationParams.MediaCore
    ];
}
