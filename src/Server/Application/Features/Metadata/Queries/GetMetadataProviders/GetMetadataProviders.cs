using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Metadata.Queries.GetMetadataProviders;

public record GetMetadataProvidersQuery : IRequest<IEnumerable<MetadataProviderInfoDto>>
{
    public LibraryMediaType? MediaType { get; init; }
}

public class GetMetadataProvidersQueryHandler : IRequestHandler<GetMetadataProvidersQuery, IEnumerable<MetadataProviderInfoDto>>
{
    private readonly IEnumerable<IMetadataProviderInfo> _providers;

    public GetMetadataProvidersQueryHandler(IEnumerable<IMetadataProviderInfo> providers)
    {
        _providers = providers;
    }

    public Task<IEnumerable<MetadataProviderInfoDto>> Handle(GetMetadataProvidersQuery request, CancellationToken cancellationToken)
    {
        var providers = _providers.AsEnumerable();

        // Exclude internal-only providers not selectable by users
        providers = providers.Where(p => p.ProviderName != "federation");

        if (request.MediaType.HasValue)
        {
            providers = providers.Where(p => p.SupportedMediaTypes.Contains(request.MediaType.Value));
        }

        var result = providers
            .DistinctBy(p => p.ProviderName)
            .Select(p => new MetadataProviderInfoDto
            {
                ProviderName = p.ProviderName,
                SupportedMediaTypes = p.SupportedMediaTypes
            })
            .ToList();

        if (request.MediaType is null or LibraryMediaType.Serie)
        {
            result.Insert(0, new MetadataProviderInfoDto
            {
                ProviderName = "auto",
                SupportedMediaTypes = [LibraryMediaType.Serie]
            });
        }

        IEnumerable<MetadataProviderInfoDto> ordered = result
            .OrderBy(p => request.MediaType == LibraryMediaType.Serie && p.ProviderName == "auto" ? 0
                : request.MediaType == LibraryMediaType.Serie && p.ProviderName == "tvdb" ? 1
                : 2)
            .ThenBy(p => p.ProviderName);

        return Task.FromResult(ordered);
    }
}
