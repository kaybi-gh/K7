using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Singleton service that manages a persistent SignalR connection to the K7 hub.
/// Survives page navigation.
/// </summary>
public sealed class K7HubClient(ILogger<K7HubClient> logger) : IAsyncDisposable
{
    private static class HubGroups
    {
        public const string AdminStreams = nameof(AdminStreams);
        public const string AdminFederation = nameof(AdminFederation);
    }

    private HubConnection? _hubConnection;
    private bool _started;
    private string? _accessToken;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _joinedGroups = new(StringComparer.Ordinal);

    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;
    public string? ConnectedUserId { get; private set; }

    public event Action<HubConnectionState>? ConnectionStateChanged;
    public event Action<Guid, double, bool>? ProgressUpdated;
    public event Action<Guid, string?, string>? MediaAdded;
    public event Action<List<MediaBatchItem>>? MediaBatchAdded;
    public event Action<Guid>? MediaMetadataRefreshed;
    public event Action<Guid>? MediaPicturesUpdated;
    public event Action<Guid>? PersonPicturesUpdated;
    public event Action<Guid, Guid>? MediaIndexedFilesUpdated;
    public event Action<Guid, int, int, int>? LibraryScanCompleted;
    public event Action<Guid, int, int, string>? LibraryScanProgress;
    public event Action? BackgroundTaskUpdated;
    public event Action<IReadOnlyList<ActiveStreamDto>>? ActiveStreamsUpdated;
    public event Action<ServerMetricsSnapshotDto>? ServerMetricsUpdated;
    public event Action<OnlineUsersPresenceDto>? OnlineUsersPresenceUpdated;
    public event Action<RemotePlaybackRequestDto>? RemotePlaybackRequested;
    public event Action<RemoteTransportCommandDto>? RemoteTransportCommandReceived;
    public event Action<IReadOnlyList<ConnectedDeviceDto>>? ConnectedDevicesUpdated;
    public event Action<RemotePlaybackStateDto>? RemotePlaybackStateReceived;
    public event Action<SyncPlayGroupDto>? SyncPlayGroupUpdated;
    public event Action<SyncPlayCommandDto>? SyncPlayCommandReceived;
    public event Action<long, double>? SyncPlayPlayAtReceived;
    public event Action<double>? SyncPlaySeekCorrectionReceived;
    public event Action<SyncPlayChatMessageDto>? SyncPlayChatMessageReceived;
    public event Action<SyncPlayReactionDto>? SyncPlayReactionReceived;
    public event Action<string>? SyncPlayErrorReceived;
    public event Action<SyncPlayInvitationDto>? SyncPlayInvitationReceived;
    public event Action<IReadOnlyList<SyncPlayOnlineUserDto>>? SyncPlayOnlineUsersReceived;
    public event Action<SyncPlayInviteLinkDto>? SyncPlayInviteLinkReceived;
    public event Action<Guid, int>? PeerStateChanged;
    public event Action<PeerRequestDto>? PeerRequestReceived;
    public event Action<Guid, bool>? PeerTestResultReceived;

