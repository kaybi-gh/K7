using K7.Server.Application.Common.Interfaces;
using MediatR;
using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.Features.Metadata.Queries.SearchMetadata;

public class SearchMetadataQuery : IRequest<IEnumerable<MetadataSearchResult>>
{
    public required string Query { get; init; }
    public int? Year { get; init; }
    public string? ProviderId { get; init; }
}

public class SearchMetadataQueryHandler(IEnumerable<ISearchableMetadataProvider> metadataProviders)
    : IRequestHandler<SearchMetadataQuery, IEnumerable<MetadataSearchResult>>
{
    public async Task<IEnumerable<MetadataSearchResult>> Handle(SearchMetadataQuery request, CancellationToken cancellationToken)
    {
        var tasks = metadataProviders.Select(provider => provider.SearchMetadataAsync(request.Query, request.Year, request.ProviderId, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r);
    }
}