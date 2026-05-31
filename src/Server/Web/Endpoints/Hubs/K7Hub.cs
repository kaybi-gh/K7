using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Endpoints.Hubs;

/// <summary>
/// Central SignalR hub for all real-time communication between the server and connected clients.
/// Each connection is associated with a user identity and added to a user-scoped group.
/// The identity is resolved from the auth cookie when available, with a fallback to a query string
/// parameter for environments where cookies are not transmitted (e.g. Blazor WASM WebSocket connections).
/// </summary>
public class K7Hub(ISender sender, ILogger<K7Hub> logger, ISyncPlayCoordinator syncPlay, IUserSettingsService userSettingsService) : Hub<IK7HubClient>
{
    private static readonly ConcurrentDictionary<Guid, DeviceConnection> _connectedDevices = new();

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
        var httpContext = Context.GetHttpContext();
        if (Guid.TryParse(httpContext?.Request.Query["deviceId"], out var deviceId))
        {
            await sender.Send(new UpdateDeviceLastSeenCommand(deviceId));

            var deviceName = httpContext!.Request.Query["deviceName"].ToString();
            var deviceType = httpContext.Request.Query["deviceType"].ToString();
            var userDisplayName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? deviceName;

            var syncPlayEnabled = !bool.TryParse(httpContext.Request.Query["syncPlayEnabled"], out var spEnabled) || spEnabled;

            _connectedDevices[deviceId] = new DeviceConnection(
                Context.ConnectionId,
                identityUserId,
                userDisplayName,
                deviceName,
                deviceType,
                syncPlayEnabled);

            await BroadcastConnectedDevices(identityUserId);
        }