    public async Task EnsureStartedAsync(Uri baseUri, string userId, string? deviceId = null, string? accessToken = null, string? deviceName = null, string? deviceType = null)
    {
        if (string.IsNullOrEmpty(userId)) return;

        ConnectedUserId = userId;

        if (_started && _hubConnection?.State == HubConnectionState.Connected)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_started && _hubConnection?.State == HubConnectionState.Connected)
                return;

            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(deviceId))
                queryParams.Add($"deviceId={Uri.EscapeDataString(deviceId)}");
            if (!string.IsNullOrEmpty(deviceName))
                queryParams.Add($"deviceName={Uri.EscapeDataString(deviceName)}");
            if (!string.IsNullOrEmpty(deviceType))
                queryParams.Add($"deviceType={Uri.EscapeDataString(deviceType)}");

            var hubPath = queryParams.Count > 0
                ? $"/hub?{string.Join('&', queryParams)}"
                : "/hub";
            var hubUrl = new Uri(baseUri, hubPath);

            _accessToken = accessToken;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_accessToken);
                })
                .WithAutomaticReconnect(new InfiniteRetryPolicy())
                .Build();

            _hubConnection.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                await RejoinGroupsAsync();
            };

            _hubConnection.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
                return Task.CompletedTask;
            };

            _hubConnection.On<Guid, double, bool>("ReceivePlaybackProgress", (mediaId, progress, isCompleted) =>
            {
                ProgressUpdated?.Invoke(mediaId, progress, isCompleted);
            });

            _hubConnection.On<Guid, string?, string>("ReceiveMediaAdded", (mediaId, title, mediaType) =>
            {
                MediaAdded?.Invoke(mediaId, title, mediaType);
            });

            _hubConnection.On<List<MediaBatchItem>>("ReceiveMediaBatchAdded", (items) =>
            {
                MediaBatchAdded?.Invoke(items);
            });

            _hubConnection.On<Guid>("ReceiveMediaMetadataRefreshed", mediaId =>
            {
                MediaMetadataRefreshed?.Invoke(mediaId);
            });

            _hubConnection.On<Guid>("ReceiveMediaPicturesUpdated", mediaId =>
            {
                MediaPicturesUpdated?.Invoke(mediaId);
            });

            _hubConnection.On<Guid>("ReceivePersonPicturesUpdated", personId =>
            {
                PersonPicturesUpdated?.Invoke(personId);
            });

            _hubConnection.On<Guid, Guid>("ReceiveMediaIndexedFilesUpdated", (mediaId, libraryId) =>
            {
                MediaIndexedFilesUpdated?.Invoke(mediaId, libraryId);
            });

            _hubConnection.On<Guid, int, int, int>("ReceiveLibraryScanCompleted", (libraryId, addedCount, skippedCount, inaccessiblePathCount) =>
            {
                LibraryScanCompleted?.Invoke(libraryId, addedCount, skippedCount, inaccessiblePathCount);
            });

            _hubConnection.On<Guid, int, int, string>("ReceiveLibraryScanProgress", (libraryId, processed, total, phase) =>
            {
                LibraryScanProgress?.Invoke(libraryId, processed, total, phase);
            });

            _hubConnection.On("ReceiveBackgroundTaskUpdated", () =>
            {
                BackgroundTaskUpdated?.Invoke();
            });

            _hubConnection.On<IReadOnlyList<ActiveStreamDto>>("ReceiveActiveStreamsUpdated", (streams) =>
            {
                ActiveStreamsUpdated?.Invoke(streams);
            });

            _hubConnection.On<ServerMetricsSnapshotDto>("ReceiveServerMetricsUpdated", (snapshot) =>
            {
                ServerMetricsUpdated?.Invoke(snapshot);
            });

            _hubConnection.On<OnlineUsersPresenceDto>("ReceiveOnlineUsersPresenceUpdated", (presence) =>
            {
                OnlineUsersPresenceUpdated?.Invoke(presence);
            });

            _hubConnection.On<RemotePlaybackRequestDto>("ReceiveRemotePlaybackRequest", (request) =>
            {
                RemotePlaybackRequested?.Invoke(request);
            });

            _hubConnection.On<RemoteTransportCommandDto>("ReceiveRemoteTransportCommand", (command) =>
            {
                RemoteTransportCommandReceived?.Invoke(command);
            });

            _hubConnection.On<IReadOnlyList<ConnectedDeviceDto>>("ReceiveConnectedDevicesUpdated", (devices) =>
            {
                ConnectedDevicesUpdated?.Invoke(devices);
            });

            _hubConnection.On<RemotePlaybackStateDto>("ReceiveRemotePlaybackState", (state) =>
            {
                RemotePlaybackStateReceived?.Invoke(state);
            });

            _hubConnection.On<SyncPlayGroupDto>("ReceiveSyncPlayGroupUpdated", (group) =>
            {
                SyncPlayGroupUpdated?.Invoke(group);
            });

            _hubConnection.On<SyncPlayCommandDto>("ReceiveSyncPlayCommand", (command) =>
            {
                SyncPlayCommandReceived?.Invoke(command);
            });

            _hubConnection.On<long, double>("ReceiveSyncPlayPlayAt", (timestampMs, position) =>
            {
                SyncPlayPlayAtReceived?.Invoke(timestampMs, position);
            });

            _hubConnection.On<double>("ReceiveSyncPlaySeekCorrection", (position) =>
            {
                SyncPlaySeekCorrectionReceived?.Invoke(position);
            });

            _hubConnection.On<SyncPlayChatMessageDto>("ReceiveSyncPlayChatMessage", (message) =>
            {
                SyncPlayChatMessageReceived?.Invoke(message);
            });

            _hubConnection.On<SyncPlayReactionDto>("ReceiveSyncPlayReaction", (reaction) =>
            {
                SyncPlayReactionReceived?.Invoke(reaction);
            });

            _hubConnection.On<string>("ReceiveSyncPlayError", (errorCode) =>
            {
                SyncPlayErrorReceived?.Invoke(errorCode);
            });

            _hubConnection.On<SyncPlayInvitationDto>("ReceiveSyncPlayInvitation", (invitation) =>
            {
                SyncPlayInvitationReceived?.Invoke(invitation);
            });

            _hubConnection.On<IReadOnlyList<SyncPlayOnlineUserDto>>("ReceiveSyncPlayOnlineUsers", (users) =>
            {
                SyncPlayOnlineUsersReceived?.Invoke(users);
            });

            _hubConnection.On<SyncPlayInviteLinkDto>("ReceiveSyncPlayInviteLink", (link) =>
            {
                SyncPlayInviteLinkReceived?.Invoke(link);
            });

            _hubConnection.On<Guid, int>("ReceivePeerStateChanged", (peerId, newStatus) =>
            {
                PeerStateChanged?.Invoke(peerId, newStatus);
            });

            _hubConnection.On<PeerRequestDto>("ReceivePeerRequestReceived", (request) =>
            {
                PeerRequestReceived?.Invoke(request);
            });

            _hubConnection.On<Guid, bool>("ReceivePeerTestResult", (peerId, reachable) =>
            {
                PeerTestResultReceived?.Invoke(peerId, reachable);
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _hubConnection.StartAsync(cts.Token);
            _started = true;
            await RejoinGroupsAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task JoinAdminStreamsGroupAsync()
    {
        _joinedGroups.Add(HubGroups.AdminStreams);
        await SendIfConnectedAsync("JoinAdminStreamsGroup");
    }

    public async Task LeaveAdminStreamsGroupAsync()
    {
        _joinedGroups.Remove(HubGroups.AdminStreams);
        await SendIfConnectedAsync("LeaveAdminStreamsGroup");
    }

    public async Task JoinAdminFederationGroupAsync()
    {
        _joinedGroups.Add(HubGroups.AdminFederation);
        await SendIfConnectedAsync("JoinAdminFederationGroup");
    }

    public async Task LeaveAdminFederationGroupAsync()
    {
        _joinedGroups.Remove(HubGroups.AdminFederation);
        await SendIfConnectedAsync("LeaveAdminFederationGroup");
    }

    public Task RequestRemotePlaybackAsync(Guid targetDeviceId, RemotePlaybackRequestDto request) =>
        SendIfConnectedAsync("RequestRemotePlayback", targetDeviceId, request);

    public Task SendRemoteTransportCommandAsync(Guid targetDeviceId, RemoteTransportCommandDto command) =>
        SendIfConnectedAsync("SendRemoteTransportCommand", targetDeviceId, command);

    public Task RequestConnectedDevicesAsync() =>
        SendIfConnectedAsync("GetConnectedDevices");

    public Task ReportRemotePlaybackStateAsync(Guid controllerDeviceId, RemotePlaybackStateDto state) =>
        SendIfConnectedAsync("ReportRemotePlaybackState", controllerDeviceId, state);

    // --- SyncPlay ---

    public Task<bool> CreateSyncPlayGroupAsync(SyncPlayCreateGroupDto request) =>
        TrySendIfConnectedAsync("CreateSyncPlayGroup", request);

    public Task JoinSyncPlayGroupAsync(Guid groupId, string? guestToken = null, string? guestDisplayName = null) =>
        SendIfConnectedAsync("JoinSyncPlayGroup", groupId, guestToken, guestDisplayName);

    public Task LeaveSyncPlayGroupAsync(Guid groupId) =>
        SendIfConnectedAsync("LeaveSyncPlayGroup", groupId);

    public Task SyncPlayCommandAsync(Guid groupId, SyncPlayCommandType commandType, double? value = null) =>
        SendIfConnectedAsync("SyncPlayCommand", groupId, commandType, value);

    public Task SyncPlayReportReadyAsync(Guid groupId) =>
        SendIfConnectedAsync("SyncPlayReportReady", groupId);

    public Task SyncPlayReportPositionAsync(Guid groupId, double position) =>
        SendIfConnectedAsync("SyncPlayReportPosition", groupId, position);

    public Task SyncPlayAddToQueueAsync(Guid groupId, SyncPlayQueueItemDto item) =>
        SendIfConnectedAsync("SyncPlayAddToQueue", groupId, item);

    public Task SyncPlayBulkAddToQueueAsync(Guid groupId, IReadOnlyList<SyncPlayQueueItemDto> items) =>
        SendIfConnectedAsync("SyncPlayBulkAddToQueue", groupId, items);

    public Task SyncPlaySetCurrentMediaAsync(Guid groupId, SyncPlayQueueItemDto item) =>
        SendIfConnectedAsync("SyncPlaySetCurrentMedia", groupId, item);

    public Task SyncPlayRemoveFromQueueAsync(Guid groupId, Guid queueItemId) =>
        SendIfConnectedAsync("SyncPlayRemoveFromQueue", groupId, queueItemId);

    public Task SyncPlayKickAsync(Guid groupId, Guid targetDeviceId) =>
        SendIfConnectedAsync("SyncPlayKick", groupId, targetDeviceId);

    public Task SyncPlaySendChatAsync(Guid groupId, string text) =>
        SendIfConnectedAsync("SyncPlaySendChat", groupId, text);

    public Task SyncPlaySendReactionAsync(Guid groupId, string emoji) =>
        SendIfConnectedAsync("SyncPlaySendReaction", groupId, emoji);

    public Task SyncPlayGenerateGuestTokenAsync(Guid groupId) =>
        SendIfConnectedAsync("SyncPlayGenerateGuestToken", groupId);

    public Task SyncPlayInviteUserAsync(string targetUserId, Guid groupId) =>
        SendIfConnectedAsync("SyncPlayInviteUser", targetUserId, groupId);

    public Task SyncPlayGetOnlineUsersAsync() =>
        SendIfConnectedAsync("SyncPlayGetOnlineUsers");

    public Task SyncPlaySetEnabledAsync(bool enabled) =>
        SendIfConnectedAsync("SyncPlaySetEnabled", enabled);

    public Task SyncPlayGetInviteLinkAsync(Guid groupId) =>
        SendIfConnectedAsync("SyncPlayGetInviteLink", groupId);

    public Task SyncPlayJoinViaInviteTokenAsync(string token, string? guestDisplayName = null) =>
        SendIfConnectedAsync("SyncPlayJoinViaInviteToken", token, guestDisplayName);

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _lock.Dispose();
    }

    // Blazor WASM runs SignalR on a single thread: InvokeAsync deadlocks when the hub
    // awaits Clients.Caller before returning. Use SendAsync here; the server awaits
    // outbound callbacks so messages are delivered before the hub method completes.
    private Task SendIfConnectedAsync(string methodName, params object?[] args) =>
        SendCoreIfConnectedAsync(methodName, args);

    private async Task<bool> TrySendIfConnectedAsync(string methodName, params object?[] args)
    {
        var connection = _hubConnection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            logger.LogDebug("Hub send dropped: {Method} (state={State})", methodName, State);
            return false;
        }

        try
        {
            await connection.SendCoreAsync(methodName, args, CancellationToken.None);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // Connection can drop between the state check above and this call (HubConnectionState
            // is a snapshot); this is an expected race, not an actionable error, so log quietly.
            logger.LogDebug(ex, "Hub send failed: {Method} (state={State})", methodName, State);
            return false;
        }
    }

    private async Task SendCoreIfConnectedAsync(string methodName, object?[] args)
    {
        var connection = _hubConnection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            logger.LogDebug("Hub send dropped: {Method} (state={State})", methodName, State);
            return;
        }

        try
        {
            await connection.SendCoreAsync(methodName, args, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // Connection can drop between the state check above and this call (HubConnectionState
            // is a snapshot); this is an expected race, not an actionable error, so log quietly.
            logger.LogDebug(ex, "Hub send failed: {Method} (state={State})", methodName, State);
        }
    }

    public void UpdateAccessToken(string? accessToken) => _accessToken = accessToken;

    private async Task RejoinGroupsAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
            return;

        try
        {
            if (_joinedGroups.Contains(HubGroups.AdminStreams))
                await _hubConnection.SendAsync("JoinAdminStreamsGroup");

            if (_joinedGroups.Contains(HubGroups.AdminFederation))
                await _hubConnection.SendAsync("JoinAdminFederationGroup");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            logger.LogWarning(ex, "Hub rejoin failed (state={State})", State);
        }
    }

    private sealed class InfiniteRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min((retryContext.PreviousRetryCount + 1) * 2, 10));
    }
}
