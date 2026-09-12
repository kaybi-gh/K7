using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryDeletedEvent);
    public string DisplayNameKey => "EventLibraryDeletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library deleted";
    public string DefaultBodyTemplate => "Library {{Library.Title}} was removed.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.LibraryTitle,
        NotificationParams.LibraryMediaType,
        NotificationParams.LibraryRootPath
    ];
}
