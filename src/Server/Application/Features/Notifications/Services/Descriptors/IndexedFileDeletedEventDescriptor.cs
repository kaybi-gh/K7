using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class IndexedFileDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(IndexedFileDeletedEvent);
    public string DisplayName => "File Removed from Library";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "File Removed";
    public string DefaultBodyTemplate => "{{IndexedFile.Name}} removed from library";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("IndexedFile.Name", "File Name", "String"),
        new("IndexedFile.Extension", "Extension", "String"),
        new("IndexedFile.Path", "File Path", "String"),
        new("IndexedFile.ParentDirectory", "Parent Directory", "String"),
        new("IndexedFile.Size", "File Size (bytes)", "Int"),
        new("IndexedFile.LibraryId", "Library ID", "String"),
    ];
}
