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

        IEnumerable<MetadataProviderInfoDto> result = providers
            .DistinctBy(p => p.ProviderName)
            .Select(p => new MetadataProviderInfoDto
            {
                ProviderName = p.ProviderName,
                SupportedMediaTypes = p.SupportedMediaTypes
            })
            .OrderBy(p => p.ProviderName);

        return Task.FromResult(result);
    }
}
