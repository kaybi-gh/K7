using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.Shared.Services;

public sealed class SyncPlayService : ISyncPlayService, IDisposable
{
    private const int MaxChatMessages = 200;

    private readonly K7HubClient _hubClient;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly ILocalUserService _localUserService;
    private readonly List<SyncPlayChatMessageDto> _chatMessages = [];
    private readonly HashSet<Guid> _sentMessageIds = [];
    private List<SyncPlayOnlineUserDto> _onlineUsers = [];

    public SyncPlayService(K7HubClient hubClient, IDeviceStorageService deviceStorage, ILocalUserService localUserService)
    {
        _hubClient = hubClient;
        _deviceStorage = deviceStorage;
        _localUserService = localUserService;

        _hubClient.SyncPlayGroupUpdated += OnGroupUpdated;
        _hubClient.SyncPlayCommandReceived += OnCommandReceived;
        _hubClient.SyncPlayPlayAtReceived += OnPlayAtReceived;
        _hubClient.SyncPlaySeekCorrectionReceived += OnSeekCorrectionReceived;
        _hubClient.SyncPlayChatMessageReceived += OnChatMessageReceived;
        _hubClient.SyncPlayReactionReceived += OnReactionReceived;
        _hubClient.SyncPlayErrorReceived += OnErrorReceived;
        _hubClient.SyncPlayInvitationReceived += OnInvitationReceived;
        _hubClient.SyncPlayOnlineUsersReceived += OnOnlineUsersReceived;
        _hubClient.SyncPlayInviteLinkReceived += OnInviteLinkReceived;
        _hubClient.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public bool IsInGroup => CurrentGroup is not null;
    public bool IsEnabled
    {
        get => _deviceStorage.Get(PreferenceKeys.SYNCPLAY_ENABLED, true);
        set
        {
            _deviceStorage.Set(PreferenceKeys.SYNCPLAY_ENABLED, value);
            _ = _hubClient.SyncPlaySetEnabledAsync(value);
        }
    }
    public bool ShowChat { get; set; } = true;
    public bool ShowReactions { get; set; } = true;
    public SyncPlayGroupDto? CurrentGroup { get; private set; }
    public IReadOnlyList<SyncPlayChatMessageDto> ChatMessages => _chatMessages;
    public IReadOnlyList<SyncPlayOnlineUserDto> OnlineUsers => _onlineUsers;

    public event Action? GroupUpdated;
    public event Action<SyncPlayCommandDto>? CommandReceived;
    public event Action<long, double>? PlayAtReceived;
    public event Action<double>? SeekCorrectionReceived;
    public event Action<SyncPlayChatMessageDto>? ChatMessageReceived;
    public event Action<SyncPlayReactionDto>? ReactionReceived;
    public event Action<string>? ErrorReceived;
    public event Action<SyncPlayInvitationDto>? InvitationReceived;
    public event Action? OnlineUsersUpdated;
    public event Action<SyncPlayInviteLinkDto>? InviteLinkReceived;

    public async Task CreateGroupAsync(Guid? mediaReferenceId = null, string? mediaTitle = null, double mediaDuration = 0, string? mediaCoverUrl = null, double initialPosition = 0, bool isPlaying = false)
    {
        var request = new SyncPlayCreateGroupDto
        {
            InitialMediaReferenceId = mediaReferenceId,
            InitialMediaTitle = mediaTitle,
            InitialMediaDuration = mediaDuration,
            InitialMediaCoverUrl = mediaCoverUrl,
            InitialPosition = initialPosition,
            IsPlaying = isPlaying
        };

        await _hubClient.CreateSyncPlayGroupAsync(request);
    }

    public Task JoinGroupAsync(Guid groupId, string? guestToken = null, string? guestDisplayName = null)
    {
        return _hubClient.JoinSyncPlayGroupAsync(groupId, guestToken, guestDisplayName);
    }

    public async Task LeaveGroupAsync()
    {
        if (CurrentGroup is null) return;

        await _hubClient.LeaveSyncPlayGroupAsync(CurrentGroup.GroupId);
        CurrentGroup = null;
        _chatMessages.Clear();
        GroupUpdated?.Invoke();
    }

    public Task IssueCommandAsync(SyncPlayCommandType commandType, double? value = null)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayCommandAsync(CurrentGroup.GroupId, commandType, value);
    }

