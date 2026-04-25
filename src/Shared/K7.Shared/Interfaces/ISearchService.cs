using K7.Shared.Dtos.Search;

namespace K7.Shared.Interfaces;

public interface ISearchService
{
    Task<GlobalSearchResultDto?> GlobalSearchAsync(string q, int pageSize = 10, CancellationToken cancellationToken = default);
}
