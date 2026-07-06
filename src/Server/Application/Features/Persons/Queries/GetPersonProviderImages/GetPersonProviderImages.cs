using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Persons.Queries.GetPersonProviderImages;

public record GetPersonProviderImagesQuery(Guid PersonId) : IRequest<IReadOnlyList<ProviderImageDto>>;

public class GetPersonProviderImagesQueryHandler(
    IApplicationDbContext context,
    IEnumerable<IPersonImageProvider> imageProviders)
    : IRequestHandler<GetPersonProviderImagesQuery, IReadOnlyList<ProviderImageDto>>
{
    public async Task<IReadOnlyList<ProviderImageDto>> Handle(GetPersonProviderImagesQuery request, CancellationToken cancellationToken)
    {
        var person = await context.Persons
            .AsNoTracking()
            .Include(p => p.ExternalIds)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        var results = new List<ProviderImageDto>();

        foreach (var provider in imageProviders)
        {
            var externalId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == provider.ProviderName);
            if (externalId is null)
                continue;

            var images = await provider.GetPersonImagesAsync(externalId.Value, "en", cancellationToken);
            results.AddRange(images);
        }

        return MetadataImageUrlHelper.FilterProviderImages(results);
    }
}