        // If a streaming session was requested, set up the session group as well
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
        var disconnectedDevice = _connectedDevices.FirstOrDefault(kvp => kvp.Value.ConnectionId == Context.ConnectionId);
        if (disconnectedDevice.Value is not null)
        {
            _connectedDevices.TryRemove(disconnectedDevice.Key, out _);

            if (!string.IsNullOrEmpty(identityUserId))
            {
                await BroadcastConnectedDevices(identityUserId);
            }

            // Handle SyncPlay group cleanup
            var result = syncPlay.DisconnectDevice(disconnectedDevice.Key);
            if (result.GroupId != Guid.Empty && !result.GroupDestroyed)
            {
                var group = syncPlay.GetGroup(result.GroupId);
                if (group is not null)
                {
                    await Clients.Group(SyncPlayGroupName(result.GroupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- Client-to-server methods (streaming session) ---

    public Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings)
    {
        return Clients.Caller.ChangePlaybackSettings(streamId, playbackSettings);
    }

    public Task SendPlaybackState(Guid streamId, PlaybackState state, double position)
    {
        return Clients.Caller.SendPlaybackState(streamId, state, position);
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

    /// <summary>
    /// Resolves the identity user ID from cookie auth or query string fallback.
    /// </summary>
    private string? ResolveIdentityUserId()
    {
        var identityUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(identityUserId))
        {
            var httpContext = Context.GetHttpContext();
            identityUserId = httpContext?.Request.Query["userId"].ToString();
        }

        return string.IsNullOrEmpty(identityUserId) ? null : identityUserId;
    }

    // --- Remote playback (companion mode) ---

    public async Task RequestRemotePlayback(Guid targetDeviceId, RemotePlaybackRequestDto request)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        if (!_connectedDevices.TryGetValue(targetDeviceId, out var target))
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

        if (!_connectedDevices.TryGetValue(targetDeviceId, out var target))
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

        if (!_connectedDevices.TryGetValue(controllerDeviceId, out var controller))
        {
            return;
        }

        if (controller.IdentityUserId != identityUserId)
        {
            return;
        }

        await Clients.Client(controller.ConnectionId).ReceiveRemotePlaybackState(state);
    }

    public Task GetConnectedDevices()
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return Task.CompletedTask;

        var devices = _connectedDevices
            .Where(kvp => kvp.Value.IdentityUserId == identityUserId)
            .Select(kvp => new ConnectedDeviceDto
            {
                DeviceId = kvp.Key,
                DeviceName = kvp.Value.DeviceName,
                DeviceType = kvp.Value.DeviceType
            })
            .ToList();

        return Clients.Caller.ReceiveConnectedDevicesUpdated(devices);
    }

    private async Task BroadcastConnectedDevices(string identityUserId)
    {
        var devices = _connectedDevices
            .Where(kvp => kvp.Value.IdentityUserId == identityUserId)
            .Select(kvp => new ConnectedDeviceDto
            {
                DeviceId = kvp.Key,
                DeviceName = kvp.Value.DeviceName,
                DeviceType = kvp.Value.DeviceType
            })
            .ToList();

        await Clients.Group(identityUserId).ReceiveConnectedDevicesUpdated(devices);
    }

    // --- SyncPlay (watch party) ---

    public async Task CreateSyncPlayGroup(SyncPlayCreateGroupDto request)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var device = ResolveCallerDevice();
        if (device is null) return;

        SyncPlayQueueItemDto? initialMedia = request.InitialMediaReferenceId is not null
            ? new SyncPlayQueueItemDto
            {
                QueueItemId = Guid.NewGuid(),
                MediaReferenceId = request.InitialMediaReferenceId.Value,
                Title = request.InitialMediaTitle ?? "",
                Duration = request.InitialMediaDuration,
                CoverUrl = request.InitialMediaCoverUrl,
                AddedByDisplayName = device.Value.UserDisplayName
            }
            : null;

        var group = syncPlay.CreateGroup(identityUserId, device.Value.DeviceId, device.Value.UserDisplayName, device.Value.DeviceType, initialMedia, request.InitialPosition, request.IsPlaying);

        await Groups.AddToGroupAsync(Context.ConnectionId, SyncPlayGroupName(group.GroupId));
        await Clients.Caller.ReceiveSyncPlayGroupUpdated(ToGroupDto(group, identityUserId));
    }

    public async Task JoinSyncPlayGroup(Guid groupId, string? guestToken, string? guestDisplayName)
    {
        var identityUserId = ResolveIdentityUserId();
        var isGuest = string.IsNullOrEmpty(identityUserId);

        if (isGuest)
        {
            if (string.IsNullOrEmpty(guestToken) || !syncPlay.ValidateGuestToken(groupId, guestToken))
            {
                await Clients.Caller.ReceiveSyncPlayError("invalid_guest_token");
                return;
            }
        }

        var device = ResolveCallerDevice();
        if (device is null) return;

        var displayName = !string.IsNullOrEmpty(guestDisplayName)
            ? guestDisplayName
            : isGuest ? "Guest" : device.Value.UserDisplayName;

        var group = syncPlay.JoinGroup(groupId, identityUserId, device.Value.DeviceId, displayName, device.Value.DeviceType, isGuest);
        if (group is null)
        {
            await Clients.Caller.ReceiveSyncPlayError("group_not_found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SyncPlayGroupName(groupId));
        await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, identityUserId));

        var joinMessage = new SyncPlayChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            DisplayName = "",
            Text = $"{displayName} joined the session",
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayChatMessage(joinMessage);
    }

    public async Task LeaveSyncPlayGroup(Guid groupId)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);

        var result = syncPlay.LeaveGroup(groupId, device.Value.DeviceId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SyncPlayGroupName(groupId));

        if (!result.GroupDestroyed)
        {
            var group = syncPlay.GetGroup(groupId);
            if (group is not null)
            {
                await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));

