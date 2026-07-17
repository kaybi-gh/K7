using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class LibraryScanCompletedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(LibraryScanCompletedEvent);
    public string DisplayName => "Library Scan Completed";
    public NotificationEventCategory Category => NotificationEventCategory.Library;
    public string DefaultTitleTemplate => "Library scan completed";
    public string DefaultBodyTemplate => "{{Library.Title}}: +{{AddedCount}} added, {{SkippedCount}} skipped, {{InaccessibleCount}} inaccessible";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Library.Title", "Library Title", "String"),
        new("Library.Id", "Library Id", "Guid"),
        new("AddedCount", "Added Count", "Int"),
        new("SkippedCount", "Skipped Count", "Int"),
        new("InaccessibleCount", "Inaccessible Count", "Int"),
    ];
}
