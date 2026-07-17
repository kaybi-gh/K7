using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Services.Descriptors;

public class PeerConnectivityChangedEventDescriptor : INotificationEventDescriptor
{
    public string EventTypeName => nameof(PeerConnectivityChangedEvent);
    public string DisplayName => "Peer Connectivity Changed";
    public NotificationEventCategory Category => NotificationEventCategory.Federation;
    public string DefaultTitleTemplate => "Peer {{Peer.Name}} {{Succeeded}}";
    public string DefaultBodyTemplate => "Peer {{Peer.Name}} ({{Peer.BaseUrl}}) connectivity succeeded={{Succeeded}} (was {{PreviousSucceeded}})";
    public IReadOnlyList<NotificationParameterInfo> Parameters { get; } =
    [
        new("Peer.Name", "Peer Name", "String"),
        new("Peer.BaseUrl", "Peer Base URL", "String"),
        new("Peer.Id", "Peer Id", "Guid"),
        new("Succeeded", "Succeeded", "Bool"),
        new("PreviousSucceeded", "Previous Succeeded", "Bool"),
    ];
}