                var leaveMessage = new SyncPlayChatMessageDto
                {
                    MessageId = Guid.NewGuid(),
                    DisplayName = "",
                    Text = $"{displayName} left the session",
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayChatMessage(leaveMessage);
            }
        }
    }

    public async Task SyncPlayCommand(Guid groupId, SyncPlayCommandType commandType, double? value)
    {
        var identityUserId = ResolveIdentityUserId();
        var device = ResolveCallerDevice();
        if (device is null) return;

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);
        if (commandType is SyncPlayCommandType.NextInQueue or SyncPlayCommandType.PreviousInQueue)
        {
            var nextItem = syncPlay.NavigateQueue(groupId, commandType == SyncPlayCommandType.NextInQueue);
            if (nextItem is null) return;

            var queueHubGroup = SyncPlayGroupName(groupId);
            var chatMsg = new SyncPlayChatMessageDto
            {
                MessageId = Guid.NewGuid(),
                DisplayName = "",
                Text = $"{displayName} changed to {nextItem.Title}",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await Clients.Group(queueHubGroup).ReceiveSyncPlayChatMessage(chatMsg);

            var group = syncPlay.GetGroup(groupId);
            if (group is not null)
            {
                await Clients.Group(queueHubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
            }
            return;
        }

        var result = syncPlay.IssueCommand(groupId, identityUserId, commandType, value, displayName);

        if (!result.Permitted)
        {
            await Clients.Caller.ReceiveSyncPlayError("not_permitted");
            return;
        }

        var command = new SyncPlayCommandDto
        {
            CommandType = commandType,
            Value = value,
            IssuedByDisplayName = displayName
        };

        var hubGroup = SyncPlayGroupName(groupId);

        // Command to others only (issuer already applied locally)
        await Clients.OthersInGroup(hubGroup).ReceiveSyncPlayCommand(command);

        // System chat message for all
        var systemText = commandType switch
        {
            SyncPlayCommandType.Play => $"{displayName} resumed playback",
            SyncPlayCommandType.Pause => $"{displayName} paused playback",
            SyncPlayCommandType.SeekTo => $"{displayName} seeked to {FormatPosition(value ?? 0)}",
            _ => null
        };
        if (systemText is not null)
        {
            var chatMsg = new SyncPlayChatMessageDto
            {
                MessageId = Guid.NewGuid(),
                DisplayName = "",
                Text = systemText,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await Clients.Group(hubGroup).ReceiveSyncPlayChatMessage(chatMsg);
        }

        // GroupUpdated only for state-changing commands (SeekTo -> WaitingForReady)
        if (commandType == SyncPlayCommandType.SeekTo)
        {
            var group = syncPlay.GetGroup(groupId);
            if (group is not null)
            {
                await Clients.Group(hubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
            }
        }
    }

    public async Task SyncPlayReportReady(Guid groupId)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var result = syncPlay.ReportReady(groupId, device.Value.DeviceId);

        if (result.AllReady)
        {
            if (result.CatchUpOnly)
            {
                await Clients.Caller.ReceiveSyncPlayPlayAt(result.PlayAtTimestampMs, result.Position);
            }
            else
            {
                var hubGroup = SyncPlayGroupName(groupId);
                await Clients.Group(hubGroup).ReceiveSyncPlayPlayAt(result.PlayAtTimestampMs, result.Position);

                var group = syncPlay.GetGroup(groupId);
                if (group is not null)
                {
                    await Clients.Group(hubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
                }
            }
        }
    }

    public async Task SyncPlayReportPosition(Guid groupId, double position)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var correction = syncPlay.ReportPosition(groupId, device.Value.DeviceId, position);

        if (correction is not null)
        {
            // Send seek correction only to the drifted client - group continues playing
            await Clients.Caller.ReceiveSyncPlaySeekCorrection(correction.Value.Position);
        }
    }

    public async Task SyncPlayAddToQueue(Guid groupId, SyncPlayQueueItemDto item)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        if (!syncPlay.AddToQueue(groupId, item))
        {
            await Clients.Caller.ReceiveSyncPlayError("group_not_found");
            return;
        }

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);
        var hubGroup = SyncPlayGroupName(groupId);

        var chatMsg = new SyncPlayChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            DisplayName = "",
            Text = $"{displayName} added {item.Title} to queue",
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await Clients.Group(hubGroup).ReceiveSyncPlayChatMessage(chatMsg);

        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Group(hubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
        }
    }

    public async Task SyncPlayBulkAddToQueue(Guid groupId, IReadOnlyList<SyncPlayQueueItemDto> items)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        foreach (var item in items)
        {
            if (!syncPlay.AddToQueue(groupId, item))
            {
                await Clients.Caller.ReceiveSyncPlayError("group_not_found");
                return;
            }
        }

        var hubGroup = SyncPlayGroupName(groupId);
        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Group(hubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
        }
    }

    public async Task SyncPlaySetCurrentMedia(Guid groupId, SyncPlayQueueItemDto item)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        if (!syncPlay.SetCurrentMedia(groupId, item))
        {
            await Clients.Caller.ReceiveSyncPlayError("group_not_found");
            return;
        }

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);
        var hubGroup = SyncPlayGroupName(groupId);

        var chatMsg = new SyncPlayChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            DisplayName = "",
            Text = $"{displayName} changed to {item.Title}",
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await Clients.Group(hubGroup).ReceiveSyncPlayChatMessage(chatMsg);

        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Group(hubGroup).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
        }
    }

    public async Task SyncPlayRemoveFromQueue(Guid groupId, Guid queueItemId)
    {
        if (!syncPlay.RemoveFromQueue(groupId, queueItemId))
        {
            await Clients.Caller.ReceiveSyncPlayError("not_permitted");
            return;
        }

        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
        }
    }

    public async Task SyncPlayKick(Guid groupId, Guid targetDeviceId)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var result = syncPlay.Kick(groupId, identityUserId, targetDeviceId);
        if (result is null)
        {
            await Clients.Caller.ReceiveSyncPlayError("not_permitted");
            return;
        }

        if (_connectedDevices.TryGetValue(targetDeviceId, out var targetConnection))
        {
            await Clients.Client(targetConnection.ConnectionId).ReceiveSyncPlayError("kicked");
            await Groups.RemoveFromGroupAsync(targetConnection.ConnectionId, SyncPlayGroupName(groupId));
        }

        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, null));
        }
    }

    public async Task SyncPlaySendChat(Guid groupId, string text)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var group = syncPlay.GetGroup(groupId);
        if (group is null || !group.Members.ContainsKey(device.Value.DeviceId)) return;

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);

        var message = syncPlay.SendChat(groupId, displayName, text);
        if (message is null) return;

        await Clients.OthersInGroup(SyncPlayGroupName(groupId)).ReceiveSyncPlayChatMessage(message);
    }

    public async Task SyncPlaySendReaction(Guid groupId, string emoji)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var group = syncPlay.GetGroup(groupId);
        if (group is null || !group.Members.ContainsKey(device.Value.DeviceId)) return;

        var displayName = ResolveGroupDisplayName(groupId, device.Value.DeviceId, device.Value.UserDisplayName);

        var reaction = syncPlay.SendReaction(groupId, displayName, emoji);
        if (reaction is null) return;

        await Clients.Group(SyncPlayGroupName(groupId)).ReceiveSyncPlayReaction(reaction);
    }

    public async Task SyncPlayGenerateGuestToken(Guid groupId)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var token = syncPlay.GenerateGuestToken(groupId, identityUserId);
        if (token is null)
        {
            await Clients.Caller.ReceiveSyncPlayError("not_permitted");
            return;
        }

        var group = syncPlay.GetGroup(groupId);
        if (group is not null)
        {
            await Clients.Caller.ReceiveSyncPlayGroupUpdated(ToGroupDto(group, identityUserId));
        }
    }

    public async Task SyncPlayInviteUser(string targetUserId, Guid groupId)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var group = syncPlay.GetGroup(groupId);
        if (group is null) return;

        if (Guid.TryParse(targetUserId, out var targetGuid))
        {
            var prefsJson = await userSettingsService.GetAsync(targetGuid, UserSettingKeys.SyncPlayPreferences, CancellationToken.None);
            if (prefsJson is not null)
            {
                var prefs = JsonSerializer.Deserialize<SyncPlayPreferencesDto>(prefsJson);
                if (prefs is { InvitationsEnabled: false })
                    return;
            }
        }

        var device = ResolveCallerDevice();
        if (device is null) return;

        var inviterName = device.Value.UserDisplayName;

        var invitation = new SyncPlayInvitationDto
        {
            GroupId = groupId,
            GroupName = group.GroupName,
            InviterDisplayName = inviterName,
            InviterDeviceName = device.Value.DeviceName,
            CurrentMediaTitle = group.CurrentMedia?.Title,
            ParticipantCount = group.Members.Count
        };

        await Clients.Group(targetUserId).ReceiveSyncPlayInvitation(invitation);
    }

    public async Task SyncPlayGetOnlineUsers()
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var onlineUsers = _connectedDevices.Values
            .Where(d => d.IdentityUserId != identityUserId && d.SyncPlayEnabled)
            .Select(d => d.IdentityUserId)
            .Distinct()
            .Select(uid =>
            {
                var device = _connectedDevices.Values.First(d => d.IdentityUserId == uid);
                return new SyncPlayOnlineUserDto
                {
                    UserId = uid,
                    DisplayName = device.UserDisplayName,
                    DeviceName = device.DeviceName,
                    IsInSyncPlayGroup = syncPlay.IsUserInAnyGroup(uid)
                };
            })
            .ToList();

        await Clients.Caller.ReceiveSyncPlayOnlineUsers(onlineUsers);
    }

    public void SyncPlaySetEnabled(bool enabled)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var deviceId = device.Value.DeviceId;
        if (_connectedDevices.TryGetValue(deviceId, out var existing))
        {
            _connectedDevices[deviceId] = existing with { SyncPlayEnabled = enabled };
        }
    }

    public async Task SyncPlayGetInviteLink(Guid groupId)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var token = syncPlay.GenerateInviteToken(groupId);
        if (string.IsNullOrEmpty(token))
        {
            await Clients.Caller.ReceiveSyncPlayError("group_not_found");
            return;
        }

        var link = new SyncPlayInviteLinkDto
        {
            GroupId = groupId,
            Token = token
        };

        await Clients.Caller.ReceiveSyncPlayInviteLink(link);
    }

    public async Task SyncPlayJoinViaInviteToken(string token, string? guestDisplayName)
    {
        var groupId = syncPlay.ResolveInviteToken(token);
        if (groupId is null)
        {
            await Clients.Caller.ReceiveSyncPlayError("invalid_invite_link");
            return;
        }

        var identityUserId = ResolveIdentityUserId();
        var isGuest = string.IsNullOrEmpty(identityUserId);

        var device = ResolveCallerDevice();
        if (device is null) return;

        var displayName = !string.IsNullOrEmpty(guestDisplayName)
            ? guestDisplayName
            : isGuest ? "Guest" : device.Value.UserDisplayName;

        var group = syncPlay.JoinGroup(groupId.Value, identityUserId, device.Value.DeviceId, displayName, device.Value.DeviceType, isGuest);
        if (group is null)
        {
            await Clients.Caller.ReceiveSyncPlayError("group_not_found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SyncPlayGroupName(groupId.Value));
        await Clients.Group(SyncPlayGroupName(groupId.Value)).ReceiveSyncPlayGroupUpdated(ToGroupDto(group, identityUserId));
    }

    private static string SyncPlayGroupName(Guid groupId) => $"syncplay-{groupId}";

    private static string FormatPosition(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private (Guid DeviceId, string DeviceName, string DeviceType, string UserDisplayName)? ResolveCallerDevice()
    {
        var entry = _connectedDevices.FirstOrDefault(kvp => kvp.Value.ConnectionId == Context.ConnectionId);
        if (entry.Value is null)
            return null;

        return (entry.Key, entry.Value.DeviceName, entry.Value.DeviceType, entry.Value.UserDisplayName);
    }

    private string ResolveGroupDisplayName(Guid groupId, Guid deviceId, string fallback)
    {
        var group = syncPlay.GetGroup(groupId);
        if (group is not null && group.Members.TryGetValue(deviceId, out var member))
            return member.DisplayName;

        return fallback;
    }

    private static SyncPlayGroupDto ToGroupDto(SyncPlayGroupInfo group, string? requestingUserId)
    {
        var position = group.Position;
        if (group.State == SyncPlayGroupState.Playing && group.PlayStartedAtUtc is not null)
        {
            var elapsed = (DateTime.UtcNow - group.PlayStartedAtUtc.Value).TotalSeconds;
            position = group.PlayStartedAtPosition + elapsed;
        }

        return new SyncPlayGroupDto
        {
            GroupId = group.GroupId,
            GroupName = group.GroupName,
            State = group.State,
            CurrentMedia = group.CurrentMedia,
            Position = position,
            Duration = group.Duration,
            Participants = group.Members.Values.Select(m => new SyncPlayParticipantDto
            {
                UserId = m.IdentityUserId,
                DisplayName = m.DisplayName,
                DeviceId = m.DeviceId,
                DeviceName = m.DeviceName,
                IsReady = m.IsReady,
                IsGuest = m.IsGuest
            }).ToList(),
            Queue = group.Queue.ToList(),
            GuestToken = requestingUserId == group.CreatorUserId ? group.GuestToken : null
        };
    }

    private sealed record DeviceConnection(string ConnectionId, string IdentityUserId, string UserDisplayName, string DeviceName, string DeviceType, bool SyncPlayEnabled = true);
}
