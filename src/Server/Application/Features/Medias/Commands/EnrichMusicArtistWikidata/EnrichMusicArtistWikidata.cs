using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.Medias.Commands.EnrichMusicArtistWikidata;

public record EnrichMusicArtistWikidataCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required string Language { get; init; }
}

public class EnrichMusicArtistWikidataCommandHandler(
    IApplicationDbContext context,
    IEnumerable<IMusicArtistMetadataProvider> artistMetadataProviders)
    : IRequestHandler<EnrichMusicArtistWikidataCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IReadOnlyDictionary<string, IMusicArtistMetadataProvider> _artistProviders =
        artistMetadataProviders.ToDictionary(p => p.ProviderName);

    public async Task Handle(EnrichMusicArtistWikidataCommand request, CancellationToken cancellationToken)
    {
        var artist = await _context.Medias
            .OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .Include(a => a.Pictures)
            .FirstOrDefaultAsync(a => a.Id == request.MediaId, cancellationToken);
        Guard.Against.NotFound(request.MediaId, artist);

        var wikidataId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;
        if (string.IsNullOrEmpty(wikidataId) || !_artistProviders.TryGetValue("wikidata", out var wdProvider))
            return;

        var biographyLocked = artist.IsFieldLocked(nameof(MusicArtist.Biography));
        var posterLocked = artist.IsPictureTypeLocked(MetadataPictureType.Poster);
        if ((posterLocked || artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster))
            && (biographyLocked || !string.IsNullOrEmpty(artist.Biography)))
        {
            return;
        }

        var details = await wdProvider.FetchByProviderIdAsync(wikidataId, request.Language, cancellationToken);
        if (details is null)
            return;

        if (!biographyLocked && string.IsNullOrEmpty(artist.Biography) && !string.IsNullOrEmpty(details.Biography))
            artist.Biography = details.Biography;

        if (!posterLocked
            && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster)
            && MetadataImageUrlHelper.TryCreateRemoteUri(details.ImageUrl, out var wikidataImageUri))
        {
            var picture = new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                OriginalRemoteUri = wikidataImageUri,
                MediaId = artist.Id
            };
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            artist.Pictures.Add(picture);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
