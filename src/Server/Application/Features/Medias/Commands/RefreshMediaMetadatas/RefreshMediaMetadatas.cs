using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
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
    private readonly IMusicArtistMetadataProvider _artistMetadataProvider;

    public RefreshMediaMetadatasCommandHandler(
        IApplicationDbContext context,
        IMetadataProvider<ExternalMovieMetadata> movieMetadataProvider,
        IMetadataProvider<ExternalMusicAlbumMetadata> musicMetadataProvider,
        IMusicArtistMetadataProvider artistMetadataProvider)
    {
        _context = context;
        _movieMetadataProvider = movieMetadataProvider;
        _musicMetadataProvider = musicMetadataProvider;
        _artistMetadataProvider = artistMetadataProvider;
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

            movie.ApplyMetadata(metadata);
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

        foreach (var artistMeta in metadata.Artists)
        {
            var person = persons.FirstOrDefault(p =>
                string.Equals(p.Name, artistMeta.Name, StringComparison.OrdinalIgnoreCase));

            if (person == null) continue;

            var hasMbId = person.ExternalIds.Any(e => e.Platform == "musicbrainz-artist");
            if (!hasMbId)
            {
                person.ExternalIds.Add(new ExternalId
                {
                    Platform = "musicbrainz-artist",
                    Value = artistMeta.MusicBrainzArtistId,
                    PersonId = person.Id
                });
            }

            if (person.PortraitPicture == null || string.IsNullOrEmpty(person.Biography))
            {
                var details = await _artistMetadataProvider.FetchArtistDetailsAsync(
                    artistMeta.MusicBrainzArtistId, language, cancellationToken);

                if (details != null)
                {
                    if (string.IsNullOrEmpty(person.Biography) && !string.IsNullOrEmpty(details.Biography))
                        person.Biography = details.Biography;

                    if (!string.IsNullOrEmpty(details.Country) && string.IsNullOrEmpty(person.BirthPlace))
                        person.BirthPlace = details.Country;

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
