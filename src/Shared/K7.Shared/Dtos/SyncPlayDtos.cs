namespace K7.Shared.Dtos;

public enum SyncPlayGroupState
{
    Idle,
    WaitingForReady,
    Playing,
    Paused
}

public enum SyncPlayCommandType
{
    Play,
    Pause,
    SeekTo,
    NextInQueue,
    PreviousInQueue
}

public sealed record SyncPlayGroupDto
{
    public required Guid GroupId { get; init; }
    public required string GroupName { get; init; }
    public required SyncPlayGroupState State { get; init; }
    public SyncPlayQueueItemDto? CurrentMedia { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public required IReadOnlyList<SyncPlayParticipantDto> Participants { get; init; }
    public required IReadOnlyList<SyncPlayQueueItemDto> Queue { get; init; }
    public string? GuestToken { get; init; }
}

public sealed record SyncPlayParticipantDto
{
    public string? UserId { get; init; }
    public required string DisplayName { get; init; }
    public required Guid DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public bool IsReady { get; init; }
    public bool IsGuest { get; init; }
}

public sealed record SyncPlayCommandDto
{
    public required SyncPlayCommandType CommandType { get; init; }
    public double? Value { get; init; }
    public required string IssuedByDisplayName { get; init; }
}

public sealed record SyncPlayQueueItemDto
{
    public required Guid QueueItemId { get; init; }
    public required Guid MediaReferenceId { get; init; }
    public required string Title { get; init; }
    public double Duration { get; init; }
    public string? AddedByDisplayName { get; init; }
    public string? CoverUrl { get; init; }
}

public sealed record SyncPlayCreateGroupDto
{
    public Guid? InitialMediaReferenceId { get; init; }
    public string? InitialMediaTitle { get; init; }
    public double InitialMediaDuration { get; init; }
    public string? InitialMediaCoverUrl { get; init; }
    public double InitialPosition { get; init; }
    public bool IsPlaying { get; init; }
}

public sealed record SyncPlayChatMessageDto
{
    public required Guid MessageId { get; init; }
    public required string DisplayName { get; init; }
    public required string Text { get; init; }
    public required long TimestampMs { get; init; }
}

public sealed record SyncPlayReactionDto
{
    public required string DisplayName { get; init; }
    public required string Emoji { get; init; }
    public required long TimestampMs { get; init; }
}

public sealed record SyncPlayInvitationDto
{
    public required Guid GroupId { get; init; }
    public required string GroupName { get; init; }
    public required string InviterDisplayName { get; init; }
    public string? InviterDeviceName { get; init; }
    public string? CurrentMediaTitle { get; init; }
    public int ParticipantCount { get; init; }
}

public sealed record SyncPlayInviteLinkDto
{
    public required Guid GroupId { get; init; }
    public required string Token { get; init; }
}

public sealed record SyncPlayOnlineUserDto
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? DeviceName { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsInSyncPlayGroup { get; init; }
}
