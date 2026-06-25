using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
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
    private readonly ISender _sender;
    private readonly IReadOnlyDictionary<string, IMusicArtistMetadataProvider> _artistProviders;
    private readonly IMediaMetadataTagSyncService _metadataTagSyncService;

    public RefreshMediaMetadatasCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        ISender sender,
        IEnumerable<IMusicArtistMetadataProvider> artistMetadataProviders,
        IMediaMetadataTagSyncService metadataTagSyncService)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _sender = sender;
        _artistProviders = artistMetadataProviders.ToDictionary(p => p.ProviderName);
        _metadataTagSyncService = metadataTagSyncService;
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
            .Include(m => m.MetadataTags)
                .ThenInclude(mt => mt.MetadataTag)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);
        Guard.Against.NotFound(request.MediaId, media);

        var metadataUpdate = media switch
        {
            Movie movie => HandleMovieAsync(request, movie, cancellationToken),
            MusicAlbum album => HandleMusicAlbumAsync(request, album, cancellationToken),
            MusicArtist artist => HandleMusicArtistAsync(request, artist, cancellationToken),
            Serie serie => HandleSerieAsync(request, serie, cancellationToken),
            _ => throw new NotImplementedException()
        };

        await metadataUpdate;

        var isFirstRefresh = media.LastMetadataRefreshedAt is null;
        media.LastMetadataRefreshedAt = DateTimeOffset.UtcNow;

        media.AddDomainEvent(new MediaMetadataRefreshedEvent(media));

        if (isFirstRefresh)
        {
            media.AddDomainEvent(new MediaAddedEvent(media));
        }

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

                if (existingPerson is null)
                {
                    foreach (var externalId in personRole.Person.ExternalIds)
                    {
                        existingPerson = await _context.Persons
                            .Include(p => p.Roles)
                            .FirstOrDefaultAsync(p => p.ExternalIds.Any(x => x.ProviderName == externalId.ProviderName
                                && x.Value == externalId.Value), cancellationToken);

                        if (existingPerson is not null)
                        {
                            break;
                        }
                    }
                }

                // Fallback: match by name only (e.g. Person created by music metadata without birthday)
                existingPerson ??= await _context.Persons
                    .Include(p => p.Roles)
                    .FirstOrDefaultAsync(p => p.Name == personRole.Person.Name, cancellationToken);

                if (existingPerson is not null)
                {
                    personRole.Person = existingPerson;
                }
            }

            movie.ApplyMetadata(metadata);
            await _metadataTagSyncService.ApplyTagsAsync(
                movie,
                MetadataTagBuilder.FromMovieMetadata(metadata, movie),
                cancellationToken);

            if (metadata.RecommendedExternalIds.Count > 0)
            {
                var existing = await _context.MediaRecommendations
                    .FirstOrDefaultAsync(r => r.MediaId == movie.Id && r.ProviderName == provider.ProviderName, cancellationToken);

                if (existing is not null)
                {
                    existing.RecommendedIds = [.. metadata.RecommendedExternalIds];
                }
                else
                {
                    _context.MediaRecommendations.Add(new MediaRecommendation
                    {
                        MediaId = movie.Id,
                        ProviderName = provider.ProviderName,
                        RecommendedIds = [.. metadata.RecommendedExternalIds]
                    });
                }
            }

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
                .Include(t => t.ArtistCredits)
                .LoadAsync(cancellationToken);

            album.ApplyMetadata(metadata);
            await _metadataTagSyncService.ApplyTagsAsync(
                album,
                MetadataTagBuilder.FromMusicAlbumMetadata(metadata, album),
                cancellationToken);

            // Federation: create tracks and artist from peer metadata (no local scan to do it)
            if (request.MetadataProviderName == "federation")
            {
                if (album.Tracks.Count == 0 && metadata.Tracks is { Count: > 0 })
                {
                    foreach (var trackMeta in metadata.Tracks)
                    {
                        var track = new MusicTrack
                        {
                            AlbumId = album.Id,
                            Title = trackMeta.Title,
                            TrackNumber = trackMeta.TrackNumber,
                            DiscNumber = trackMeta.DiscNumber,
                        };
                        _context.Medias.Add(track);
                        album.Tracks.Add(track);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // Re-parent RemoteIndexedFiles from album to individual tracks
                if (album.Tracks.Count > 0 && metadata.Tracks is { Count: > 0 })
                {
                    var albumRemoteFiles = await _context.RemoteIndexedFiles
                        .Where(r => r.MediaId == album.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var trackMeta in metadata.Tracks)
                    {
                        if (trackMeta.RemoteId is null) continue;

                        var localTrack = album.Tracks.FirstOrDefault(t =>
                            t.TrackNumber == trackMeta.TrackNumber
                            || string.Equals(t.Title, trackMeta.Title, StringComparison.OrdinalIgnoreCase));

                        if (localTrack is null) continue;

                        var remoteFile = albumRemoteFiles.FirstOrDefault(r => r.RemoteMediaId == trackMeta.RemoteId.Value);
                        if (remoteFile is not null)
                        {
                            remoteFile.MediaId = localTrack.Id;
                        }
                    }
                }

                if (album.ArtistId is null && metadata.Artists is { Count: > 0 })
                {
                    var primaryArtist = metadata.Artists[0];
                    var artist = await FindOrCreateMusicArtistAsync(primaryArtist.Name, primaryArtist.MusicBrainzArtistId, cancellationToken);
                    album.ArtistId = artist.Id;
                }
            }

            await EnrichArtistsAsync(album, metadata, request.Language, cancellationToken);
            await PersistTrackExternalIdsAsync(album, metadata, cancellationToken);
            await SyncTrackArtistCreditsAsync(album, metadata, cancellationToken);
        }
    }

    private async Task HandleMusicArtistAsync(RefreshMediaMetadatasCommand request, MusicArtist artist, CancellationToken cancellationToken)
    {
        var language = request.Language;

        if (_artistProviders.TryGetValue("musicbrainz", out var mbProvider))
        {
            var mbDetails = await mbProvider.FetchByProviderIdAsync(
                request.MetadataProviderExternalId, language, cancellationToken);

            if (mbDetails is not null)
            {
                if (!artist.IsFieldLocked(nameof(MusicArtist.Country)) && !string.IsNullOrEmpty(mbDetails.Country))
                    artist.Country = mbDetails.Country;

                if (!artist.IsFieldLocked(nameof(MusicArtist.ExternalIds)))
                {
                    if (!string.IsNullOrEmpty(mbDetails.WikidataId) && !artist.ExternalIds.Any(e => e.ProviderName == "wikidata"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "wikidata", Value = mbDetails.WikidataId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.SpotifyId) && !artist.ExternalIds.Any(e => e.ProviderName == "spotify"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "spotify", Value = mbDetails.SpotifyId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.ImdbId) && !artist.ExternalIds.Any(e => e.ProviderName == "imdb"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = mbDetails.ImdbId, MediaId = artist.Id });
                }

                await SyncArtistMembersAsync(artist, mbDetails.Members, request.Language, cancellationToken);

                // Poster from MusicBrainz cover art
                if (!artist.IsFieldLocked(nameof(MusicArtist.Pictures))
                    && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(mbDetails.ImageUrl))
                {
                    var picture = new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        OriginalRemoteUri = new Uri(mbDetails.ImageUrl),
                        MediaId = artist.Id
                    };
                    picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
                    artist.Pictures.Add(picture);
                }
            }
        }

        // Bio/image from Wikidata
        var wikidataId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;

        if (!string.IsNullOrEmpty(wikidataId) && _artistProviders.TryGetValue("wikidata", out var wdProvider))
        {
            var details = await wdProvider.FetchByProviderIdAsync(wikidataId, language, cancellationToken);
            if (details is not null)
            {
                if (!artist.IsFieldLocked(nameof(MusicArtist.Biography)) && !string.IsNullOrEmpty(details.Biography))
                    artist.Biography = details.Biography;

                if (!artist.IsFieldLocked(nameof(MusicArtist.Pictures))
                    && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(details.ImageUrl))
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

    private async Task HandleSerieAsync(RefreshMediaMetadatasCommand request, Serie serie, CancellationToken cancellationToken)
    {
        // Load serie-specific includes
        await _context.Entry(serie).Collection(s => s.Seasons).Query()
            .Include(s => s.Pictures)
            .Include(s => s.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.Pictures)
            .Include(s => s.Episodes).ThenInclude(e => e.PersonRoles)
            .LoadAsync(cancellationToken);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(request.MetadataProviderName);

        var serieMetadata = await metadataProvider.FetchSerieMetadataAsync(
            request.MetadataProviderExternalId, request.Language, cancellationToken);
        serie.ApplyMetadata(serieMetadata);
        await _metadataTagSyncService.ApplyTagsAsync(
            serie,
            MetadataTagBuilder.FromSerieMetadata(serieMetadata, serie),
            cancellationToken);

        if (serieMetadata.RecommendedExternalIds.Count > 0)
        {
            var existing = await _context.MediaRecommendations
                .FirstOrDefaultAsync(r => r.MediaId == serie.Id && r.ProviderName == metadataProvider.ProviderName, cancellationToken);

            if (existing is not null)
            {
                existing.RecommendedIds = [.. serieMetadata.RecommendedExternalIds];
            }
            else
            {
                _context.MediaRecommendations.Add(new MediaRecommendation
                {
                    MediaId = serie.Id,
                    ProviderName = metadataProvider.ProviderName,
                    RecommendedIds = [.. serieMetadata.RecommendedExternalIds]
                });
            }
        }

        // Person dedup
        if (!serie.IsFieldLocked(nameof(Serie.PersonRoles)) && serieMetadata.PersonRoles?.Count > 0)
        {
            await ResolvePersonReferencesAsync(serieMetadata.PersonRoles, cancellationToken);

            serie.PersonRoles.Clear();
            foreach (var role in serieMetadata.PersonRoles)
                serie.PersonRoles.Add(role);
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

        // Federation: create seasons and episodes from peer metadata (no local scan to do it)
        if (request.MetadataProviderName == "federation")
        {
            if (serie.Seasons.Count == 0 && serieMetadata.TotalSeasons > 0)
            {
                for (var i = 1; i <= serieMetadata.TotalSeasons; i++)
                {
                    serie.Seasons.Add(new SerieSeason { SerieId = serie.Id, SeasonNumber = i });
                }
            }
        }

        // Fetch and apply season metadata
        foreach (var season in serie.Seasons)
        {
            var seasonMetadata = await metadataProvider.FetchSeasonMetadataAsync(
                request.MetadataProviderExternalId, season.SeasonNumber, request.Language, cancellationToken);
            season.ApplyMetadata(seasonMetadata);

            if (request.MetadataProviderName == "federation")
            {
                if (season.Episodes.Count == 0 && seasonMetadata.EpisodeCount > 0)
                {
                    for (var i = 1; i <= seasonMetadata.EpisodeCount; i++)
                    {
                        season.Episodes.Add(new SerieEpisode { SerieId = serie.Id, EpisodeNumber = i });
                    }
                }
            }
        }

        // Fetch and apply episode metadata
        var episodeRemoteIds = new Dictionary<(int Season, int Episode), Guid>();
        foreach (var season in serie.Seasons)
        {
            foreach (var episode in season.Episodes)
            {
                var episodeMetadata = await metadataProvider.FetchEpisodeMetadataAsync(
                    request.MetadataProviderExternalId, season.SeasonNumber, episode.EpisodeNumber,
                    request.Language, cancellationToken);

                episode.ApplyMetadata(episodeMetadata);

                if (!episode.IsFieldLocked(nameof(SerieEpisode.PersonRoles)) && episodeMetadata.PersonRoles.Count > 0)
                {
                    await ResolvePersonReferencesAsync(episodeMetadata.PersonRoles, cancellationToken);

                    episode.PersonRoles.Clear();
                    foreach (var role in episodeMetadata.PersonRoles)
                        episode.PersonRoles.Add(role);
                }

                if (episodeMetadata.RemoteId is not null)
                    episodeRemoteIds[(season.SeasonNumber, episode.EpisodeNumber)] = episodeMetadata.RemoteId.Value;

                if (!string.IsNullOrEmpty(episodeMetadata.StillImageUrl) && !episode.IsFieldLocked(nameof(SerieEpisode.Pictures)))
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

        // Re-parent RemoteIndexedFiles from serie to individual episodes
        if (request.MetadataProviderName == "federation" && episodeRemoteIds.Count > 0)
        {
            // Persist episodes first so FK references are valid
            await _context.SaveChangesAsync(cancellationToken);

            var serieRemoteFiles = await _context.RemoteIndexedFiles
                .Where(r => r.MediaId == serie.Id)
                .ToListAsync(cancellationToken);

            foreach (var season in serie.Seasons)
            {
                foreach (var episode in season.Episodes)
                {
                    if (!episodeRemoteIds.TryGetValue((season.SeasonNumber, episode.EpisodeNumber), out var remoteId))
                        continue;

                    var remoteFile = serieRemoteFiles.FirstOrDefault(r => r.RemoteMediaId == remoteId);
                    if (remoteFile is not null)
                    {
                        remoteFile.MediaId = episode.Id;
                    }
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
            if (track.IsFieldLocked(nameof(MusicTrack.ExternalIds))) continue;

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

            if (!track.IsFieldLocked(nameof(MusicTrack.Lyrics))
                && string.IsNullOrEmpty(track.Lyrics)
                && !string.IsNullOrEmpty(metadataTrack.Lyrics))
            {
                track.Lyrics = metadataTrack.Lyrics;
            }

            if (!track.IsFieldLocked(nameof(MusicTrack.LyricsLrc))
                && string.IsNullOrEmpty(track.LyricsLrc)
                && !string.IsNullOrEmpty(metadataTrack.LyricsLrc))
            {
                track.LyricsLrc = metadataTrack.LyricsLrc;
            }
        }
    }

    private async Task EnrichArtistsAsync(MusicAlbum album, ExternalMusicAlbumMetadata metadata, string language, CancellationToken cancellationToken)
    {
        if (album.ArtistId is null) return;

        var artist = await _context.Medias.OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .Include(a => a.Pictures)
            .Include(a => a.PersonRoles)
                .ThenInclude(pr => pr.Person)
            .FirstOrDefaultAsync(a => a.Id == album.ArtistId, cancellationToken);

        if (artist is null) return;

        // Try to match artist from metadata to get MusicBrainz ID
        var artistMetadata = metadata.Artists?.FirstOrDefault(a =>
            string.Equals(a.Name, artist.Title, StringComparison.OrdinalIgnoreCase));

        var mbExternalId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "musicbrainz");
        if (!artist.IsFieldLocked(nameof(MusicArtist.ExternalIds)) && mbExternalId is null && !string.IsNullOrEmpty(artistMetadata?.MusicBrainzArtistId))
        {
            mbExternalId = new ExternalId
            {
                ProviderName = "musicbrainz",
                Value = artistMetadata.MusicBrainzArtistId,
                MediaId = artist.Id
            };
            artist.ExternalIds.Add(mbExternalId);
        }

        // Always fetch MusicBrainz details (for members, country, image)
        ExternalMusicArtistDetails? mbDetails = null;
        string? mbImageUrl = null;

        if (_artistProviders.TryGetValue("musicbrainz", out var mbProvider))
        {
            var mbId = mbExternalId?.Value;
            mbDetails = !string.IsNullOrEmpty(mbId)
                ? await mbProvider.FetchByProviderIdAsync(mbId, language, cancellationToken)
                : await mbProvider.SearchByNameAsync(artist.Title!, language, cancellationToken);

            if (mbDetails is not null)
            {
                if (!artist.IsFieldLocked(nameof(MusicArtist.ExternalIds)))
                {
                    if (!string.IsNullOrEmpty(mbDetails.MusicBrainzArtistId) && !artist.ExternalIds.Any(e => e.ProviderName == "musicbrainz"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = mbDetails.MusicBrainzArtistId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.WikidataId) && !artist.ExternalIds.Any(e => e.ProviderName == "wikidata"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "wikidata", Value = mbDetails.WikidataId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.SpotifyId) && !artist.ExternalIds.Any(e => e.ProviderName == "spotify"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "spotify", Value = mbDetails.SpotifyId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.ImdbId) && !artist.ExternalIds.Any(e => e.ProviderName == "imdb"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = mbDetails.ImdbId, MediaId = artist.Id });
                }

                if (!artist.IsFieldLocked(nameof(MusicArtist.Country)) && !string.IsNullOrEmpty(mbDetails.Country) && string.IsNullOrEmpty(artist.Country))
                    artist.Country = mbDetails.Country;

                mbImageUrl = mbDetails.ImageUrl;

                await SyncArtistMembersAsync(artist, mbDetails.Members, language, cancellationToken);
            }
        }

        // Skip bio/image enrichment if already complete or locked
        var biographyLocked = artist.IsFieldLocked(nameof(MusicArtist.Biography));
        var picturesLocked = artist.IsFieldLocked(nameof(MusicArtist.Pictures));
        if ((picturesLocked || artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster))
            && (biographyLocked || !string.IsNullOrEmpty(artist.Biography))) return;

        var wikidataId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;

        if (!string.IsNullOrEmpty(wikidataId) && _artistProviders.TryGetValue("wikidata", out var wdProvider))
        {
            var details = await wdProvider.FetchByProviderIdAsync(wikidataId, language, cancellationToken);
            if (details is not null)
            {
                if (!biographyLocked && string.IsNullOrEmpty(artist.Biography) && !string.IsNullOrEmpty(details.Biography))
                    artist.Biography = details.Biography;

                if (!picturesLocked && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(details.ImageUrl))
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

        if (!picturesLocked && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster) && !string.IsNullOrEmpty(mbImageUrl))
        {
            var picture = new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                OriginalRemoteUri = new Uri(mbImageUrl),
                MediaId = artist.Id
            };
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            artist.Pictures.Add(picture);
        }
    }

    private async Task SyncTrackArtistCreditsAsync(MusicAlbum album, ExternalMusicAlbumMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata.Tracks is not { Count: > 0 }) return;

        foreach (var track in album.Tracks)
        {
            if (track.ArtistCredits.Count > 0) continue;

            var metadataTrack = metadata.Tracks.FirstOrDefault(mt =>
                mt.TrackNumber == track.TrackNumber
                || string.Equals(mt.Title, track.Title, StringComparison.OrdinalIgnoreCase));

            if (metadataTrack?.ArtistCredits is not { Count: > 0 }) continue;

            for (var i = 0; i < metadataTrack.ArtistCredits.Count; i++)
            {
                var credit = metadataTrack.ArtistCredits[i];
                var creditArtist = await FindOrCreateMusicArtistAsync(credit.Name, credit.MusicBrainzArtistId, cancellationToken);
                track.ArtistCredits.Add(new MusicArtistCredit
                {
                    MusicArtistId = creditArtist.Id,
                    MediaId = track.Id,
                    IsGuest = credit.IsGuest,
                    Order = i
                });
            }
        }
    }

    private async Task<MusicArtist> FindOrCreateMusicArtistAsync(string name, string? musicBrainzId, CancellationToken cancellationToken)
    {
        MusicArtist? existing = null;

        if (!string.IsNullOrEmpty(musicBrainzId))
        {
            existing = await _context.Medias.OfType<MusicArtist>()
                .FirstOrDefaultAsync(a => a.ExternalIds.Any(e =>
                    e.ProviderName == "musicbrainz" && e.Value == musicBrainzId), cancellationToken);
        }

        existing ??= await _context.Medias.OfType<MusicArtist>()
            .FirstOrDefaultAsync(a => a.Title == name, cancellationToken);

        if (existing is not null) return existing;

        var artist = new MusicArtist { Title = name };
        _context.Medias.Add(artist);

        if (!string.IsNullOrEmpty(musicBrainzId))
        {
            artist.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = musicBrainzId, MediaId = artist.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return artist;
    }

    private async Task SyncArtistMembersAsync(MusicArtist artist, IReadOnlyList<ExternalMusicArtistMember>? members, string language, CancellationToken cancellationToken)
    {
        if (members is not { Count: > 0 }) return;
        if (artist.PersonRoles.Count > 0) return;

        foreach (var member in members)
        {
            Person? person = null;
            var isNewPerson = false;

            // Try to find by MusicBrainz ExternalId first
            if (!string.IsNullOrEmpty(member.MusicBrainzArtistId))
            {
                person = await _context.Persons
                    .Include(p => p.ExternalIds)
                    .FirstOrDefaultAsync(p => p.ExternalIds.Any(e =>
                        e.ProviderName == "musicbrainz" && e.Value == member.MusicBrainzArtistId), cancellationToken);
            }

            // Fallback to name match
            person ??= await _context.Persons
                .Include(p => p.ExternalIds)
                .FirstOrDefaultAsync(p => p.Name == member.Name, cancellationToken);

            if (person is null)
            {
                person = new Person { Name = member.Name };
                _context.Persons.Add(person);
                await _context.SaveChangesAsync(cancellationToken);
                isNewPerson = true;
            }

            if (!string.IsNullOrEmpty(member.MusicBrainzArtistId)
                && !person.ExternalIds.Any(e => e.ProviderName == "musicbrainz"))
            {
                person.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = member.MusicBrainzArtistId, PersonId = person.Id });
            }

            var role = new MusicArtistMember
            {
                Person = person,
                PersonId = person.Id,
                MediaId = artist.Id,
                Role = member.Role,
                IsActive = member.IsActive
            };
            artist.PersonRoles.Add(role);

            // Queue person metadata enrichment for new persons with a MusicBrainz ID
            if (isNewPerson && !string.IsNullOrEmpty(member.MusicBrainzArtistId))
            {
                await _sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new RefreshPersonMetadataCommand
                    {
                        PersonId = person.Id,
                        ProviderName = "musicbrainz",
                        ProviderId = member.MusicBrainzArtistId,
                        Language = language
                    },
                    Priority = BackgroundTaskPriority.Low,
                    TargetEntityId = person.Id,
                    TargetEntityTypeName = nameof(Person),
                    MaxAttempts = 3,
                    ConcurrencyGroup = "musicbrainz"
                }, cancellationToken);
            }
        }
    }

    private async Task ResolvePersonReferencesAsync(IEnumerable<BasePersonRole> roles, CancellationToken cancellationToken)
    {
        foreach (var role in roles)
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

            existingPerson ??= await _context.Persons
                .Include(p => p.ExternalIds)
                .FirstOrDefaultAsync(p => p.Name == role.Person.Name, cancellationToken);

            if (existingPerson is not null)
                role.Person = existingPerson;
        }
    }
}
