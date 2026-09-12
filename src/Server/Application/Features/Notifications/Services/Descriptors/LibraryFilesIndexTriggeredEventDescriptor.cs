using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryFilesIndexTriggeredEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryFilesIndexTriggeredEvent);
    public string DisplayNameKey => "EventLibraryFilesIndexTriggeredEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library scan started";
    public string DefaultBodyTemplate => "Scan started for library {{Library.Title}}.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.LibraryTitle,
        NotificationParams.LibraryMediaType,
        NotificationParams.LibraryRootPath,
        NotificationParams.LibraryMetadataProvider
    ];
}
