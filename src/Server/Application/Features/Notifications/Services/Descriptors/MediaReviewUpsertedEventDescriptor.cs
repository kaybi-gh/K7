using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaReviewUpsertedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaReviewUpsertedEvent);
    public string DisplayNameKey => "EventMediaReviewUpsertedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "Review updated";
    public string DefaultBodyTemplate => "{{User.Name}} reviewed {{Media.Title}} ({{Rating.Value}}/10): {{Review.Text}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.UserName,
        NotificationParams.UserId,
        NotificationParams.RatingValue,
        NotificationParams.ReviewText,
        NotificationParams.ReviewEmoji,
        NotificationParams.ReviewIsNew,
        ..NotificationParams.MediaCore
    ];
}
