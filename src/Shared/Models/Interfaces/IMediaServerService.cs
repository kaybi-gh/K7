using MediaClient.Shared.Domain.Models;

namespace MediaClient.Shared.Domain.Interfaces;

public interface IMediaServerService
{
    string GetBaseUrl();
    Task<MediaDto?> GetMediaAsync(Guid id);
    Task<PaginatedList<MediaDto>?> GetMediasAsync(GetMediasWithPaginationQuery query);
}
