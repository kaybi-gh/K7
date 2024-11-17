using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;

public record RefreshMediaMetadatasCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required string MetadataProviderExternalId { get; init; }
    public required string Language { get; init; }
    public required string FallbackLanguage { get; init; }
}

public class RefreshMediaMetadatasCommandHandler : IRequestHandler<RefreshMediaMetadatasCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly TMDbMetadataProvider _metadataProvider;

    public RefreshMediaMetadatasCommandHandler(IApplicationDbContext context, ISender sender, TMDbMetadataProvider metadataProvider)
    {
        _context = context;
        _sender = sender;
        _metadataProvider = metadataProvider;
    }

    public async Task Handle(RefreshMediaMetadatasCommand request, CancellationToken cancellationToken)
    {
        var media = await _context.Medias
            .FindAsync([request.MediaId], cancellationToken);
        Guard.Against.NotFound(request.MediaId, media);

        var metadata = media.Type switch
        {
            MediaType.Movie => await _metadataProvider.FetchMovieMetadata(media.Id,
                request.MetadataProviderExternalId,
                request.Language,
                cancellationToken),
            _ => throw new NotImplementedException()
        };
        
        if (metadata != null)
        {
            foreach (var personRole in metadata.PersonRoles)
            {
                var existingPerson = await _context.Persons
                    .Include(p => p.Roles)
                    .FirstOrDefaultAsync(p => p.Name == personRole.Person.Name
                        && p.Birthday == personRole.Person.Birthday, cancellationToken);

                if (existingPerson == null)
                {
                    foreach (var externalId in personRole.Person.ExternalIds)
                    {
                        existingPerson = await _context.Persons
                            .Include(p => p.Roles)
                            .FirstOrDefaultAsync(p => p.ExternalIds.Any(x => x.Platform == externalId.Platform
                                && x.Value == externalId.Value), cancellationToken);

                        if (existingPerson != null)
                        {
                            break;
                        }
                    }
                }

                if (existingPerson != null)
                {
                    personRole.Person = existingPerson;
                }
            }

            await _context.MediaMetadatas.AddAsync(metadata, cancellationToken);
            media.Metadata = metadata;
            await _context.SaveChangesAsync(cancellationToken);

            // TODO - Remove the extra method FetchMetadataPictures
            var mediaPictures = await _metadataProvider.FetchMetadataPictures(metadata.Id, request.MetadataProviderExternalId, "fr", cancellationToken, fallbackLanguage: "en");
            media.Metadata.Pictures = mediaPictures;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
