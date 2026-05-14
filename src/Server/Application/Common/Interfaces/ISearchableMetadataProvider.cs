using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.Common.Interfaces;

public interface ISearchableMetadataProvider
{
    string ProviderName { get; }
    Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year, string? providerId, MediaType? mediaType, string language, CancellationToken cancellationToken);
}