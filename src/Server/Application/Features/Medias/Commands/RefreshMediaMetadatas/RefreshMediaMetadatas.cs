using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;

public record RefreshMediaMetadatasCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required string MetadataProviderExternalId { get; init; }
    public required string MetadataProviderName { get; init; }
    public required string Language { get; init; }
    public required string FallbackLanguage { get; init; }
}

public class RefreshMediaMetadatasCommandHandler : IRequestHandler<RefreshMediaMetadatasCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, IMusicArtistMetadataProvider> _artistProviders;

    public RefreshMediaMetadatasCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        IEnumerable<IMusicArtistMetadataProvider> artistMetadataProviders)
    {
        _context = context;
        _serviceProvider = serviceProvider;
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
            Serie serie => HandleSerieAsync(request, serie, cancellationToken),
            _ => throw new NotImplementedException()
        };

        await metadataUpdate;
        media.LastMetadataRefreshedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleMovieAsync(RefreshMediaMetadatasCommand request, Movie movie, CancellationToken cancellationToken = default)
    {
        var provider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(request.MetadataProviderName);
        var metadata = await provider.FetchMetadata(request.MetadataProviderExternalId,
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
        var provider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(request.MetadataProviderName);
        var metadata = await provider.FetchMetadata(
            request.MetadataProviderExternalId, request.Language, cancellationToken);

        if (metadata != null)
        {
            await _context.Entry(album).Collection(a => a.Tracks).Query()
                .Include(t => t.ExternalIds)
                .LoadAsync(cancellationToken);

            album.ApplyMetadata(metadata);
            await EnrichArtistsAsync(album, metadata, request.Language, cancellationToken);
            await PersistTrackExternalIdsAsync(album, metadata, cancellationToken);
        }
    }

    private async Task HandleSerieAsync(RefreshMediaMetadatasCommand request, Serie serie, CancellationToken cancellationToken)
    {
        // Load serie-specific includes
        await _context.Entry(serie).Collection(s => s.Seasons).Query()
            .Include(s => s.Pictures)
            .Include(s => s.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.Pictures)
            .LoadAsync(cancellationToken);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(request.MetadataProviderName);

        var serieMetadata = await metadataProvider.FetchSerieMetadataAsync(
            request.MetadataProviderExternalId, request.Language, cancellationToken);
        serie.ApplyMetadata(serieMetadata);

        // Person dedup
        if (serieMetadata.PersonRoles?.Count > 0)
        {
            serie.PersonRoles.Clear();
            foreach (var role in serieMetadata.PersonRoles)
            {
                Person? existingPerson = null;
                foreach (var externalId in role.Person.ExternalIds)
                {
                    existingPerson = await _context.Persons
                        .Include(p => p.ExternalIds)
                        .FirstOrDefaultAsync(p => p.ExternalIds.Any(e =>
                            e.ProviderName == externalId.ProviderName && e.Value == externalId.Value),
                            cancellationToken);
                    if (existingPerson is not null)
                        break;
                }

                if (existingPerson is not null)
                    role.Person = existingPerson;

                serie.PersonRoles.Add(role);
            }
        }

        // Ratings
        foreach (var rating in serieMetadata.Ratings)
        {
            var existing = serie.Ratings.OfType<MetadataProviderRating>()
                .FirstOrDefault(r => r.MetadataProvider == rating.MetadataProvider);
            if (existing is not null)
            {
                existing.Value = rating.Value;
                existing.RatingCount = rating.RatingCount;
            }
            else
            {
                serie.Ratings.Add(rating);
            }
        }

        // Fetch and apply season metadata
        foreach (var season in serie.Seasons)
        {
            var seasonMetadata = await metadataProvider.FetchSeasonMetadataAsync(
                request.MetadataProviderExternalId, season.SeasonNumber, request.Language, cancellationToken);
            season.ApplyMetadata(seasonMetadata);
        }

        // Fetch and apply episode metadata
        foreach (var season in serie.Seasons)
        {
            foreach (var episode in season.Episodes)
            {
                var episodeMetadata = await metadataProvider.FetchEpisodeMetadataAsync(
                    request.MetadataProviderExternalId, season.SeasonNumber, episode.EpisodeNumber,
                    request.Language, cancellationToken);

                episode.ApplyMetadata(episodeMetadata);

                if (!string.IsNullOrEmpty(episodeMetadata.StillImageUrl))
                {
                    var stillPicture = new MetadataPicture
                    {
                        OriginalRemoteUri = new Uri(episodeMetadata.StillImageUrl),
                        Type = MetadataPictureType.Still
                    };
                    stillPicture.AddDomainEvent(new MetadataPictureCreatedEvent(stillPicture));

                    episode.Pictures.Clear();
                    episode.Pictures.Add(stillPicture);
                }
            }
        }
    }

    private async Task PersistTrackExternalIdsAsync(MusicAlbum album, ExternalMusicAlbumMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata.Tracks is not { Count: > 0 }) return;

        var trackIds = album.Tracks.Select(t => t.Id).ToList();
        var existingExternalIds = await _context.ExternalIds
            .Where(e => e.MediaId.HasValue && trackIds.Contains(e.MediaId.Value))
            .ToListAsync(cancellationToken);

        foreach (var track in album.Tracks)
        {
            var metadataTrack = metadata.Tracks.FirstOrDefault(mt =>
                string.Equals(mt.Title, track.Title, StringComparison.OrdinalIgnoreCase)
                || mt.TrackNumber == track.TrackNumber);

            if (metadataTrack is null) continue;

            if (!string.IsNullOrEmpty(metadataTrack.MusicBrainzRecordingId)
                && !existingExternalIds.Any(e => e.MediaId == track.Id && e.ProviderName == "musicbrainz"))
            {
                track.ExternalIds.Add(new ExternalId
                {
                    ProviderName = "musicbrainz",
                    Value = metadataTrack.MusicBrainzRecordingId
                });
            }

            if (!string.IsNullOrEmpty(metadataTrack.Isrc)
                && !existingExternalIds.Any(e => e.MediaId == track.Id && e.ProviderName == "isrc"))
            {
                track.ExternalIds.Add(new ExternalId
                {
                    ProviderName = "isrc",
                    Value = metadataTrack.Isrc
                });
            }
        }
    }

    private async Task EnrichArtistsAsync(MusicAlbum album, ExternalMusicAlbumMetadata metadata, string language, CancellationToken cancellationToken)
    {
        if (metadata.Artists is not { Count: > 0 }) return;
        if (album.ArtistId is null) return;

        var artist = await _context.Medias.OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .Include(a => a.Pictures)
            .FirstOrDefaultAsync(a => a.Id == album.ArtistId, cancellationToken);

        if (artist is null) return;

        var artistMetadata = metadata.Artists.FirstOrDefault(a =>
            string.Equals(a.Name, artist.Title, StringComparison.OrdinalIgnoreCase));

        if (artistMetadata is null) return;

        var mbExternalId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "musicbrainz");
        if (mbExternalId is null && !string.IsNullOrEmpty(artistMetadata.MusicBrainzArtistId))
        {
            mbExternalId = new ExternalId
            {
                ProviderName = "musicbrainz",
                Value = artistMetadata.MusicBrainzArtistId,
                MediaId = artist.Id
            };
            artist.ExternalIds.Add(mbExternalId);
        }

        if (artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(artist.Biography)) return;

        var wikidataId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;

        if (wikidataId is null && _artistProviders.TryGetValue("musicbrainz", out var mbProvider))
        {
            var mbId = mbExternalId?.Value;
            var mbDetails = !string.IsNullOrEmpty(mbId)
                ? await mbProvider.FetchByProviderIdAsync(mbId, language, cancellationToken)
                : await mbProvider.SearchByNameAsync(artist.Title!, language, cancellationToken);

            if (mbDetails is not null)
            {
                if (!string.IsNullOrEmpty(mbDetails.MusicBrainzArtistId) && !artist.ExternalIds.Any(e => e.ProviderName == "musicbrainz"))
                    artist.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = mbDetails.MusicBrainzArtistId, MediaId = artist.Id });

                if (!string.IsNullOrEmpty(mbDetails.Country) && string.IsNullOrEmpty(artist.Country))
                    artist.Country = mbDetails.Country;

                wikidataId = mbDetails.WikidataId;
                if (!string.IsNullOrEmpty(wikidataId) && !artist.ExternalIds.Any(e => e.ProviderName == "wikidata"))
                    artist.ExternalIds.Add(new ExternalId { ProviderName = "wikidata", Value = wikidataId, MediaId = artist.Id });
            }
        }

        if (!string.IsNullOrEmpty(wikidataId) && _artistProviders.TryGetValue("wikidata", out var wdProvider))
        {
            var details = await wdProvider.FetchByProviderIdAsync(wikidataId, language, cancellationToken);
            if (details is not null)
            {
                if (string.IsNullOrEmpty(artist.Biography) && !string.IsNullOrEmpty(details.Biography))
                    artist.Biography = details.Biography;

                if (!artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(details.ImageUrl))
                {
                    var picture = new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        OriginalRemoteUri = new Uri(details.ImageUrl),
                        MediaId = artist.Id
                    };
                    picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
                    artist.Pictures.Add(picture);
                }
            }
        }
    }
}
