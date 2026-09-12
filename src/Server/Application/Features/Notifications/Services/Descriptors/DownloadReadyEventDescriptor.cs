using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DownloadReadyEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DownloadReadyEvent);
    public string DisplayNameKey => "EventDownloadReadyEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Download;
    public string DefaultTitleTemplate => "Download ready";
    public string DefaultBodyTemplate => "Your download is ready ({{Download.ContentType}}, {{Download.FileSize}} bytes, direct={{Download.IsDirectStream}}).";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.DownloadStatus,
        NotificationParams.DownloadIsDirect,
        NotificationParams.DownloadContentType,
        NotificationParams.DownloadFileSize,
        NotificationParams.DownloadIndexedFileId,
        NotificationParams.DownloadDeviceId,
        NotificationParams.DownloadUserId
    ];
}
