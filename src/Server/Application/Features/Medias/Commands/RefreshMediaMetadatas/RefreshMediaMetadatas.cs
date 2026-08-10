using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.EnrichMusicArtistWikidata;
using K7.Server.Application.Features.Medias.Commands.EnrichSerieTmdbSupplemental;
using K7.Server.Application.Features.Medias.Commands.GenerateEpisodeStillFromSource;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly MetadataPictureDeletionService _pictureDeletionService;
    private readonly ILogger<RefreshMediaMetadatasCommandHandler> _logger;

    public RefreshMediaMetadatasCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        ISender sender,
        IEnumerable<IMusicArtistMetadataProvider> artistMetadataProviders,
        IMediaMetadataTagSyncService metadataTagSyncService,
        MetadataPictureDeletionService pictureDeletionService,
        ILogger<RefreshMediaMetadatasCommandHandler> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _sender = sender;
        _artistProviders = artistMetadataProviders.ToDictionary(p => p.ProviderName);
        _metadataTagSyncService = metadataTagSyncService;
        _pictureDeletionService = pictureDeletionService;
        _logger = logger;
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
                    .ThenInclude(p => p!.Variants)
            .Include(m => m.Ratings)
            .Include(m => m.MetadataTags)
                .ThenInclude(mt => mt.MetadataTag)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);
        Guard.Against.NotFound(request.MediaId, media);

        var refreshMedia = await MediaMetadataRefreshTargetHelper.ResolveRefreshMediaAsync(_context, media, cancellationToken);
        if (refreshMedia.Id != media.Id)
        {
            media = await _context.Medias
                .Include(m => m.ExternalIds)
                .Include(m => m.Pictures)
                .Include(m => m.PersonRoles)
                    .ThenInclude(pr => pr.Person)
                .Include(m => m.PersonRoles)
                    .ThenInclude(pr => pr.ExternalIds)
                .Include(m => m.PersonRoles)
                    .ThenInclude(pr => pr.PortraitPicture)
                        .ThenInclude(p => p!.Variants)
                .Include(m => m.Ratings)
                .Include(m => m.MetadataTags)
                    .ThenInclude(mt => mt.MetadataTag)
                .FirstAsync(m => m.Id == refreshMedia.Id, cancellationToken);

            var providerExternalId = media.ExternalIds
                .FirstOrDefault(e => string.Equals(e.ProviderName, request.MetadataProviderName, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrEmpty(providerExternalId))
            {
                request = request with { MetadataProviderExternalId = providerExternalId };
            }
        }

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
                    foreach (var externalId in personRole.Person.ExternalIds.ToList())
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

            if (!movie.IsFieldLocked(nameof(Movie.PersonRoles)) && metadata.PersonRoles.Count > 0)
                RemovePersonRolePortraits(movie.PersonRoles);

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

        await _context.Entry(album).Collection(a => a.Tracks).Query()
            .Include(t => t.ExternalIds)
            .Include(t => t.ArtistCredits)
            .Include(t => t.IndexedFiles)
            .LoadAsync(cancellationToken);

        MusicAlbumReleaseHints? releaseHints = null;
        if (provider is IMusicAlbumReleaseAwareMetadataProvider)
        {
            var candidateTracks = album.Tracks
                .Where(t => t.IndexedFiles.Count > 0)
                .ToList();
            if (candidateTracks.Count == 0)
                candidateTracks = album.Tracks.ToList();

            releaseHints = new MusicAlbumReleaseHints
            {
                ExpectedTrackCount = candidateTracks.Count > 0 ? candidateTracks.Count : null,
                ExpectedTrackTitles = candidateTracks
                    .Where(t => !string.IsNullOrWhiteSpace(t.Title))
                    .Select(t => t.Title!)
                    .ToList(),
                PreferredReleaseId = album.ExternalIds
                    .FirstOrDefault(e => e.ProviderName == "musicbrainz-release")?.Value
            };
        }

        var metadata = provider is IMusicAlbumReleaseAwareMetadataProvider releaseAware
            ? await releaseAware.FetchMetadata(request.MetadataProviderExternalId, request.Language, releaseHints, cancellationToken)
            : await provider.FetchMetadata(request.MetadataProviderExternalId, request.Language, cancellationToken);

        if (metadata != null)
        {
            album.ApplyMetadata(metadata);
            UpsertAlbumReleaseExternalId(album, metadata);
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
                            SortTitle = trackMeta.SortTitle ?? MediaSortTitleHelper.Compute(trackMeta.Title),
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
                    var artist = await FindOrCreateMusicArtistAsync(primaryArtist.Name, primaryArtist.SortName, primaryArtist.MusicBrainzArtistId, cancellationToken);
                    album.ArtistId = artist.Id;
                }
            }

            await EnrichArtistsAsync(album, metadata, request.Language, cancellationToken);
            await PersistTrackExternalIdsAsync(album, metadata, cancellationToken);
            await SyncTrackArtistCreditsAsync(album, metadata, cancellationToken);
        }
    }

    private static void UpsertAlbumReleaseExternalId(MusicAlbum album, ExternalMusicAlbumMetadata metadata)
    {
        if (album.IsFieldLocked(nameof(MusicAlbum.ExternalIds)))
            return;

        var releaseId = metadata.ExternalIds?
            .FirstOrDefault(e => e.ProviderName == "musicbrainz-release")?.Value;
        if (string.IsNullOrWhiteSpace(releaseId))
            return;

        var existing = album.ExternalIds.FirstOrDefault(e => e.ProviderName == "musicbrainz-release");
        if (existing is not null)
        {
            existing.Value = releaseId;
            foreach (var duplicate in album.ExternalIds.Where(e => e.ProviderName == "musicbrainz-release" && e != existing).ToList())
                album.ExternalIds.Remove(duplicate);
        }
        else
            album.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz-release", Value = releaseId, MediaId = album.Id });
    }

    private async Task HandleMusicArtistAsync(RefreshMediaMetadatasCommand request, MusicArtist artist, CancellationToken cancellationToken)
    {
        var language = request.Language;

        if (_artistProviders.TryGetValue("musicbrainz", out var mbProvider))
        {
            ExternalMusicArtistDetails? mbDetails = null;
            if (!string.IsNullOrWhiteSpace(request.MetadataProviderExternalId))
            {
                mbDetails = await mbProvider.FetchByProviderIdAsync(
                    request.MetadataProviderExternalId, language, cancellationToken);
            }

            mbDetails ??= !string.IsNullOrWhiteSpace(artist.Title)
                ? await mbProvider.SearchByNameAsync(artist.Title!, language, cancellationToken)
                : null;

            if (mbDetails is not null)
            {
                if (!artist.IsFieldLocked(nameof(MusicArtist.Country)) && !string.IsNullOrEmpty(mbDetails.Country))
                    artist.Country = mbDetails.Country;

                if (!artist.IsFieldLocked(nameof(MusicArtist.ExternalIds)))
                {
                    if (!string.IsNullOrEmpty(mbDetails.MusicBrainzArtistId)
                        && !artist.ExternalIds.Any(e => e.ProviderName == "musicbrainz"))
                    {
                        artist.ExternalIds.Add(new ExternalId
                        {
                            ProviderName = "musicbrainz",
                            Value = mbDetails.MusicBrainzArtistId,
                            MediaId = artist.Id
                        });
                    }

                    if (!string.IsNullOrEmpty(mbDetails.WikidataId) && !artist.ExternalIds.Any(e => e.ProviderName == "wikidata"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "wikidata", Value = mbDetails.WikidataId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.SpotifyId) && !artist.ExternalIds.Any(e => e.ProviderName == "spotify"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "spotify", Value = mbDetails.SpotifyId, MediaId = artist.Id });

                    if (!string.IsNullOrEmpty(mbDetails.ImdbId) && !artist.ExternalIds.Any(e => e.ProviderName == "imdb"))
                        artist.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = mbDetails.ImdbId, MediaId = artist.Id });
                }

                await SyncArtistMembersAsync(artist, mbDetails.Members, request.Language, cancellationToken);

                // Poster from MusicBrainz cover art
                if (!artist.IsPictureTypeLocked(MetadataPictureType.Poster)
                    && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster)
                    && MetadataImageUrlHelper.TryCreateRemoteUri(mbDetails.ImageUrl, out var mbImageUri))
                {
                    var picture = new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        OriginalRemoteUri = mbImageUri,
                        MediaId = artist.Id
                    };
                    picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
                    artist.Pictures.Add(picture);
                }
            }
        }

        await QueueEnrichMusicArtistWikidataIfNeededAsync(artist, language, cancellationToken);
    }

    private async Task HandleSerieAsync(RefreshMediaMetadatasCommand request, Serie serie, CancellationToken cancellationToken)
    {
        // Load serie-specific includes
        await _context.Entry(serie).Collection(s => s.Seasons).Query()
            .Include(s => s.Pictures)
            .Include(s => s.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.Pictures)
            .Include(s => s.Episodes).ThenInclude(e => e.Ratings)
            .Include(s => s.Episodes).ThenInclude(e => e.PersonRoles)
                .ThenInclude(pr => pr.PortraitPicture!)
                    .ThenInclude(p => p.Variants)
            .LoadAsync(cancellationToken);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(request.MetadataProviderName);

        var serieMetadata = await metadataProvider.FetchSerieMetadataAsync(
            request.MetadataProviderExternalId, request.Language, cancellationToken, request.FallbackLanguage);

        if (!serie.IsFieldLocked(nameof(Serie.PersonRoles)) && serieMetadata.PersonRoles?.Count > 0)
        {
            await ResolvePersonReferencesAsync(serieMetadata.PersonRoles, cancellationToken);
            RemovePersonRolePortraits(serie.PersonRoles);
        }

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

        SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(serie, serieMetadata.Ratings);

        // Persist crosswalk ids from serie fetch before episode loop so inline fallback can resolve TMDB/TVDB.
        if (!serie.IsFieldLocked(nameof(Serie.ExternalIds)) && serieMetadata.ExternalIds?.Count > 0)
        {
            foreach (var external in serieMetadata.ExternalIds)
            {
                if (string.IsNullOrWhiteSpace(external.ProviderName) || string.IsNullOrWhiteSpace(external.Value))
                    continue;
                if (serie.ExternalIds.Any(e =>
                        string.Equals(e.ProviderName, external.ProviderName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                serie.ExternalIds.Add(new ExternalId
                {
                    ProviderName = external.ProviderName,
                    Value = external.Value,
                    MediaId = serie.Id
                });
            }
        }

        if (string.IsNullOrWhiteSpace(serie.NumberingProviderName))
            serie.NumberingProviderName = MetadataProviderHostMapper.NormalizeProviderName(request.MetadataProviderName);

        var enrichmentProviderName = SerieMetadataProviderCascade.ResolveEnrichmentProvider(
            serie.NumberingProviderName ?? request.MetadataProviderName);
        ISerieMetadataProvider? enrichmentProvider = null;
        string? enrichmentExternalId = null;
        if (!string.IsNullOrWhiteSpace(enrichmentProviderName))
        {
            enrichmentExternalId = serie.ExternalIds
                .FirstOrDefault(e => string.Equals(e.ProviderName, enrichmentProviderName, StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?? (enrichmentProviderName == MetadataProviderNames.Tmdb
                    ? serie.ExternalIds.FirstOrDefault(e => e.ProviderName == MetadataProviderNames.Imdb)?.Value
                    : null);
            if (!string.IsNullOrWhiteSpace(enrichmentExternalId))
                enrichmentProvider = _serviceProvider.GetKeyedService<ISerieMetadataProvider>(enrichmentProviderName);
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
            ExternalSeasonMetadata seasonMetadata;
            try
            {
                seasonMetadata = await metadataProvider.FetchSeasonMetadataAsync(
                    request.MetadataProviderExternalId, season.SeasonNumber, request.Language, cancellationToken, request.FallbackLanguage);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping season {SeasonNumber} metadata refresh for serie {MediaId} via {Provider}",
                    season.SeasonNumber,
                    serie.Id,
                    request.MetadataProviderName);
                continue;
            }

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

        // Fetch and apply episode metadata (catalog-first, then inline enrichment fallback)
        var episodeRemoteIds = new Dictionary<(int Season, int Episode), Guid>();
        foreach (var season in serie.Seasons)
        {
            foreach (var episode in season.Episodes)
            {
                ExternalEpisodeMetadata? episodeMetadata = null;
                try
                {
                    episodeMetadata = await metadataProvider.TryBuildEpisodeMetadataFromCatalogAsync(
                        request.MetadataProviderExternalId,
                        season.SeasonNumber,
                        episode.EpisodeNumber,
                        request.Language,
                        request.FallbackLanguage,
                        cancellationToken);

                    episodeMetadata ??= await metadataProvider.FetchEpisodeMetadataAsync(
                        request.MetadataProviderExternalId, season.SeasonNumber, episode.EpisodeNumber,
                        request.Language, cancellationToken, request.FallbackLanguage);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "Canon episode miss S{SeasonNumber}E{EpisodeNumber} for serie {MediaId} via {Provider}",
                        season.SeasonNumber,
                        episode.EpisodeNumber,
                        serie.Id,
                        request.MetadataProviderName);
                }

                if (episodeMetadata is null
                    && enrichmentProvider is not null
                    && !string.IsNullOrWhiteSpace(enrichmentExternalId))
                {
                    try
                    {
                        episodeMetadata = await enrichmentProvider.TryBuildEpisodeMetadataFromCatalogAsync(
                            enrichmentExternalId,
                            season.SeasonNumber,
                            episode.EpisodeNumber,
                            request.Language,
                            request.FallbackLanguage,
                            cancellationToken);

                        episodeMetadata ??= await enrichmentProvider.FetchEpisodeMetadataAsync(
                            enrichmentExternalId,
                            season.SeasonNumber,
                            episode.EpisodeNumber,
                            request.Language,
                            cancellationToken,
                            request.FallbackLanguage);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Skipping episode S{SeasonNumber}E{EpisodeNumber} metadata refresh for serie {MediaId} via {Provider}",
                            season.SeasonNumber,
                            episode.EpisodeNumber,
                            serie.Id,
                            enrichmentProviderName);
                    }
                }

                if (episodeMetadata is null)
                {
                    if (!episode.IsPictureTypeLocked(MetadataPictureType.Still))
                        await TryQueueEpisodeStillFromSourceFallbackAsync(episode, cancellationToken);
                    continue;
                }

                episode.ApplyMetadata(episodeMetadata);

                if (!episode.IsFieldLocked(nameof(SerieEpisode.PersonRoles)) && episodeMetadata.PersonRoles.Count > 0)
                {
                    await ResolvePersonReferencesAsync(episodeMetadata.PersonRoles, cancellationToken);

                    RemovePersonRolePortraits(episode.PersonRoles);
                    episode.PersonRoles.Clear();
                    foreach (var role in episodeMetadata.PersonRoles)
                        episode.PersonRoles.Add(role);
                }

                if (episodeMetadata.RemoteId is not null)
                    episodeRemoteIds[(season.SeasonNumber, episode.EpisodeNumber)] = episodeMetadata.RemoteId.Value;

                var stillImageUrl = episodeMetadata.StillImageUrl;
                var episodeRatings = episodeMetadata.Ratings;

                SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(episode, episodeRatings);

                if (!string.IsNullOrEmpty(stillImageUrl)
                    && !episode.IsPictureTypeLocked(MetadataPictureType.Still)
                    && MetadataImageUrlHelper.TryCreateRemoteUri(stillImageUrl, out var stillUri))
                {
                    var stillPicture = new MetadataPicture
                    {
                        OriginalRemoteUri = stillUri,
                        Type = MetadataPictureType.Still
                    };
                    stillPicture.AddDomainEvent(new MetadataPictureCreatedEvent(stillPicture));

                    episode.RemovePicturesOfType(MetadataPictureType.Still);
                    episode.Pictures.Add(stillPicture);
                }
                else if (!episode.IsPictureTypeLocked(MetadataPictureType.Still))
                {
                    await TryQueueEpisodeStillFromSourceFallbackAsync(episode, cancellationToken);
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

        if (string.Equals(request.MetadataProviderName, "tvdb", StringComparison.OrdinalIgnoreCase))
        {
            await QueueEnrichSerieTmdbSupplementalAsync(serie, request, cancellationToken);
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
                    mt.DiscNumber == track.DiscNumber && mt.TrackNumber == track.TrackNumber)
                ?? metadata.Tracks.FirstOrDefault(mt =>
                    mt.TrackNumber == track.TrackNumber
                    && string.Equals(mt.Title, track.Title, StringComparison.OrdinalIgnoreCase))
                ?? metadata.Tracks.FirstOrDefault(mt =>
                    string.Equals(mt.Title, track.Title, StringComparison.OrdinalIgnoreCase));

            if (metadataTrack is null) continue;

            if (!track.IsFieldLocked(nameof(MusicTrack.SortTitle))
                && metadataTrack.SortTitle is not null)
            {
                track.SortTitle = metadataTrack.SortTitle;
            }

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

        // Match album artist by name; never fall back to Artists[0] (VA / multi-artist albums).
        var artistMetadata = metadata.Artists?.FirstOrDefault(a =>
            MusicArtistNameNormalizer.NamesMatch(a.Name, artist.Title));

        if (!artist.IsFieldLocked(nameof(MusicArtist.SortTitle))
            && artistMetadata?.SortName is not null)
        {
            artist.SortTitle = artistMetadata.SortName;
        }

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
        var posterLocked = artist.IsPictureTypeLocked(MetadataPictureType.Poster);
        if ((posterLocked || artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster))
            && (biographyLocked || !string.IsNullOrEmpty(artist.Biography)))
        {
            return;
        }

        await QueueEnrichMusicArtistWikidataIfNeededAsync(artist, language, cancellationToken);

        if (!posterLocked
            && !artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster)
            && MetadataImageUrlHelper.TryCreateRemoteUri(mbImageUrl, out var coverImageUri))
        {
            var picture = new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                OriginalRemoteUri = coverImageUri,
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
                var creditArtist = await FindOrCreateMusicArtistAsync(credit.Name, null, credit.MusicBrainzArtistId, cancellationToken);
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

    private async Task<MusicArtist> FindOrCreateMusicArtistAsync(string name, string? sortName, string? musicBrainzId, CancellationToken cancellationToken)
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

        var artist = new MusicArtist
        {
            Title = name,
            SortTitle = sortName ?? MediaSortTitleHelper.Compute(name)
        };
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
                    TargetEntityId = person.Id,
                    TargetEntityTypeName = nameof(Person),
                    Lane = BackgroundTaskLane.Metadata,
                    MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName("musicbrainz"),
                    WorkClass = BackgroundTaskWorkClass.Polish,
                    TriggeredBy = BackgroundTaskTriggeredBy.System,
                    MaxAttempts = 3
                }, cancellationToken);
            }
        }
    }

    private async Task QueueEnrichSerieTmdbSupplementalAsync(
        Serie serie,
        RefreshMediaMetadatasCommand request,
        CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new EnrichSerieTmdbSupplementalCommand
            {
                MediaId = serie.Id,
                Language = request.Language,
                FallbackLanguage = request.FallbackLanguage
            },
            TargetEntityId = serie.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName("tmdb"),
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 3
        }, cancellationToken);
    }

    private async Task QueueEnrichMusicArtistWikidataIfNeededAsync(
        MusicArtist artist,
        string language,
        CancellationToken cancellationToken)
    {
        var wikidataId = artist.ExternalIds.FirstOrDefault(e => e.ProviderName == "wikidata")?.Value;
        if (string.IsNullOrEmpty(wikidataId))
            return;

        var biographyLocked = artist.IsFieldLocked(nameof(MusicArtist.Biography));
        var posterLocked = artist.IsPictureTypeLocked(MetadataPictureType.Poster);
        if ((posterLocked || artist.Pictures.Any(p => p.Type == MetadataPictureType.Poster))
            && (biographyLocked || !string.IsNullOrEmpty(artist.Biography)))
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new EnrichMusicArtistWikidataCommand
            {
                MediaId = artist.Id,
                Language = language
            },
            TargetEntityId = artist.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName("wikidata"),
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 3
        }, cancellationToken);
    }

    private async Task ResolvePersonReferencesAsync(IEnumerable<BasePersonRole> roles, CancellationToken cancellationToken)
    {
        foreach (var role in roles.ToList())
        {
            if (role.Person is null)
                continue;

            var matchedPersons = new List<Person>();
            foreach (var externalId in role.Person.ExternalIds.ToList())
            {
                var match = await _context.Persons
                    .Include(p => p.ExternalIds)
                    .Include(p => p.PortraitPicture)
                    .FirstOrDefaultAsync(p => p.ExternalIds.Any(e =>
                        e.ProviderName == externalId.ProviderName && e.Value == externalId.Value),
                        cancellationToken);
                if (match is not null && matchedPersons.All(p => p.Id != match.Id))
                    matchedPersons.Add(match);
            }

            Person? existingPerson = matchedPersons.Count > 0
                ? PickCanonicalPerson(matchedPersons)
                : await _context.Persons
                    .Include(p => p.ExternalIds)
                    .Include(p => p.PortraitPicture)
                    .FirstOrDefaultAsync(p => p.Name == role.Person.Name, cancellationToken);

            if (existingPerson is null)
                continue;

            foreach (var duplicate in matchedPersons.Where(p => !ReferenceEquals(p, existingPerson)))
                PersonMetadataMergeHelper.MergeMissingPersonData(existingPerson, duplicate);

            if (!ReferenceEquals(existingPerson, role.Person))
                PersonMetadataMergeHelper.MergeMissingPersonData(existingPerson, role.Person);

            role.Person = existingPerson;
        }
    }

    private static Person PickCanonicalPerson(IReadOnlyList<Person> persons)
    {
        return persons
            .OrderByDescending(p => p.ExternalIds.Any(e => e.ProviderName == "tmdb"))
            .ThenByDescending(p => !string.IsNullOrWhiteSpace(p.Biography))
            .ThenByDescending(p => p.Birthday.HasValue)
            .ThenBy(p => p.Id)
            .First();
    }

    private void RemovePersonRolePortraits(IEnumerable<BasePersonRole> roles)
    {
        foreach (var role in roles)
        {
            if (role.PortraitPicture is null)
                continue;

            _pictureDeletionService.Remove(role.PortraitPicture);
            role.PortraitPicture = null;
        }
    }

    private async Task TryQueueEpisodeStillFromSourceFallbackAsync(
        SerieEpisode episode,
        CancellationToken cancellationToken)
    {
        if (episode.Pictures.Any(picture => picture.Type == MetadataPictureType.Still))
            return;

        var hasIndexedVideo = await _context.IndexedFiles
            .AnyAsync(
                file => file.MediaId == episode.Id && file.FileMetadata is VideoFileMetadata,
                cancellationToken);

        if (!hasIndexedVideo)
            return;

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateEpisodeStillFromSourceCommand { MediaId = episode.Id },
            TargetEntityId = episode.Id,
            TargetEntityTypeName = nameof(SerieEpisode),
            Lane = BackgroundTaskLane.ImageExtract,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 2
        }, cancellationToken);
    }
}
