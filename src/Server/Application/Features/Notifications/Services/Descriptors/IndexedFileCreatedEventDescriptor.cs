using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class IndexedFileCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(IndexedFileCreatedEvent);
    public string DisplayName => "File Added to Library";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "File Added";
    public string DefaultBodyTemplate => "{{IndexedFile.Name}} added to library";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("IndexedFile.Name", "File Name", "String"),
        new("IndexedFile.Extension", "Extension", "String"),
        new("IndexedFile.Path", "File Path", "String"),
        new("IndexedFile.ParentDirectory", "Parent Directory", "String"),
        new("IndexedFile.Size", "File Size (bytes)", "Int"),
        new("IndexedFile.LibraryId", "Library ID", "String"),
        new("FileType", "File Type", "String"),
    ];
}
