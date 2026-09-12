using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class IndexedFileDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(IndexedFileDeletedEvent);
    public string DisplayNameKey => "EventIndexedFileDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "File removed";
    public string DefaultBodyTemplate => "File {{IndexedFile.Name}} was removed from the library.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.IndexedFileName,
        NotificationParams.IndexedFileExtension,
        NotificationParams.IndexedFilePath,
        NotificationParams.IndexedFileParent,
        NotificationParams.IndexedFileSize,
        NotificationParams.IndexedFileLibraryId
    ];
}
