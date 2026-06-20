using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IAdminPresenceNotificationClient
{
    Task ReceiveOnlineUsersPresenceUpdated(OnlineUsersPresenceDto presence);
}
