using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryScanCompletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryScanCompletedEvent);
    public string DisplayNameKey => "EventLibraryScanCompletedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library scan completed";
    public string DefaultBodyTemplate => "Scan of {{Library.Title}} finished: {{AddedCount}} added, {{SkippedCount}} skipped, {{InaccessibleCount}} inaccessible.";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.LibraryTitle,
        NotificationParams.LibraryId,
        NotificationParams.AddedCount,
        NotificationParams.SkippedCount,
        NotificationParams.InaccessibleCount
    ];
}
