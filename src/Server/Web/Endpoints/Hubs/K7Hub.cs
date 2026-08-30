using System.Globalization;
using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Endpoints.Hubs;

/// <summary>
/// Central SignalR hub for all real-time communication between the server and connected clients.
/// Each connection is associated with a user identity and added to a user-scoped group.
/// The identity is resolved from authenticated claims (cookie or bearer token).
/// </summary>
[Authorize(Policy = Policies.GuestOrAbove)]
public partial class K7Hub(
    ISender sender,
    ILogger<K7Hub> logger,
    ISyncPlayCoordinator syncPlay,
    IUserSettingsService userSettingsService,
    IHubPresenceTracker presenceTracker,
    IServiceScopeFactory scopeFactory) : Hub<IK7HubClient>
{
    public override async Task OnConnectedAsync()
    {
        var identityUserId = ResolveIdentityUserId();

        logger.LogDebug("Hub connection established: identityUserId='{IdentityUserId}', connectionId='{ConnectionId}'", identityUserId, Context.ConnectionId);

        if (string.IsNullOrEmpty(identityUserId))
        {
            logger.LogWarning("No identity on hub connection, aborting. ConnectionId='{ConnectionId}'", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, identityUserId);

        // Update device LastSeen timestamp and track connection
        var registeredDeviceId = await TryRegisterCallerDeviceFromQueryAsync();
        if (registeredDeviceId is Guid deviceId)
        {
            await sender.Send(new UpdateDeviceLastSeenCommand(deviceId));
            await BroadcastConnectedDevicesAsync(identityUserId);
            await BroadcastOnlineUsersPresenceToAdminsAsync();
        }

        var httpContext = Context.GetHttpContext();
        if (Guid.TryParse(httpContext?.Request.Query["indexedFileId"], out Guid indexedFileId))
        {
            double position = 0;
            if (double.TryParse(httpContext!.Request.Query["position"], NumberStyles.Float, CultureInfo.InvariantCulture, out double providedPosition))
            {
                position = providedPosition;
            }

            var session = new StreamingSessionDto
            {
                Id = Guid.NewGuid(),
                IndexedFileId = indexedFileId,
                State = PlaybackState.Idle,
                Position = position,
                PlaybackSettings = new()
            };

            await Groups.AddToGroupAsync(Context.ConnectionId, session.Id.ToString());
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var identityUserId = ResolveIdentityUserId();

        if (!string.IsNullOrEmpty(identityUserId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, identityUserId);
        }

        // Remove device from connection tracker
        if (presenceTracker.TryRemoveByConnectionId(Context.ConnectionId, out var deviceId, out var disconnectedConnection)
            && disconnectedConnection is not null)
        {
            if (!string.IsNullOrEmpty(identityUserId))
            {
                await BroadcastConnectedDevicesAsync(identityUserId);
            }

            var result = syncPlay.DisconnectDevice(deviceId);
            if (result.GroupId != Guid.Empty && !result.GroupDestroyed)
            {
                var group = syncPlay.GetGroup(result.GroupId);
                if (group is not null)
                {
                    await Clients.Group(SyncPlayGroupName(result.GroupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
                }
            }

            await BroadcastOnlineUsersPresenceToAdminsAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- Client-to-server methods (streaming session) ---

    public async Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings)
    {
        await Clients.Caller.ChangePlaybackSettings(streamId, playbackSettings);
    }

    public async Task SendPlaybackState(Guid streamId, PlaybackState state, double position)
    {
        await Clients.Caller.SendPlaybackState(streamId, state, position);
    }

    public async Task SendIndexedFileStreamUri(Guid streamId, Guid indexedFileId, Guid deviceId, PlaybackSettingsDto playbackSettings)
    {
        var uri = await sender.Send(new GetStreamUriQuery { Id = indexedFileId, DeviceId = deviceId });
        await Clients.Caller.ReceiveIndexedFileStreamUri(uri);
    }

    // --- Admin stream monitoring ---

    public const string AdminStreamsGroup = "admin-streams";

    public async Task JoinAdminStreamsGroup()
    {
        if (!Context.User?.IsInRole("Administrator") ?? true)
        {
            logger.LogWarning("Non-admin user attempted to join admin-streams group. ConnectionId='{ConnectionId}'", Context.ConnectionId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminStreamsGroup);
        await Clients.Caller.ReceiveOnlineUsersPresenceUpdated(BuildOnlineUsersPresenceDto());

        using (var scope = scopeFactory.CreateScope())
        {
            var snapshotService = scope.ServiceProvider.GetRequiredService<IActiveStreamsSnapshotService>();
            var streams = await snapshotService.BuildAsync(Context.ConnectionAborted);
            await Clients.Caller.ReceiveActiveStreamsUpdated(streams);
        }
    }

    public async Task LeaveAdminStreamsGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminStreamsGroup);
    }

    // --- Admin federation monitoring ---

    public const string AdminFederationGroup = "admin-federation";

    public async Task JoinAdminFederationGroup()
    {
        if (!Context.User?.IsInRole("Administrator") ?? true)
        {
            logger.LogWarning("Non-admin user attempted to join admin-federation group. ConnectionId='{ConnectionId}'", Context.ConnectionId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminFederationGroup);
    }

    public async Task LeaveAdminFederationGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminFederationGroup);
    }

    private string? ResolveIdentityUserId()
    {
        // OpenIddict access tokens expose the user id as "sub"; cookies/API keys use NameIdentifier.
        var identityUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return string.IsNullOrEmpty(identityUserId) ? null : identityUserId;
    }

    // --- Remote playback (companion mode) ---

    public async Task RequestRemotePlayback(Guid targetDeviceId, RemotePlaybackRequestDto request)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        if (!presenceTracker.TryGetDevice(targetDeviceId, out var target))
        {
            logger.LogWarning("Remote playback requested for offline device {DeviceId}", targetDeviceId);
            return;
        }

        if (target.IdentityUserId != identityUserId)
        {
            logger.LogWarning("User {UserId} attempted remote playback on device owned by another user", identityUserId);
            return;
        }

        await Clients.Client(target.ConnectionId).ReceiveRemotePlaybackRequest(request);
    }

    public async Task SendRemoteTransportCommand(Guid targetDeviceId, RemoteTransportCommandDto command)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        if (!presenceTracker.TryGetDevice(targetDeviceId, out var target))
        {
            return;
        }

        if (target.IdentityUserId != identityUserId)
        {
            return;
        }

        await Clients.Client(target.ConnectionId).ReceiveRemoteTransportCommand(command);
    }

    public async Task ReportRemotePlaybackState(Guid controllerDeviceId, RemotePlaybackStateDto state)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        if (!presenceTracker.TryGetDevice(controllerDeviceId, out var controller))
        {
            return;
        }

        if (controller.IdentityUserId != identityUserId)
        {
            return;
        }

        await Clients.Client(controller.ConnectionId).ReceiveRemotePlaybackState(state);
    }

    public async Task GetConnectedDevices()
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId))
            return;

        var devices = await BuildConnectedDevicesAsync(identityUserId);
        await Clients.Caller.ReceiveConnectedDevicesUpdated(devices);
    }

    private async Task BroadcastConnectedDevicesAsync(string identityUserId)
    {
        var devices = await BuildConnectedDevicesAsync(identityUserId);
        await Clients.Group(identityUserId).ReceiveConnectedDevicesUpdated(devices);
    }

    private async Task<IReadOnlyList<ConnectedDeviceDto>> BuildConnectedDevicesAsync(string identityUserId)
    {
        var connected = presenceTracker.GetDevicesForUser(identityUserId).ToList();
        if (connected.Count == 0)
            return [];

        var labels = await ResolveDeviceLabelsAsync(connected.Select(kvp => kvp.Key).ToList());

        return connected
            .Select(kvp =>
            {
                labels.TryGetValue(kvp.Key, out var label);
                var deviceName = FirstNonEmpty(
                    label.Name,
                    kvp.Value.DeviceName,
                    label.Type,
                    kvp.Value.DeviceType,
                    "Device");
                var deviceType = FirstNonEmpty(kvp.Value.DeviceType, label.Type, "Unknown");
                return new ConnectedDeviceDto
                {
                    DeviceId = kvp.Key,
                    DeviceName = deviceName,
                    DeviceType = deviceType
                };
            })
            .ToList();
    }

    private Task BroadcastOnlineUsersPresenceToAdminsAsync() =>
        Clients.Group(AdminStreamsGroup).ReceiveOnlineUsersPresenceUpdated(BuildOnlineUsersPresenceDto());

    private OnlineUsersPresenceDto BuildOnlineUsersPresenceDto() =>
        new() { IdentityUserIds = presenceTracker.GetOnlineIdentityUserIds() };

    private async Task<Guid?> TryRegisterCallerDeviceFromQueryAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (!Guid.TryParse(httpContext?.Request.Query["deviceId"], out var deviceId))
            return null;

        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId))
            return null;

        var queryName = httpContext!.Request.Query["deviceName"].ToString();
        var queryType = httpContext.Request.Query["deviceType"].ToString();
        var (deviceName, deviceType) = await ResolveDevicePresenceAsync(deviceId, queryName, queryType);
        var userDisplayName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? deviceName;
        var syncPlayEnabled = !bool.TryParse(httpContext.Request.Query["syncPlayEnabled"], out var spEnabled) || spEnabled;

        var connection = new HubDeviceConnection(
            Context.ConnectionId,
            identityUserId,
            userDisplayName,
            deviceName,
            deviceType,
            syncPlayEnabled);

        presenceTracker.RegisterDevice(deviceId, connection);
        return deviceId;
    }

    private async Task<(string Name, string Type)> ResolveDevicePresenceAsync(
        Guid deviceId,
        string queryName,
        string queryType)
    {
        var labels = await ResolveDeviceLabelsAsync([deviceId]);
        labels.TryGetValue(deviceId, out var label);

        // Prefer the persisted admin/devices name over the hub query (often empty / DeviceType only).
        var deviceName = FirstNonEmpty(label.Name, queryName, label.Type, queryType, "Device");
        var deviceType = FirstNonEmpty(queryType, label.Type, "Unknown");
        return (deviceName, deviceType);
    }

    private async Task<Dictionary<Guid, (string? Name, string? Type)>> ResolveDeviceLabelsAsync(
        IReadOnlyList<Guid> deviceIds)
    {
        var result = new Dictionary<Guid, (string? Name, string? Type)>();
        if (deviceIds.Count == 0)
            return result;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rows = await db.Devices
            .AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .Select(d => new { d.Id, d.DeviceName, d.DeviceType })
            .ToListAsync();

        foreach (var row in rows)
        {
            result[row.Id] = (
                string.IsNullOrWhiteSpace(row.DeviceName) ? null : row.DeviceName.Trim(),
                row.DeviceType.ToString());
        }

        return result;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