    public Task ReportReadyAsync()
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayReportReadyAsync(CurrentGroup.GroupId);
    }

    public Task ReportPositionAsync(double position)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayReportPositionAsync(CurrentGroup.GroupId, position);
    }

    public Task AddToQueueAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl)
    {
        if (CurrentGroup is null) return Task.CompletedTask;

        var item = new SyncPlayQueueItemDto
        {
            QueueItemId = Guid.NewGuid(),
            MediaReferenceId = mediaReferenceId,
            Title = title,
            Duration = duration,
            CoverUrl = coverUrl
        };

        return _hubClient.SyncPlayAddToQueueAsync(CurrentGroup.GroupId, item);
    }

    public Task BulkAddToQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> items)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayBulkAddToQueueAsync(CurrentGroup.GroupId, items);
    }

    public Task SetCurrentMediaAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl)
    {
        if (CurrentGroup is null) return Task.CompletedTask;

        var item = new SyncPlayQueueItemDto
        {
            QueueItemId = Guid.NewGuid(),
            MediaReferenceId = mediaReferenceId,
            Title = title,
            Duration = duration,
            CoverUrl = coverUrl
        };

        return _hubClient.SyncPlaySetCurrentMediaAsync(CurrentGroup.GroupId, item);
    }

    public Task RemoveFromQueueAsync(Guid queueItemId)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayRemoveFromQueueAsync(CurrentGroup.GroupId, queueItemId);
    }

    public Task KickAsync(Guid targetDeviceId)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayKickAsync(CurrentGroup.GroupId, targetDeviceId);
    }

    public bool IsOwnMessage(Guid messageId) => _sentMessageIds.Contains(messageId);

    public async Task SendChatAsync(string text)
    {
        if (CurrentGroup is null || string.IsNullOrWhiteSpace(text)) return;

        var myUserId = _localUserService.GetLastActive()?.IdentityUserId ?? _hubClient.ConnectedUserId;
        var displayName = CurrentGroup.Participants
            .FirstOrDefault(p => p.UserId == myUserId)?.DisplayName ?? "You";

        var localMessage = new SyncPlayChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            DisplayName = displayName,
            Text = text.Trim(),
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        _sentMessageIds.Add(localMessage.MessageId);
        OnChatMessageReceived(localMessage);

        await _hubClient.SyncPlaySendChatAsync(CurrentGroup.GroupId, text);
    }

    public Task SendReactionAsync(string emoji)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlaySendReactionAsync(CurrentGroup.GroupId, emoji);
    }

    public Task GenerateGuestTokenAsync()
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayGenerateGuestTokenAsync(CurrentGroup.GroupId);
    }

    public Task JoinViaInviteTokenAsync(string token, string? guestDisplayName = null)
    {
        return _hubClient.SyncPlayJoinViaInviteTokenAsync(token, guestDisplayName);
    }

    public Task InviteUserAsync(string targetUserId)
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayInviteUserAsync(targetUserId, CurrentGroup.GroupId);
    }

    public Task RefreshOnlineUsersAsync()
    {
        return _hubClient.SyncPlayGetOnlineUsersAsync();
    }

    public Task GetInviteLinkAsync()
    {
        if (CurrentGroup is null) return Task.CompletedTask;
        return _hubClient.SyncPlayGetInviteLinkAsync(CurrentGroup.GroupId);
    }

    public event Action? RejoinRequested;

    public void RequestRejoin()
    {
        RejoinRequested?.Invoke();
    }

    private void OnGroupUpdated(SyncPlayGroupDto group)
    {

        CurrentGroup = group;
        GroupUpdated?.Invoke();
    }

    private void OnCommandReceived(SyncPlayCommandDto command)
    {
        CommandReceived?.Invoke(command);
    }

    private void OnPlayAtReceived(long timestampMs, double position)
    {
        PlayAtReceived?.Invoke(timestampMs, position);
    }

    private void OnSeekCorrectionReceived(double position)
    {
        SeekCorrectionReceived?.Invoke(position);
    }

    private void OnChatMessageReceived(SyncPlayChatMessageDto message)
    {
        _chatMessages.Add(message);

        if (_chatMessages.Count > MaxChatMessages)
        {
            _chatMessages.RemoveAt(0);
        }

        ChatMessageReceived?.Invoke(message);
    }

    private void OnReactionReceived(SyncPlayReactionDto reaction)
    {
        ReactionReceived?.Invoke(reaction);
    }

    private void OnErrorReceived(string errorCode)
    {
        if (errorCode == "kicked")
        {
            CurrentGroup = null;
            _chatMessages.Clear();
            GroupUpdated?.Invoke();
        }

        ErrorReceived?.Invoke(errorCode);
    }

    private void OnInvitationReceived(SyncPlayInvitationDto invitation)
    {
        InvitationReceived?.Invoke(invitation);
    }

    private void OnOnlineUsersReceived(IReadOnlyList<SyncPlayOnlineUserDto> users)
    {
        _onlineUsers = users.ToList();
        OnlineUsersUpdated?.Invoke();
    }

    private void OnInviteLinkReceived(SyncPlayInviteLinkDto link)
    {
        InviteLinkReceived?.Invoke(link);
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        if (state == HubConnectionState.Connected)
        {
            _ = _hubClient.SyncPlaySetEnabledAsync(IsEnabled);

            if (CurrentGroup is { } group)
            {
                _ = _hubClient.JoinSyncPlayGroupAsync(group.GroupId);
            }
        }
    }

    public void Dispose()
    {
        _hubClient.SyncPlayGroupUpdated -= OnGroupUpdated;
        _hubClient.SyncPlayCommandReceived -= OnCommandReceived;
        _hubClient.SyncPlayPlayAtReceived -= OnPlayAtReceived;
        _hubClient.SyncPlaySeekCorrectionReceived -= OnSeekCorrectionReceived;
        _hubClient.SyncPlayChatMessageReceived -= OnChatMessageReceived;
        _hubClient.SyncPlayReactionReceived -= OnReactionReceived;
        _hubClient.SyncPlayErrorReceived -= OnErrorReceived;
        _hubClient.SyncPlayInvitationReceived -= OnInvitationReceived;
        _hubClient.SyncPlayOnlineUsersReceived -= OnOnlineUsersReceived;
        _hubClient.SyncPlayInviteLinkReceived -= OnInviteLinkReceived;
        _hubClient.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
