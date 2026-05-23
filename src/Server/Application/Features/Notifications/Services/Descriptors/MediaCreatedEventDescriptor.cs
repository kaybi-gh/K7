using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaCreatedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaCreatedEvent);
    public string DisplayName => "Media Added";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Media.Title", "Title", "String"),
        new("Media.MediaType", "Media Type", "String"),
        new("Media.ReleaseYear", "Release Year", "Int"),
        new("Media.Library.Title", "Library Name", "String"),
        new("Media.Library.MediaType", "Library Media Type", "String"),
    ];
}
