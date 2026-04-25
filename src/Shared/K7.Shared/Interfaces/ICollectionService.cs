using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface ICollectionService
{
    Task<PaginatedListDto<LiteCollectionDto>?> GetCollectionsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, bool? isPublic = null, CancellationToken cancellationToken = default);
    Task<CollectionDto?> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<CollectionItemDto>?> GetCollectionItemsAsync(Guid collectionId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> CreateCollectionAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default);
    Task UpdateCollectionAsync(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> AddCollectionItemAsync(Guid collectionId, Guid mediaId, CancellationToken cancellationToken = default);
    Task RemoveCollectionItemAsync(Guid collectionId, Guid itemId, CancellationToken cancellationToken = default);
}
