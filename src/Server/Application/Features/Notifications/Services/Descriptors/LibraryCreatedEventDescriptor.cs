using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryCreatedEvent);
    public string DisplayNameKey => "EventLibraryCreatedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library created";
    public string DefaultBodyTemplate => "Library {{Library.Title}} ({{Library.MediaType}}) was created.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.LibraryTitle,
        NotificationParams.LibraryMediaType,
        NotificationParams.LibraryRootPath,
        NotificationParams.LibraryMetadataProvider,
        NotificationParams.LibraryMetadataLanguage
    ];
}
