using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IAdminMetricsNotificationClient
{
    Task ReceiveServerMetricsUpdated(ServerMetricsSnapshotDto snapshot);
}
