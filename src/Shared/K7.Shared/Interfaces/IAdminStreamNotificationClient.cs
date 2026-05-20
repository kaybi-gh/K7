using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IAdminStreamNotificationClient
{
    Task ReceiveActiveStreamsUpdated(IReadOnlyList<ActiveStreamDto> streams);
}
