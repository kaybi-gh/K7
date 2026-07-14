using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Interfaces;

public interface IHomeRecommendationService
{
    Task<IReadOnlyList<Guid>> GetRecommendedMediaIdsAsync(
        Guid userId,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
