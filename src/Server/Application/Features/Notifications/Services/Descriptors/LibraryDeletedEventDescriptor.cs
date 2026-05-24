using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryDeletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryDeletedEvent);
    public string DisplayName => "Library Deleted";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library Deleted";
    public string DefaultBodyTemplate => "{{Library.Title}} has been removed";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Library.Title", "Library Name", "String"),
        new("Library.MediaType", "Media Type", "String"),
        new("Library.RootPath", "Root Path", "String"),
        new("Library.Description", "Description", "String"),
    ];
}
