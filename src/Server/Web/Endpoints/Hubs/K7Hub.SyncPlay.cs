using System.Text.Json;
using K7.Server.Application.Services;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Web.Endpoints.Hubs;

/// <summary>
/// SyncPlay (watch party) hub methods - split out of K7Hub.cs to keep the primary
/// hub file focused on connection lifecycle, streaming, and presence concerns.
/// </summary>
public partial class K7Hub
{
    public async Task CreateSyncPlayGroup(SyncPlayCreateGroupDto request)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId)) return;

        var device = ResolveCallerDevice();
        if (device is null)
        {
            logger.LogWarning("CreateSyncPlayGroup aborted: device not registered. ConnectionId='{ConnectionId}'", Context.ConnectionId);
            await Clients.Caller.ReceiveSyncPlayError("device_not_registered");
            return;
        }

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

        var group = syncPlay.CreateGroup(identityUserId, device.Value.DeviceId, device.Value.UserDisplayName, device.Value.DeviceName, initialMedia, request.InitialPosition, request.IsPlaying);

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

        var group = syncPlay.JoinGroup(groupId, identityUserId, device.Value.DeviceId, displayName, device.Value.DeviceName, isGuest);
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
            var nextItem = syncPlay.NavigateQueue(groupId, device.Value.DeviceId, commandType == SyncPlayCommandType.NextInQueue);
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

        var result = syncPlay.IssueCommand(groupId, device.Value.DeviceId, commandType, value);

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

        if (!syncPlay.AddToQueue(groupId, device.Value.DeviceId, item))
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
            if (!syncPlay.AddToQueue(groupId, device.Value.DeviceId, item))
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

        if (!syncPlay.SetCurrentMedia(groupId, device.Value.DeviceId, item))
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
        var device = ResolveCallerDevice();
        if (device is null) return;

        if (!syncPlay.RemoveFromQueue(groupId, device.Value.DeviceId, queueItemId))
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

        if (presenceTracker.TryGetDevice(targetDeviceId, out var targetConnection))
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

        if (!syncPlay.TryConsumeChatRateLimit(groupId, device.Value.DeviceId))
        {
            await Clients.Caller.ReceiveSyncPlayError("rate_limited");
            return;
        }

        var message = syncPlay.SendChat(groupId, device.Value.DeviceId, text);
        if (message is null) return;

        await Clients.OthersInGroup(SyncPlayGroupName(groupId)).ReceiveSyncPlayChatMessage(message);
    }

    public async Task SyncPlaySendReaction(Guid groupId, string emoji)
    {
        var device = ResolveCallerDevice();
        if (device is null) return;

        var group = syncPlay.GetGroup(groupId);
        if (group is null || !group.Members.ContainsKey(device.Value.DeviceId)) return;

        if (!syncPlay.TryConsumeReactionRateLimit(groupId, device.Value.DeviceId))
        {
            await Clients.Caller.ReceiveSyncPlayError("rate_limited");
            return;
        }

        var reaction = syncPlay.SendReaction(groupId, device.Value.DeviceId, emoji);
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
            var prefsJson = await userSettingsService.GetAsync(targetGuid, UserSettingKeys.SyncPlayPreferences, Context.ConnectionAborted);
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
        if (string.IsNullOrEmpty(identityUserId))
            return;

        var onlineUsers = presenceTracker.GetAllDevices()
            .Select(kvp => kvp.Value)
            .Where(d => d.IdentityUserId != identityUserId && d.SyncPlayEnabled)
            .Select(d => d.IdentityUserId)
            .Distinct()
            .Select(uid =>
            {
                var device = presenceTracker.GetAllDevices()
                    .Select(kvp => kvp.Value)
                    .First(d => d.IdentityUserId == uid);
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
        if (presenceTracker.TryGetDevice(deviceId, out var existing))
        {
            presenceTracker.UpdateDevice(deviceId, existing with { SyncPlayEnabled = enabled });
        }
    }

    public async Task SyncPlayGetInviteLink(Guid groupId)
    {
        var identityUserId = ResolveIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId))
            return;

        var token = await syncPlay.GenerateInviteTokenAsync(groupId, identityUserId, Context.ConnectionAborted);
        if (string.IsNullOrEmpty(token))
        {
            await Clients.Caller.ReceiveSyncPlayError("not_permitted");
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
        var groupId = await syncPlay.ResolveInviteTokenAsync(token, Context.ConnectionAborted);
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

        var group = syncPlay.JoinGroup(groupId.Value, identityUserId, device.Value.DeviceId, displayName, device.Value.DeviceName, isGuest);
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
        var entry = presenceTracker.FindByConnectionId(Context.ConnectionId);
        if (entry is not null)
            return (entry.Value.DeviceId, entry.Value.Connection.DeviceName, entry.Value.Connection.DeviceType, entry.Value.Connection.UserDisplayName);

        if (!TryRegisterCallerDeviceFromQuery(out var deviceId, out var connection))
            return null;

        return (deviceId, connection.DeviceName, connection.DeviceType, connection.UserDisplayName);
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
            Queue = group.SnapshotQueue(),
            GuestToken = requestingUserId == group.CreatorUserId ? group.GuestToken : null
        };
    }
}
