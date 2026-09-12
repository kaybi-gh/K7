using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PeerConnectivityChangedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PeerConnectivityChangedEvent);
    public string DisplayNameKey => "EventPeerConnectivityChangedEvent";
    public NotificationEventCategory Category => NotificationEventCategory.Federation;
    public string DefaultTitleTemplate => "Peer connectivity";
    public string DefaultBodyTemplate => "Peer {{Peer.Name}} is now {{Succeeded}} (was {{PreviousSucceeded}}).";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        NotificationParams.PeerName,
        NotificationParams.PeerBaseUrl,
        NotificationParams.PeerId,
        NotificationParams.PeerSucceeded,
        NotificationParams.PeerPreviousSucceeded
    ];
}
