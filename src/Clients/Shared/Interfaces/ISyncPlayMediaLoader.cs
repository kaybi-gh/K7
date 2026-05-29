using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface ISyncPlayMediaLoader
{
    Task LoadAndPlayMediaAsync(Guid mediaReferenceId, string? title, string? coverUrl);
    Task LoadQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> queue, int currentIndex);
}
