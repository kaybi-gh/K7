using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Singleton service that manages a persistent SignalR connection to the K7 hub.
/// Survives page navigation.
/// </summary>
public sealed class K7HubClient : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private bool _started;
    private readonly SemaphoreSlim _lock = new(1, 1);

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

            var query = $"/hub?userId={Uri.EscapeDataString(userId)}";
            if (!string.IsNullOrEmpty(deviceId))
            {
                query += $"&deviceId={Uri.EscapeDataString(deviceId)}";
            }
            if (!string.IsNullOrEmpty(deviceName))
            {
                query += $"&deviceName={Uri.EscapeDataString(deviceName)}";
            }
            if (!string.IsNullOrEmpty(deviceType))
            {
                query += $"&deviceType={Uri.EscapeDataString(deviceType)}";
            }

            var hubUrl = new Uri(baseUri, query);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        options.Headers["Authorization"] = $"Bearer {accessToken}";
                    }
                })
                .WithAutomaticReconnect(new InfiniteRetryPolicy())
                .Build();

            _hubConnection.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                return Task.CompletedTask;
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
            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task JoinAdminStreamsGroupAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinAdminStreamsGroup");
    }

    public async Task LeaveAdminStreamsGroupAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("LeaveAdminStreamsGroup");
    }

    public async Task JoinAdminFederationGroupAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinAdminFederationGroup");
    }

    public async Task LeaveAdminFederationGroupAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("LeaveAdminFederationGroup");
    }

    public async Task RequestRemotePlaybackAsync(Guid targetDeviceId, RemotePlaybackRequestDto request)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("RequestRemotePlayback", targetDeviceId, request);
    }

    public async Task SendRemoteTransportCommandAsync(Guid targetDeviceId, RemoteTransportCommandDto command)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SendRemoteTransportCommand", targetDeviceId, command);
    }

    public async Task RequestConnectedDevicesAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("GetConnectedDevices");
    }

    public async Task ReportRemotePlaybackStateAsync(Guid controllerDeviceId, RemotePlaybackStateDto state)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("ReportRemotePlaybackState", controllerDeviceId, state);
    }

    // --- SyncPlay ---

    public async Task CreateSyncPlayGroupAsync(SyncPlayCreateGroupDto request)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("CreateSyncPlayGroup", request);
    }

    public async Task JoinSyncPlayGroupAsync(Guid groupId, string? guestToken = null, string? guestDisplayName = null)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinSyncPlayGroup", groupId, guestToken, guestDisplayName);
    }

    public async Task LeaveSyncPlayGroupAsync(Guid groupId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("LeaveSyncPlayGroup", groupId);
    }

    public async Task SyncPlayCommandAsync(Guid groupId, SyncPlayCommandType commandType, double? value = null)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayCommand", groupId, commandType, value);
    }

    public async Task SyncPlayReportReadyAsync(Guid groupId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayReportReady", groupId);
    }

    public async Task SyncPlayReportPositionAsync(Guid groupId, double position)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayReportPosition", groupId, position);
    }

    public async Task SyncPlayAddToQueueAsync(Guid groupId, SyncPlayQueueItemDto item)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayAddToQueue", groupId, item);
    }

    public async Task SyncPlayBulkAddToQueueAsync(Guid groupId, IReadOnlyList<SyncPlayQueueItemDto> items)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayBulkAddToQueue", groupId, items);
    }

    public async Task SyncPlaySetCurrentMediaAsync(Guid groupId, SyncPlayQueueItemDto item)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlaySetCurrentMedia", groupId, item);
    }

    public async Task SyncPlayRemoveFromQueueAsync(Guid groupId, Guid queueItemId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayRemoveFromQueue", groupId, queueItemId);
    }

    public async Task SyncPlayKickAsync(Guid groupId, Guid targetDeviceId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayKick", groupId, targetDeviceId);
    }

    public async Task SyncPlaySendChatAsync(Guid groupId, string text)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlaySendChat", groupId, text);
    }

    public async Task SyncPlaySendReactionAsync(Guid groupId, string emoji)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlaySendReaction", groupId, emoji);
    }

    public async Task SyncPlayGenerateGuestTokenAsync(Guid groupId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayGenerateGuestToken", groupId);
    }

    public async Task SyncPlayInviteUserAsync(string targetUserId, Guid groupId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayInviteUser", targetUserId, groupId);
    }

    public async Task SyncPlayGetOnlineUsersAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayGetOnlineUsers");
    }

    public async Task SyncPlaySetEnabledAsync(bool enabled)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlaySetEnabled", enabled);
    }

    public async Task SyncPlayGetInviteLinkAsync(Guid groupId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayGetInviteLink", groupId);
    }

    public async Task SyncPlayJoinViaInviteTokenAsync(string token, string? guestDisplayName = null)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SyncPlayJoinViaInviteToken", token, guestDisplayName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _lock.Dispose();
    }

    private sealed class InfiniteRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount * 2, 10));
    }
}
