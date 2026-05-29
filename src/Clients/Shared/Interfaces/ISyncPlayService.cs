using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface ISyncPlayService
{
    bool IsInGroup { get; }
    bool IsEnabled { get; set; }
    bool ShowChat { get; set; }
    bool ShowReactions { get; set; }
    SyncPlayGroupDto? CurrentGroup { get; }
    IReadOnlyList<SyncPlayChatMessageDto> ChatMessages { get; }
    IReadOnlyList<SyncPlayOnlineUserDto> OnlineUsers { get; }

    event Action? GroupUpdated;
    event Action<SyncPlayCommandDto>? CommandReceived;
    event Action<long, double>? PlayAtReceived;
    event Action<double>? SeekCorrectionReceived;
    event Action<SyncPlayChatMessageDto>? ChatMessageReceived;
    event Action<SyncPlayReactionDto>? ReactionReceived;
    event Action<string>? ErrorReceived;
    event Action<SyncPlayInvitationDto>? InvitationReceived;
    event Action? OnlineUsersUpdated;
    event Action<SyncPlayInviteLinkDto>? InviteLinkReceived;
    event Action? RejoinRequested;

    Task CreateGroupAsync(Guid? mediaReferenceId = null, string? mediaTitle = null, double mediaDuration = 0, string? mediaCoverUrl = null, double initialPosition = 0, bool isPlaying = false);
    Task JoinGroupAsync(Guid groupId, string? guestToken = null, string? guestDisplayName = null);
    Task JoinViaInviteTokenAsync(string token, string? guestDisplayName = null);
    Task LeaveGroupAsync();
    Task IssueCommandAsync(SyncPlayCommandType commandType, double? value = null);
    Task ReportReadyAsync();
    Task ReportPositionAsync(double position);
    Task AddToQueueAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl);
    Task BulkAddToQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> items);
    Task SetCurrentMediaAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl);
    Task RemoveFromQueueAsync(Guid queueItemId);
    Task KickAsync(Guid targetDeviceId);
    bool IsOwnMessage(Guid messageId);
    Task SendChatAsync(string text);
    Task SendReactionAsync(string emoji);
    Task GenerateGuestTokenAsync();
    Task InviteUserAsync(string targetUserId);
    Task RefreshOnlineUsersAsync();
    Task GetInviteLinkAsync();
    void RequestRejoin();
}
