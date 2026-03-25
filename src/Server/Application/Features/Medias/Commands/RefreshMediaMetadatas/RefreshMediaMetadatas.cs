using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;

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
    private readonly IMetadataProvider<ExternalMovieMetadata> _movieMetadataProvider;
    private readonly IMetadataProvider<ExternalMusicAlbumMetadata> _musicMetadataProvider;
    private readonly IReadOnlyDictionary<string, IMusicArtistMetadataProvider> _artistProviders;

    public RefreshMediaMetadatasCommandHandler(
        IApplicationDbContext context,
        IMetadataProvider<ExternalMovieMetadata> movieMetadataProvider,
        IMetadataProvider<ExternalMusicAlbumMetadata> musicMetadataProvider,
        IEnumerable<IMusicArtistMetadataProvider> artistMetadataProviders)
    {
        _context = context;
        _movieMetadataProvider = movieMetadataProvider;
        _musicMetadataProvider = musicMetadataProvider;
        _artistProviders = artistMetadataProviders.ToDictionary(p => p.ProviderName);
    }

    public async Task Handle(RefreshMediaMetadatasCommand request, CancellationToken cancellationToken)
    {
        var media = await _context.Medias
            .Include(m => m.ExternalIds)
            .Include(m => m.Pictures)
            .Include(m => m.PersonRoles)
                .ThenInclude(pr => pr.Person)
            .Include(m => m.PersonRoles)
                .ThenInclude(pr => pr.ExternalIds)
            .Include(m => m.PersonRoles)
                .ThenInclude(pr => pr.PortraitPicture)
            .Include(m => m.Ratings)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);
        Guard.Against.NotFound(request.MediaId, media);

        var metadataUpdate = media switch
        {
            Movie movie => HandleMovieAsync(request, movie, cancellationToken),
            MusicAlbum album => HandleMusicAlbumAsync(request, album, cancellationToken),
            _ => throw new NotImplementedException()
        };

        await metadataUpdate;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleMovieAsync(RefreshMediaMetadatasCommand request, Movie movie, CancellationToken cancellationToken = default)
    {
        var metadata = await _movieMetadataProvider.FetchMetadata(request.MetadataProviderExternalId,
                request.Language,
                cancellationToken);

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
                            .FirstOrDefaultAsync(p => p.ExternalIds.Any(x => x.ProviderName == externalId.ProviderName
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

            movie.ApplyMetadata(metadata);

            foreach (var rating in metadata.Ratings)
            {
                var existing = movie.Ratings.OfType<MetadataProviderRating>()
                    .FirstOrDefault(r => r.MetadataProvider == rating.MetadataProvider);
                if (existing is not null)
                {
                    existing.Value = rating.Value;
                    existing.RatingCount = rating.RatingCount;
                }
                else
                {
                    movie.Ratings.Add(rating);
                }
            }
        }
    }

    private async Task HandleMusicAlbumAsync(RefreshMediaMetadatasCommand request, MusicAlbum album, CancellationToken cancellationToken)
    {
        var metadata = await _musicMetadataProvider.FetchMetadata(
            request.MetadataProviderExternalId, request.Language, cancellationToken);

        if (metadata != null)
        {
            album.ApplyMetadata(metadata);
            await EnrichArtistsAsync(album, metadata, request.Language, cancellationToken);
        }
    }

    private async Task EnrichArtistsAsync(MusicAlbum album, ExternalMusicAlbumMetadata metadata, string language, CancellationToken cancellationToken)
    {
        if (metadata.Artists is not { Count: > 0 }) return;

        var personIds = album.PersonRoles.Select(pr => pr.PersonId).Distinct().ToList();
        var persons = await _context.Persons
            .Include(p => p.ExternalIds)
            .Include(p => p.PortraitPicture)
            .Where(p => personIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var artistMetadata in metadata.Artists)
        {
            var person = persons.FirstOrDefault(p =>
                string.Equals(p.Name, artistMetadata.Name, StringComparison.OrdinalIgnoreCase));

            if (person == null) continue;

            var mbExternalId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == "musicbrainz");
            if (mbExternalId == null && !string.IsNullOrEmpty(artistMetadata.MusicBrainzArtistId))
            {
                mbExternalId = new ExternalId
                {
                    ProviderName = "musicbrainz",
                    Value = artistMetadata.MusicBrainzArtistId,
                    PersonId = person.Id
                };
                person.ExternalIds.Add(mbExternalId);
            }

            if (person.PortraitPicture != null && !string.IsNullOrEmpty(person.Biography)) continue;

            var wikidataId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;

            if (wikidataId == null && _artistProviders.TryGetValue("musicbrainz", out var mbProvider))
            {
                var mbId = mbExternalId?.Value;
                var mbDetails = !string.IsNullOrEmpty(mbId)
                    ? await mbProvider.FetchByProviderIdAsync(mbId, language, cancellationToken)
                    : await mbProvider.SearchByNameAsync(person.Name, language, cancellationToken);

                if (mbDetails != null)
                {
                    if (!string.IsNullOrEmpty(mbDetails.MusicBrainzArtistId) && !person.ExternalIds.Any(e => e.ProviderName == "musicbrainz"))
                        person.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = mbDetails.MusicBrainzArtistId, PersonId = person.Id });

                    if (!string.IsNullOrEmpty(mbDetails.Country) && string.IsNullOrEmpty(person.BirthPlace))
                        person.BirthPlace = mbDetails.Country;

                    wikidataId = mbDetails.WikidataId;
                    if (!string.IsNullOrEmpty(wikidataId))
                        person.ExternalIds.Add(new ExternalId { ProviderName = "wikidata", Value = wikidataId, PersonId = person.Id });
                }
            }

            if (!string.IsNullOrEmpty(wikidataId) && _artistProviders.TryGetValue("wikidata", out var wdProvider))
            {
                var details = await wdProvider.FetchByProviderIdAsync(wikidataId, language, cancellationToken);
                if (details != null)
                {
                    if (string.IsNullOrEmpty(person.Biography) && !string.IsNullOrEmpty(details.Biography))
                        person.Biography = details.Biography;

                    if (person.PortraitPicture == null && !string.IsNullOrEmpty(details.ImageUrl))
                    {
                        var picture = new MetadataPicture
                        {
                            Type = MetadataPictureType.Portrait,
                            OriginalRemoteUri = new Uri(details.ImageUrl),
                            PersonId = person.Id
                        };
                        picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
                        person.PortraitPicture = picture;
                    }
                }
            }
        }
    }
}
