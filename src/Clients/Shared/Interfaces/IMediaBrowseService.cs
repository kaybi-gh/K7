using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IMediaBrowseService
{
    Task<IReadOnlyList<MediaBrowseItem>> GetRootItemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaBrowseItem>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioQueueItem>> GetPlayableItemsAsync(string parentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaBrowseItem>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
