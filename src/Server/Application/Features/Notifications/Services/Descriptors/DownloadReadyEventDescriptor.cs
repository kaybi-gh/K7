using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class DownloadReadyEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(DownloadReadyEvent);
    public string DisplayName => "Download Ready";
    public NotificationEventCategory Category => NotificationEventCategory.Download;
    public string DefaultTitleTemplate => "Download Ready";
    public string DefaultBodyTemplate => "Your download is ready ({{Download.ContentType}})";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Download.Status", "Status", "String"),
        new("Download.IsDirectStream", "Is Direct Stream", "String"),
        new("Download.ContentType", "Content Type", "String"),
        new("Download.FileSize", "File Size (bytes)", "Int"),
        new("Download.IndexedFileId", "File ID", "String"),
        new("Download.DeviceId", "Device ID", "String"),
        new("Download.UserId", "User ID", "String"),
    ];
}
