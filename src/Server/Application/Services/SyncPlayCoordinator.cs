using System.Collections.Concurrent;
using System.Security.Cryptography;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public readonly record struct SeekCorrection(Guid DeviceId, double Position);

public interface ISyncPlayCoordinator
{
    SyncPlayGroupInfo CreateGroup(string identityUserId, Guid deviceId, string displayName, string deviceName, SyncPlayQueueItemDto? initialMedia = null, double initialPosition = 0, bool isPlaying = false);
    SyncPlayGroupInfo? JoinGroup(Guid groupId, string? identityUserId, Guid deviceId, string displayName, string deviceName, bool isGuest);
    DisconnectResult LeaveGroup(Guid groupId, Guid deviceId);
    CommandResult IssueCommand(Guid groupId, Guid deviceId, SyncPlayCommandType commandType, double? value);
    ReadyResult ReportReady(Guid groupId, Guid deviceId);
    SeekCorrection? ReportPosition(Guid groupId, Guid deviceId, double position);
    SyncPlayGroupInfo? GetGroup(Guid groupId);
    void CleanupStaleGroups();
    bool AddToQueue(Guid groupId, Guid deviceId, SyncPlayQueueItemDto item);
    bool SetCurrentMedia(Guid groupId, Guid deviceId, SyncPlayQueueItemDto item);
    SyncPlayQueueItemDto? NavigateQueue(Guid groupId, Guid deviceId, bool forward);
    bool RemoveFromQueue(Guid groupId, Guid deviceId, Guid queueItemId);
    KickResult? Kick(Guid groupId, string identityUserId, Guid targetDeviceId);
    SyncPlayChatMessageDto? SendChat(Guid groupId, Guid deviceId, string text);
    SyncPlayReactionDto? SendReaction(Guid groupId, Guid deviceId, string emoji);
    string? GenerateGuestToken(Guid groupId, string identityUserId);
    bool ValidateGuestToken(Guid groupId, string token);
    string? GenerateInviteToken(Guid groupId, string identityUserId);
    Guid? ResolveInviteToken(string token);
    DisconnectResult DisconnectDevice(Guid deviceId);
    bool IsUserInAnyGroup(string identityUserId);
}

public sealed record SyncPlayGroupInfo
{
    public required Guid GroupId { get; init; }
    public required string GroupName { get; init; }
    public required string CreatorUserId { get; init; }
    public required SyncPlayGroupState State { get; set; }
    public SyncPlayQueueItemDto? CurrentMedia { get; set; }
    public double Position { get; set; }
    public double Duration { get; set; }
    public DateTime LastActivity { get; set; }
    public string? GuestToken { get; set; }
    public string? InviteToken { get; set; }
    public ConcurrentDictionary<Guid, SyncPlayMember> Members { get; } = new();
    public List<SyncPlayQueueItemDto> Queue { get; } = [];
    public object Gate { get; } = new();

    public IReadOnlyList<SyncPlayQueueItemDto> SnapshotQueue()
    {
        lock (Gate)
            return Queue.ToList();
    }

    public DateTime? PlayStartedAtUtc { get; set; }
    public double PlayStartedAtPosition { get; set; }
}

public sealed record SyncPlayMember
{
    public string? IdentityUserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required string DisplayName { get; init; }
    public required string DeviceName { get; init; }
    public bool IsGuest { get; init; }
    public bool IsReady { get; set; }
    public double LastPosition { get; set; }
    public DateTime LastActivityUtc { get; set; }
}

public sealed record DisconnectResult
{
    public required Guid GroupId { get; init; }
    public bool GroupDestroyed { get; init; }
    public Guid DisconnectedDeviceId { get; init; }
}

public sealed record CommandResult
{
    public required bool Permitted { get; init; }
    public SyncPlayGroupState NewState { get; init; }
    public double? SeekPosition { get; init; }
}

public sealed record ReadyResult
{
    public required bool AllReady { get; init; }
    public long PlayAtTimestampMs { get; init; }
    public double Position { get; init; }
    public bool CatchUpOnly { get; init; }
}

public sealed record KickResult
{
    public required Guid TargetDeviceId { get; init; }
    public required string TargetConnectionId { get; init; }
}

