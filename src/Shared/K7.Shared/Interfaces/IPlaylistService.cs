using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IPlaylistService
{
    Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PlaylistDto?> GetPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<PlaylistItemDto>?> GetPlaylistItemsAsync(Guid playlistId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> CreatePlaylistAsync(CreatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task DeletePlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default);
    Task RemovePlaylistItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteSmartPlaylistDto>?> GetSmartPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<SmartPlaylistDto?> GetSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateSmartPlaylistAsync(CreateSmartPlaylistRequest request, CancellationToken cancellationToken = default);
    Task UpdateSmartPlaylistAsync(Guid id, UpdateSmartPlaylistRequest request, CancellationToken cancellationToken = default);
    Task DeleteSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task EvaluateSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
}
