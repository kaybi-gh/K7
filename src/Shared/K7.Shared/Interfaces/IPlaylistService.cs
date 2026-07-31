using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IPlaylistService
{
    Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, LibraryItemOrderingOption? orderBy = null, CancellationToken cancellationToken = default);
    Task<PlaylistDto?> GetPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<PlaylistItemDto>?> GetPlaylistItemsAsync(Guid playlistId, int pageNumber = 1, int pageSize = 50, bool includeUnavailable = false, CancellationToken cancellationToken = default);
    Task<Guid> CreatePlaylistAsync(CreatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task DeletePlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> UploadPlaylistCoverAsync(Guid playlistId, Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<Guid> SetPlaylistCoverFromPictureAsync(Guid playlistId, Guid sourcePictureId, CancellationToken cancellationToken = default);
    Task RemovePlaylistCoverAsync(Guid playlistId, CancellationToken cancellationToken = default);
    Task<Guid> AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default);
    Task RemovePlaylistItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken = default);
    Task RecordPlaylistPlaybackAsync(Guid playlistId, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteDynamicPlaylistDto>?> GetDynamicPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<DynamicPlaylistDto?> GetDynamicPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateDynamicPlaylistAsync(CreateDynamicPlaylistRequest request, CancellationToken cancellationToken = default);
    Task UpdateDynamicPlaylistAsync(Guid id, UpdateDynamicPlaylistRequest request, CancellationToken cancellationToken = default);
    Task DeleteDynamicPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
    Task EvaluateDynamicPlaylistAsync(Guid id, CancellationToken cancellationToken = default);
}