public class SyncPlayCoordinator : ISyncPlayCoordinator
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PlayAtBuffer = TimeSpan.FromMilliseconds(500);

    private readonly ConcurrentDictionary<Guid, SyncPlayGroupInfo> _groups = new();
    private readonly ConcurrentDictionary<string, Guid> _inviteTokens = new();
    private readonly ILogger<SyncPlayCoordinator> _logger;

    public SyncPlayCoordinator(ILogger<SyncPlayCoordinator> logger)
    {
        _logger = logger;
    }

    public SyncPlayGroupInfo CreateGroup(string identityUserId, Guid deviceId, string displayName, string deviceName, SyncPlayQueueItemDto? initialMedia = null, double initialPosition = 0, bool isPlaying = false)
    {
        var group = new SyncPlayGroupInfo
        {
            GroupId = Guid.NewGuid(),
            GroupName = $"{displayName}'s session",
            CreatorUserId = identityUserId,
            State = isPlaying ? SyncPlayGroupState.Playing : SyncPlayGroupState.Idle,
            CurrentMedia = initialMedia,
            Position = initialPosition,
            Duration = initialMedia?.Duration ?? 0,
            LastActivity = DateTime.UtcNow
        };

        if (isPlaying)
        {
            group.PlayStartedAtUtc = DateTime.UtcNow;
            group.PlayStartedAtPosition = initialPosition;
        }

        if (initialMedia is not null)
        {
            lock (group.Gate)
                group.Queue.Add(initialMedia);
        }

        var member = new SyncPlayMember
        {
            IdentityUserId = identityUserId,
            DeviceId = deviceId,
            DisplayName = displayName,
            DeviceName = deviceName,
            IsGuest = false,
            LastActivityUtc = DateTime.UtcNow
        };

        group.Members[deviceId] = member;
        _groups[group.GroupId] = group;

        return group;
    }

    public SyncPlayGroupInfo? JoinGroup(Guid groupId, string? identityUserId, Guid deviceId, string displayName, string deviceName, bool isGuest)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        var member = new SyncPlayMember
        {
            IdentityUserId = identityUserId,
            DeviceId = deviceId,
            DisplayName = displayName,
            DeviceName = deviceName,
            IsGuest = isGuest,
            LastActivityUtc = DateTime.UtcNow
        };

        group.Members[deviceId] = member;
        group.LastActivity = DateTime.UtcNow;

        return group;
    }

    public DisconnectResult LeaveGroup(Guid groupId, Guid deviceId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return new DisconnectResult { GroupId = groupId, GroupDestroyed = true, DisconnectedDeviceId = deviceId };

        group.Members.TryRemove(deviceId, out var removedMember);
        group.LastActivity = DateTime.UtcNow;

        if (group.Members.IsEmpty)
        {
            _groups.TryRemove(groupId, out _);
            if (group.InviteToken is not null)
            {
                _inviteTokens.TryRemove(group.InviteToken, out _);
            }
            return new DisconnectResult { GroupId = groupId, GroupDestroyed = true, DisconnectedDeviceId = deviceId };
        }

        return new DisconnectResult { GroupId = groupId, GroupDestroyed = false, DisconnectedDeviceId = deviceId };
    }

    public CommandResult IssueCommand(Guid groupId, Guid deviceId, SyncPlayCommandType commandType, double? value)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return new CommandResult { Permitted = false };

        group.LastActivity = DateTime.UtcNow;

        lock (group.Gate)
        {
            switch (commandType)
            {
                case SyncPlayCommandType.Play:
                    group.State = SyncPlayGroupState.Playing;
                    group.PlayStartedAtUtc = DateTime.UtcNow;
                    group.PlayStartedAtPosition = group.Position;
                    return new CommandResult { Permitted = true, NewState = SyncPlayGroupState.Playing };

                case SyncPlayCommandType.Pause:
                    group.State = SyncPlayGroupState.Paused;
                    group.Position = value ?? group.Position;
                    group.PlayStartedAtUtc = null;
                    return new CommandResult { Permitted = true, NewState = SyncPlayGroupState.Paused };

                case SyncPlayCommandType.SeekTo:
                    group.State = SyncPlayGroupState.WaitingForReady;
                    group.Position = value ?? 0;
                    group.PlayStartedAtUtc = null;
                    ResetAllReady(group);
                    return new CommandResult { Permitted = true, NewState = SyncPlayGroupState.WaitingForReady, SeekPosition = value };

                case SyncPlayCommandType.NextInQueue:
                case SyncPlayCommandType.PreviousInQueue:
                    return new CommandResult { Permitted = true, NewState = group.State };

                default:
                    return new CommandResult { Permitted = false };
            }
        }
    }

    public ReadyResult ReportReady(Guid groupId, Guid deviceId)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return new ReadyResult { AllReady = false };

        lock (group.Gate)
        {
            if (group.Members.TryGetValue(deviceId, out var member))
            {
                member.IsReady = true;
                member.LastActivityUtc = DateTime.UtcNow;
            }

            group.LastActivity = DateTime.UtcNow;

            // If the group is already playing, send a catch-up PlayAt to the joining member
            if (group.State == SyncPlayGroupState.Playing)
            {
                var estimatedPosition = group.PlayStartedAtPosition;
                if (group.PlayStartedAtUtc is not null)
                {
                    var elapsed = (DateTime.UtcNow - group.PlayStartedAtUtc.Value).TotalSeconds;
                    estimatedPosition += elapsed;
                }

                var catchUpAt = DateTimeOffset.UtcNow.Add(PlayAtBuffer).ToUnixTimeMilliseconds();
                _logger.LogDebug("[SyncPlay Server] ReportReady: device={Device}, group already Playing - sending catch-up at {Pos}", deviceId, estimatedPosition);
                return new ReadyResult { AllReady = true, CatchUpOnly = true, PlayAtTimestampMs = catchUpAt, Position = estimatedPosition };
            }

            var members = group.Members.Values.ToList();
            var readyCount = members.Count(m => m.IsReady);
            var totalCount = members.Count;
            _logger.LogDebug("[SyncPlay Server] ReportReady: device={Device}, state={State}, ready={Ready}/{Total}", deviceId, group.State, readyCount, totalCount);

            var allReady = totalCount > 0 && readyCount == totalCount;
            if (!allReady)
                return new ReadyResult { AllReady = false };

            var playAt = DateTimeOffset.UtcNow.Add(PlayAtBuffer).ToUnixTimeMilliseconds();
            group.State = SyncPlayGroupState.Playing;
            group.PlayStartedAtUtc = DateTime.UtcNow.Add(PlayAtBuffer);
            group.PlayStartedAtPosition = group.Position;

            return new ReadyResult { AllReady = true, PlayAtTimestampMs = playAt, Position = group.Position };
        }
    }

    public SeekCorrection? ReportPosition(Guid groupId, Guid deviceId, double position)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return null;

        if (group.Members.TryGetValue(deviceId, out var member))
        {
            member.LastPosition = position;
            member.LastActivityUtc = DateTime.UtcNow;
        }

        group.LastActivity = DateTime.UtcNow;

        // No server-side drift detection - sync happens only at state transitions (Play/Pause/Seek/PlayAt).
        // Clients are trusted to maintain sync between transitions.
        return null;
    }

    public SyncPlayGroupInfo? GetGroup(Guid groupId)
    {
        _groups.TryGetValue(groupId, out var group);
        return group;
    }

    public void CleanupStaleGroups()
    {
        var cutoff = DateTime.UtcNow - StaleThreshold;
        var staleGroups = _groups.Where(kv => kv.Value.LastActivity < cutoff).Select(kv => kv.Key).ToList();

        foreach (var key in staleGroups)
        {
            if (_groups.TryRemove(key, out var removed) && removed.InviteToken is not null)
            {
                _inviteTokens.TryRemove(removed.InviteToken, out _);
            }
        }
    }

    public bool AddToQueue(Guid groupId, Guid deviceId, SyncPlayQueueItemDto item)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return false;

        lock (group.Gate)
        {
            if (group.Queue.Any(q => q.MediaReferenceId == item.MediaReferenceId))
                return true;

            group.Queue.Add(item);
            group.LastActivity = DateTime.UtcNow;
            return true;
        }
    }

    public bool SetCurrentMedia(Guid groupId, Guid deviceId, SyncPlayQueueItemDto item)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return false;

        lock (group.Gate)
        {
            var existing = group.Queue.FirstOrDefault(q => q.MediaReferenceId == item.MediaReferenceId);
            if (existing is not null)
            {
                item = existing;
            }
            else
            {
                group.Queue.Add(item);
            }

            group.CurrentMedia = item;
            group.Duration = item.Duration;
            group.Position = 0;
            group.State = SyncPlayGroupState.WaitingForReady;
            group.PlayStartedAtUtc = null;
            group.LastActivity = DateTime.UtcNow;
            ResetAllReady(group);
            return true;
        }
    }

    public bool RemoveFromQueue(Guid groupId, Guid deviceId, Guid queueItemId)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out var member))
            return false;

        if (group.CreatorUserId != member.IdentityUserId)
            return false;

        lock (group.Gate)
        {
            var item = group.Queue.FirstOrDefault(q => q.QueueItemId == queueItemId);
            if (item is null)
                return false;

            group.Queue.Remove(item);
            group.LastActivity = DateTime.UtcNow;
            return true;
        }
    }

    public SyncPlayQueueItemDto? NavigateQueue(Guid groupId, Guid deviceId, bool forward)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out _))
            return null;

        lock (group.Gate)
        {
            if (group.CurrentMedia is null || group.Queue.Count == 0)
                return null;

            var currentIndex = group.Queue.FindIndex(q => q.QueueItemId == group.CurrentMedia.QueueItemId);
            var nextIndex = forward ? currentIndex + 1 : currentIndex - 1;

            if (nextIndex < 0 || nextIndex >= group.Queue.Count)
                return null;

            var nextItem = group.Queue[nextIndex];
            group.CurrentMedia = nextItem;
            group.Duration = nextItem.Duration;
            group.Position = 0;
            group.State = SyncPlayGroupState.WaitingForReady;
            group.PlayStartedAtUtc = null;
            group.LastActivity = DateTime.UtcNow;
            ResetAllReady(group);
            return nextItem;
        }
    }

    public KickResult? Kick(Guid groupId, string identityUserId, Guid targetDeviceId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        if (group.CreatorUserId != identityUserId)
            return null;

        if (!group.Members.TryRemove(targetDeviceId, out _))
            return null;

        group.LastActivity = DateTime.UtcNow;
        return new KickResult { TargetDeviceId = targetDeviceId, TargetConnectionId = string.Empty };
    }

    public SyncPlayChatMessageDto? SendChat(Guid groupId, Guid deviceId, string text)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out var member))
            return null;

        group.LastActivity = DateTime.UtcNow;

        return new SyncPlayChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            DisplayName = member.DisplayName,
            Text = text,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public SyncPlayReactionDto? SendReaction(Guid groupId, Guid deviceId, string emoji)
    {
        if (!TryGetMember(groupId, deviceId, out var group, out var member))
            return null;

        group.LastActivity = DateTime.UtcNow;

        return new SyncPlayReactionDto
        {
            DisplayName = member.DisplayName,
            Emoji = emoji,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public string? GenerateGuestToken(Guid groupId, string identityUserId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        if (group.CreatorUserId != identityUserId)
            return null;

        if (group.GuestToken is not null)
            return group.GuestToken;

        group.GuestToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        return group.GuestToken;
    }

    public bool ValidateGuestToken(Guid groupId, string token)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        return group.GuestToken is not null && group.GuestToken == token;
    }

    public DisconnectResult DisconnectDevice(Guid deviceId)
    {
        foreach (var group in _groups.Values)
        {
            if (group.Members.ContainsKey(deviceId))
            {
                return LeaveGroup(group.GroupId, deviceId);
            }
        }

        return new DisconnectResult { GroupId = Guid.Empty, GroupDestroyed = false, DisconnectedDeviceId = deviceId };
    }

    public string? GenerateInviteToken(Guid groupId, string identityUserId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        if (group.CreatorUserId != identityUserId)
            return null;

        if (group.InviteToken is not null)
            return group.InviteToken;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        group.InviteToken = token;
        _inviteTokens[token] = groupId;
        return token;
    }

    public Guid? ResolveInviteToken(string token)
    {
        if (_inviteTokens.TryGetValue(token, out var groupId) && _groups.ContainsKey(groupId))
            return groupId;

        return null;
    }

    public bool IsUserInAnyGroup(string identityUserId)
    {
        return _groups.Values.Any(g => g.Members.Values.Any(m => m.IdentityUserId == identityUserId));
    }

    private bool TryGetMember(
        Guid groupId,
        Guid deviceId,
        out SyncPlayGroupInfo group,
        out SyncPlayMember member)
    {
        member = null!;
        if (!_groups.TryGetValue(groupId, out group!))
            return false;

        if (!group.Members.TryGetValue(deviceId, out member!))
            return false;

        return true;
    }

    private static void ResetAllReady(SyncPlayGroupInfo group)
    {
        foreach (var member in group.Members.Values)
            member.IsReady = false;
    }
}
