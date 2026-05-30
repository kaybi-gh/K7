using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryCreatedEvent);
    public string DisplayName => "Library Created";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library Created";
    public string DefaultBodyTemplate => "{{Library.Title}} ({{Library.MediaType}})";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Library.Title", "Library Title", "String"),
        new("Library.MediaType", "Media Type", "String"),
        new("Library.RootPath", "Root Path", "String"),
        new("Library.MetadataProviderName", "Metadata Provider", "String"),
        new("Library.MetadataLanguage", "Metadata Language", "String"),
    ];
}
