using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Services;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Commands.RefreshMediaMetadatas;

public record RefreshMediaMetadatasCommand : IRequest
{
    public required int MediaId { get; init; }
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
            media.Metadata = metadata;
            await _context.SaveChangesAsync(cancellationToken);

            if (media.Metadata.ExternalIds != null)
            {
                foreach (var externalId in media.Metadata.ExternalIds)
                {
                    externalId.MetadataId = media.Metadata.Id;
                }
            }
            var mediaPictures = await _metadataProvider.FetchMediaPictures(metadata.Id, request.MetadataProviderExternalId, "fr", cancellationToken, fallbackLanguage: "en");
            media.Metadata.Pictures = mediaPictures;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
