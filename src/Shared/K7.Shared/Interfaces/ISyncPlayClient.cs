using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface ISyncPlayClient
{
    Task ReceiveSyncPlayGroupUpdated(SyncPlayGroupDto group);
    Task ReceiveSyncPlayCommand(SyncPlayCommandDto command);
    Task ReceiveSyncPlayPlayAt(long serverTimestampMs, double position);
    Task ReceiveSyncPlaySeekCorrection(double position);
    Task ReceiveSyncPlayChatMessage(SyncPlayChatMessageDto message);
    Task ReceiveSyncPlayReaction(SyncPlayReactionDto reaction);
    Task ReceiveSyncPlayError(string errorCode);
    Task ReceiveSyncPlayInvitation(SyncPlayInvitationDto invitation);
    Task ReceiveSyncPlayOnlineUsers(IReadOnlyList<SyncPlayOnlineUserDto> users);
    Task ReceiveSyncPlayInviteLink(SyncPlayInviteLinkDto link);
}
