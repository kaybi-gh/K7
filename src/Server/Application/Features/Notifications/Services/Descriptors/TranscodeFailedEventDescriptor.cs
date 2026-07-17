using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class TranscodeFailedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(TranscodeFailedEvent);
    public string DisplayName => "Transcode Failed";
    public NotificationEventCategory Category => NotificationEventCategory.Health;
    public string DefaultTitleTemplate => "Transcode failed";
    public string DefaultBodyTemplate => "{{MediaTitle}}: {{ErrorMessage}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("IndexedFileId", "Indexed File Id", "Guid"),
        new("MediaTitle", "Media Title", "String"),
        new("ErrorMessage", "Error Message", "String"),
    ];
}
