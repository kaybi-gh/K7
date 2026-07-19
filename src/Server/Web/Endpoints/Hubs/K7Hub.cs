using System.Globalization;
using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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
        if (TryRegisterCallerDeviceFromQuery(out var deviceId, out _))
        {
            await sender.Send(new UpdateDeviceLastSeenCommand(deviceId));
            await BroadcastConnectedDevices(identityUserId);
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
                await BroadcastConnectedDevices(identityUserId);
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

        var devices = presenceTracker.GetDevicesForUser(identityUserId)
            .Select(kvp => new ConnectedDeviceDto
            {
                DeviceId = kvp.Key,
                DeviceName = kvp.Value.DeviceName,
                DeviceType = kvp.Value.DeviceType
            })
            .ToList();

        await Clients.Caller.ReceiveConnectedDevicesUpdated(devices);
    }

    private async Task BroadcastConnectedDevices(string identityUserId)
    {
        var devices = presenceTracker.GetDevicesForUser(identityUserId)
            .Select(kvp => new ConnectedDeviceDto
            {
                DeviceId = kvp.Key,
                DeviceName = kvp.Value.DeviceName,
                DeviceType = kvp.Value.DeviceType
            })
            .ToList();

        await Clients.Group(identityUserId).ReceiveConnectedDevicesUpdated(devices);
    }

    private Task BroadcastOnlineUsersPresenceToAdminsAsync() =>
        Clients.Group(AdminStreamsGroup).ReceiveOnlineUsersPresenceUpdated(BuildOnlineUsersPresenceDto());

    private OnlineUsersPresenceDto BuildOnlineUsersPresenceDto() =>
        new() { IdentityUserIds = presenceTracker.GetOnlineIdentityUserIds() };

    private bool TryRegisterCallerDeviceFromQuery(out Guid deviceId, out HubDeviceConnection connection)
    {
        deviceId = default;
        connection = null!;

        var httpContext = Context.GetHttpContext();
        if (!Guid.TryParse(httpContext?.Request.Query["deviceId"], out deviceId))
            return false;

        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId))
            return false;

        var deviceName = httpContext!.Request.Query["deviceName"].ToString();
        var deviceType = httpContext.Request.Query["deviceType"].ToString();
        var userDisplayName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? deviceName;
        var syncPlayEnabled = !bool.TryParse(httpContext.Request.Query["syncPlayEnabled"], out var spEnabled) || spEnabled;

        connection = new HubDeviceConnection(
            Context.ConnectionId,
            identityUserId,
            userDisplayName,
            deviceName,
            deviceType,
            syncPlayEnabled);

        presenceTracker.RegisterDevice(deviceId, connection);
        return true;
    }
}
