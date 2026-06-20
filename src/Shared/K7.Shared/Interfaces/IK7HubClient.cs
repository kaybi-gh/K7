namespace K7.Shared.Interfaces;

/// <summary>
/// Strongly-typed client interface for the K7 SignalR hub.
/// All server-to-client messages are defined here.
/// </summary>
public interface IK7HubClient : IPlaybackProgressClient, IMediaStreamSession, ILibraryNotificationClient, IBackgroundTaskNotificationClient, IAdminStreamNotificationClient, IAdminMetricsNotificationClient, IAdminPresenceNotificationClient, IRemotePlaybackClient, ISyncPlayClient, IFederationNotificationClient;
