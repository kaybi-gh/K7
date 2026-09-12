using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaRatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaRatedEvent);
    public string DisplayNameKey => "EventMediaRatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "Media rated";
    public string DefaultBodyTemplate => "{{User.Name}} rated {{Media.Title}} {{Rating.Value}}/10.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        NotificationParams.RatingValue,
        NotificationParams.RatingIsNew,
        ..NotificationParams.MediaCore
    ];
}
