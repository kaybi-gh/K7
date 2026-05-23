using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryFilesIndexTriggeredEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryFilesIndexTriggeredEvent);
    public string DisplayName => "Library Scan Started";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Library.Title", "Library Title", "String"),
        new("Library.MediaType", "Media Type", "String"),
    ];
}
