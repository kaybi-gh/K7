using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class TranscodeFailedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(TranscodeFailedEvent);
    public string DisplayNameKey => "EventTranscodeFailedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Health;
    public string DefaultTitleTemplate => "Transcode failed";
    public string DefaultBodyTemplate => "Transcode failed for {{MediaTitle}}: {{ErrorMessage}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.TranscodeIndexedFileId,
        NotificationParams.TranscodeMediaTitle,
        NotificationParams.TranscodeError
    ];
}
