using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class MediaAddedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(MediaAddedEvent);
    public string DisplayName => "Media Added";
    public NotificationEventCategory Category => NotificationEventCategory.Media;
    public string DefaultTitleTemplate => "New Media Added";
    public string DefaultBodyTemplate => "{{Media.Title}} ({{Media.Type}})";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Media.Title", "Title", "String"),
        new("Media.OriginalTitle", "Original Title", "String"),
        new("Media.Type", "Media Type", "String"),
        new("Media.ReleaseDate", "Release Date", "String"),
        new("Media.Genres.Count", "Genres Count", "Int"),
        new("PictureUrl", "Picture URL (poster or cover)", "String"),
        new("BackdropUrl", "Backdrop Image URL", "String"),
    ];
}
