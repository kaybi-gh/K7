using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class IndexedFileCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(IndexedFileCreatedEvent);
    public string DisplayNameKey => "EventIndexedFileCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "File added";
    public string DefaultBodyTemplate => "File {{IndexedFile.Name}} was added ({{IndexedFile.Extension}}, {{IndexedFile.Size}} bytes).";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.IndexedFileName,
        NotificationParams.IndexedFileExtension,
        NotificationParams.IndexedFilePath,
        NotificationParams.IndexedFileParent,
        NotificationParams.IndexedFileSize,
        NotificationParams.IndexedFileLibraryId,
        NotificationParams.FileType
    ];
}
