using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryFilesIndexTriggeredEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryFilesIndexTriggeredEvent);
    public string DisplayName => "Library Scan Started";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library Scan";
    public string DefaultBodyTemplate => "Scan started for {{Library.Title}}";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Library.Title", "Library Title", "String"),
        new("Library.MediaType", "Media Type", "String"),
        new("Library.RootPath", "Root Path", "String"),
        new("Library.MetadataProviderName", "Metadata Provider", "String"),
    ];
}
