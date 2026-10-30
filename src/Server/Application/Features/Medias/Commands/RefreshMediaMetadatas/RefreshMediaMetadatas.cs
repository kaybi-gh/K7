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

    /// <summary>
    /// When true, only unenriched episodes (and their seasons) are fetched. Series/season artwork
    /// and cast already present are left in place. Used when a new episode is attached to an existing series.
    /// </summary>
    public bool Incremental { get; init; }
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
                    .ThenInclude(p => p.PortraitPicture)
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
                        .ThenInclude(p => p.PortraitPicture)
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

        // Movie roles come from the TMDb credits payload (name, id, portrait). New persons get
        // their biography and birthday from a background refresh once they have an id.
        if (media is Movie refreshedMovie)
        {
            await PersonMetadataRefreshQueueHelper.QueueMissingTmdbDetailsAsync(
                _sender,
                refreshedMovie.PersonRoles.Select(r => r.Person),
                request.Language,
                cancellationToken);
        }
    }

    private async Task HandleMovieAsync(RefreshMediaMetadatasCommand request, Movie movie, CancellationToken cancellationToken = default)
    {
        var provider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(request.MetadataProviderName);
        var metadata = await provider.FetchMetadata(request.MetadataProviderExternalId,
                request.Language,
                cancellationToken);

        if (metadata != null)
        {
            if (metadata.PersonRoles.Count > 0)
            {
                await PersonRolePersonResolveHelper.ResolveAsync(
                    _context, metadata.PersonRoles, cancellationToken);
                PersonRolePersonResolveHelper.PreferPersonLevelPortraits(metadata.PersonRoles);
            }

            if (!movie.IsFieldLocked(nameof(Movie.PersonRoles)) && metadata.PersonRoles.Count > 0)
            {
                PersonRolePortraitReuseHelper.PreserveDownloadedPortraits(movie.PersonRoles, metadata.PersonRoles);
                RemovePersonRolePortraits(movie.PersonRoles);
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

            // Prefer on-disk file count over MusicTrack row count: a prior title-only merge
            // can leave 22 files on 16 rows and would otherwise hint a 16-track single disc.
            var fileCount = candidateTracks.Sum(t => t.IndexedFiles.Count);
            var layoutTrackCount = fileCount > 0 ? fileCount : candidateTracks.Count;

            var discCount = candidateTracks
                .Select(t => MusicAlbumTrackMatchHelper.NormalizeDisc(t.DiscNumber))
                .Distinct()
                .Count();

            releaseHints = new MusicAlbumReleaseHints
            {
                ExpectedTrackCount = layoutTrackCount > 0 ? layoutTrackCount : null,
                ExpectedDiscCount = candidateTracks.Count > 0 ? discCount : null,
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

            // Create missing disc+track shells from the chosen release (deluxe 10+12, etc.).
            // Local libraries previously only stamped ids onto existing file-backed rows.
            EnsureAlbumTracklistShells(album, metadata);

            // Federation: re-parent remote files and fill artist when the peer album has no local scan.
            if (request.MetadataProviderName == "federation")
            {
                if (album.Tracks.Count > 0 && metadata.Tracks is { Count: > 0 })
                {
                    var albumRemoteFiles = await _context.RemoteIndexedFiles
                        .Where(r => r.MediaId == album.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var trackMeta in metadata.Tracks)
                    {
                        if (trackMeta.RemoteId is null) continue;

                        var localTrack = MusicAlbumTrackMatchHelper.FindOnAlbum(
                            album.Tracks,
                            trackMeta.Title,
                            trackMeta.TrackNumber,
                            trackMeta.DiscNumber);

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

    /// <summary>
    /// Ensures every MusicBrainz medium/track slot exists as a local MusicTrack row
    /// (file-less shells for missing files). Match by disc+track only - never by title alone.
    /// </summary>
    private void EnsureAlbumTracklistShells(MusicAlbum album, ExternalMusicAlbumMetadata metadata)
    {
        if (metadata.Tracks is not { Count: > 0 })
            return;

        foreach (var existing in album.Tracks)
        {
            if (!string.IsNullOrWhiteSpace(existing.Title)
                || existing.IsFieldLocked(nameof(MusicTrack.Title)))
                continue;

            var meta = MusicAlbumTrackMatchHelper.FindMetadataTrackBySlot(
                metadata.Tracks,
                existing.DiscNumber,
                existing.TrackNumber,
                mt => mt.DiscNumber,
                mt => mt.TrackNumber);
            if (meta?.Title is null)
                continue;

            existing.Title = meta.Title;
            existing.SortTitle = meta.SortTitle ?? MediaSortTitleHelper.Compute(meta.Title);
        }

        var shells = MusicAlbumTrackMatchHelper.CreateMissingTrackShells(
            album.Id,
            album.Tracks,
            metadata.Tracks.Select(mt => (
                mt.DiscNumber,
                mt.TrackNumber,
                (string?)mt.Title,
                (string?)mt.SortTitle)));

        foreach (var track in shells)
        {
            _context.Medias.Add(track);
            album.Tracks.Add(track);
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

                if (!artist.IsFieldLocked(nameof(MusicArtist.ReleaseDate)) && mbDetails.FormedOn is DateOnly formedOn)
                    artist.ReleaseDate = formedOn;

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

                ApplyMusicArtistLocalizedNames(artist, mbDetails.Name, mbDetails.OriginalName, mbDetails.SortName);

                await SyncArtistMembersAsync(artist, mbDetails.Members, request.Language, cancellationToken);
                await SoloArtistBirthDate.ApplyAsync(_context, artist, cancellationToken);

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

        var providerKey = SerieMetadataProviderCascade.ResolveKeyedProviderName(
            request.MetadataProviderName,
            serie.NumberingProviderName,
            serie.ExternalIds,
            request.MetadataProviderExternalId);
        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(providerKey);

        var serieMetadata = await metadataProvider.FetchSerieMetadataAsync(
            request.MetadataProviderExternalId, request.Language, cancellationToken, request.FallbackLanguage);

        var applySeriePersonRoles = !request.Incremental || serie.PersonRoles.Count == 0;
        if (applySeriePersonRoles
            && !serie.IsFieldLocked(nameof(Serie.PersonRoles))
            && serieMetadata.PersonRoles?.Count > 0)
        {
            await PersonRolePersonResolveHelper.ResolveAsync(
                _context, serieMetadata.PersonRoles, cancellationToken);
            PersonRolePersonResolveHelper.PreferPersonLevelPortraits(serieMetadata.PersonRoles);
            PersonRolePortraitReuseHelper.PreserveDownloadedPortraits(serie.PersonRoles, serieMetadata.PersonRoles);
            RemovePersonRolePortraits(serie.PersonRoles);
        }
        else if (request.Incremental && serie.PersonRoles.Count > 0)
        {
            serieMetadata.PersonRoles?.Clear();
        }

        if (request.Incremental)
            SerieEpisodeEnrichmentHelper.RemoveExistingPictureTypes(serie, serieMetadata.Pictures);

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
            serie.NumberingProviderName = MetadataProviderHostMapper.NormalizeProviderName(providerKey);

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

        var seasonsToRefresh = request.Incremental
            ? serie.Seasons.Where(season =>
                    SerieEpisodeEnrichmentHelper.IsSeasonUnenriched(season)
                    || season.Episodes.Any(SerieEpisodeEnrichmentHelper.IsUnenriched))
                .ToList()
            : [.. serie.Seasons];

        if (request.Incremental)
        {
            _logger.LogInformation(
                "Incremental metadata refresh for serie {MediaId}: {SeasonCount} seasons, {EpisodeCount} new episodes",
                serie.Id,
                seasonsToRefresh.Count,
                seasonsToRefresh.SelectMany(season => season.Episodes).Count(SerieEpisodeEnrichmentHelper.IsUnenriched));
        }

        // Fetch and apply season metadata
        foreach (var season in seasonsToRefresh)
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

            if (request.Incremental)
                SerieEpisodeEnrichmentHelper.RemoveExistingPictureTypes(season, seasonMetadata.Pictures);

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

        // Fetch and apply episode metadata (catalog-first, then inline enrichment fallback).
        // Cast is resolved in a second pass so person lookups are batched across the serie.
        var episodeRemoteIds = new Dictionary<(int Season, int Episode), Guid>();
        var episodeCastJobs = new List<(SerieEpisode Episode, ExternalEpisodeMetadata Metadata)>();
        foreach (var season in seasonsToRefresh)
        {
            foreach (var episode in season.Episodes)
            {
                if (request.Incremental && !SerieEpisodeEnrichmentHelper.IsUnenriched(episode))
                    continue;

                ExternalEpisodeMetadata? episodeMetadata = null;
                try
                {
                    episodeMetadata = await ResolveEpisodeMetadataAsync(
                        metadataProvider,
                        request.MetadataProviderExternalId,
                        season.SeasonNumber,
                        episode.EpisodeNumber,
                        request.Language,
                        request.FallbackLanguage,
                        episode,
                        request.Incremental,
                        cancellationToken);
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
                        episodeMetadata = await ResolveEpisodeMetadataAsync(
                            enrichmentProvider,
                            enrichmentExternalId,
                            season.SeasonNumber,
                            episode.EpisodeNumber,
                            request.Language,
                            request.FallbackLanguage,
                            episode,
                            request.Incremental,
                            cancellationToken);
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
                    episodeCastJobs.Add((episode, episodeMetadata));

                if (episodeMetadata.RemoteId is not null)
                    episodeRemoteIds[(season.SeasonNumber, episode.EpisodeNumber)] = episodeMetadata.RemoteId.Value;

                var stillImageUrl = episodeMetadata.StillImageUrl;
                var episodeRatings = episodeMetadata.Ratings;

                SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(episode, episodeRatings);

                var preferredStillUrl = await PreferHdEpisodeStillUrlAsync(
                    episode,
                    stillImageUrl,
                    enrichmentProvider,
                    enrichmentExternalId,
                    enrichmentProviderName,
                    season.SeasonNumber,
                    episode.EpisodeNumber,
                    request.Language,
                    request.FallbackLanguage,
                    request.Incremental,
                    cancellationToken);

                if (!episode.IsPictureTypeLocked(MetadataPictureType.Still))
                    stillImageUrl = preferredStillUrl;

                if (!string.IsNullOrEmpty(stillImageUrl)
                    && !episode.IsPictureTypeLocked(MetadataPictureType.Still)
                    && MetadataImageUrlHelper.TryCreateRemoteUri(stillImageUrl, out var stillUri)
                    && stillUri is not null)
                {
                    // Full refresh used to always recreate stills (even same URI), wiping local
                    // files and re-flooding the download queue alongside episode cast portraits.
                    if (!MetadataImageUrlHelper.HasDownloadedPictureWithRemoteUri(
                            episode.Pictures,
                            MetadataPictureType.Still,
                            stillUri))
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
                }
                else if (!episode.IsPictureTypeLocked(MetadataPictureType.Still))
                {
                    await TryQueueEpisodeStillFromSourceFallbackAsync(episode, cancellationToken);
                }
            }
        }

        if (episodeCastJobs.Count > 0)
        {
            var allEpisodeRoles = episodeCastJobs
                .SelectMany(job => job.Metadata.PersonRoles)
                .ToList();
            await PersonRolePersonResolveHelper.ResolveAsync(_context, allEpisodeRoles, cancellationToken);
            // One portrait download per person across the whole serie, not per episode role.
            PersonRolePersonResolveHelper.PreferPersonLevelPortraits(allEpisodeRoles);

            foreach (var (episode, episodeMetadata) in episodeCastJobs)
            {
                PersonRolePortraitReuseHelper.PreserveDownloadedPortraits(
                    episode.PersonRoles,
                    episodeMetadata.PersonRoles);
                RemovePersonRolePortraits(episode.PersonRoles);
                episode.PersonRoles.Clear();
                foreach (var role in episodeMetadata.PersonRoles)
                    episode.PersonRoles.Add(role);
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

        if (!request.Incremental
            && string.Equals(providerKey, "tvdb", StringComparison.OrdinalIgnoreCase))
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

        var recordingIdClaimCounts = album.Tracks
            .Select(t =>
                existingExternalIds.FirstOrDefault(e => e.MediaId == t.Id
                    && string.Equals(e.ProviderName, "musicbrainz", StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?? t.ExternalIds.FirstOrDefault(e =>
                    string.Equals(e.ProviderName, "musicbrainz", StringComparison.OrdinalIgnoreCase))
                ?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var track in album.Tracks)
        {
            if (track.IsFieldLocked(nameof(MusicTrack.ExternalIds))) continue;

            var existingRecordingId = existingExternalIds
                .FirstOrDefault(e => e.MediaId == track.Id
                    && string.Equals(e.ProviderName, "musicbrainz", StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?? track.ExternalIds.FirstOrDefault(e =>
                    string.Equals(e.ProviderName, "musicbrainz", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            var recordingShared = !string.IsNullOrWhiteSpace(existingRecordingId)
                && recordingIdClaimCounts.TryGetValue(existingRecordingId, out var claimants)
                && claimants > 1;

            // Prefer a unique stored recording when its title still matches; otherwise title on
            // disc, then slot. Shared/wrong stamps (Spotlight carrying 23's id) get corrected.
            var metadataTrack = MusicAlbumTrackMatchHelper.FindMetadataTrackForRecordingStamp(
                metadata.Tracks,
                track.DiscNumber,
                track.TrackNumber,
                track.Title,
                existingRecordingId,
                recordingShared,
                mt => mt.DiscNumber,
                mt => mt.TrackNumber,
                mt => mt.Title,
                mt => mt.MusicBrainzRecordingId);

            if (metadataTrack is null) continue;

            if (!track.IsFieldLocked(nameof(MusicTrack.SortTitle))
                && metadataTrack.SortTitle is not null)
            {
                track.SortTitle = metadataTrack.SortTitle;
            }

            if (!string.IsNullOrEmpty(metadataTrack.MusicBrainzRecordingId))
            {
                UpsertTrackExternalId(
                    track,
                    existingExternalIds,
                    "musicbrainz",
                    metadataTrack.MusicBrainzRecordingId);
            }

            if (!string.IsNullOrEmpty(metadataTrack.Isrc))
            {
                UpsertTrackExternalId(
                    track,
                    existingExternalIds,
                    "isrc",
                    metadataTrack.Isrc);
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

    private static void UpsertTrackExternalId(
        MusicTrack track,
        List<ExternalId> existingExternalIds,
        string providerName,
        string value)
    {
        var existing = existingExternalIds.FirstOrDefault(e =>
            e.MediaId == track.Id
            && string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        var attached = track.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (attached is not null)
        {
            attached.Value = value;
            existingExternalIds.Add(attached);
            return;
        }

        var created = new ExternalId
        {
            ProviderName = providerName,
            Value = value,
            MediaId = track.Id
        };
        track.ExternalIds.Add(created);
        existingExternalIds.Add(created);
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
            MusicArtistNameNormalizer.NamesMatch(a.Name, artist.Title)
            || MusicArtistNameNormalizer.NamesMatch(a.OriginalName, artist.Title)
            || MediaIdentityKeys.PersonNamesMatch(a.Name, artist.Title)
            || MediaIdentityKeys.PersonNamesMatch(a.OriginalName, artist.Title)
            || MediaIdentityKeys.PersonNamesMatch(a.SortName, artist.Title)
            || MediaIdentityKeys.PersonNamesMatch(a.SortName, artist.SortTitle));

        if (artistMetadata is not null)
            ApplyMusicArtistLocalizedNames(artist, artistMetadata.Name, artistMetadata.OriginalName, artistMetadata.SortName);

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

                if (!artist.IsFieldLocked(nameof(MusicArtist.ReleaseDate))
                    && artist.ReleaseDate is null
                    && mbDetails.FormedOn is DateOnly formedOn)
                    artist.ReleaseDate = formedOn;

                ApplyMusicArtistLocalizedNames(artist, mbDetails.Name, mbDetails.OriginalName, mbDetails.SortName);

                mbImageUrl = mbDetails.ImageUrl;

                await SyncArtistMembersAsync(artist, mbDetails.Members, language, cancellationToken);
                await SoloArtistBirthDate.ApplyAsync(_context, artist, cancellationToken);
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

            var metadataTrack = MusicAlbumTrackMatchHelper.FindMetadataTrackBySlot(
                metadata.Tracks,
                track.DiscNumber,
                track.TrackNumber,
                mt => mt.DiscNumber,
                mt => mt.TrackNumber);

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

    private static void ApplyMusicArtistLocalizedNames(
        MusicArtist artist,
        string? name,
        string? originalName,
        string? sortName)
    {
        if (!artist.IsFieldLocked(nameof(MusicArtist.Title)) && !string.IsNullOrWhiteSpace(name))
            artist.Title = name;
        if (!artist.IsFieldLocked(nameof(MusicArtist.OriginalTitle)) && !string.IsNullOrWhiteSpace(originalName))
            artist.OriginalTitle = originalName;
        if (!artist.IsFieldLocked(nameof(MusicArtist.SortTitle)) && !string.IsNullOrWhiteSpace(sortName))
            artist.SortTitle = sortName;
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
        // MusicBrainz emits one "member of band" relation per instrument or period.
        DedupeArtistMembers(artist);
        if (members is not { Count: > 0 }) return;
        if (artist.PersonRoles.OfType<MusicArtistMember>().Any()) return;

        var addedPersonIds = new HashSet<Guid>();
        foreach (var member in CollapseArtistMembers(members))
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

            if (!addedPersonIds.Add(person.Id))
                continue;

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

    private void DedupeArtistMembers(MusicArtist artist)
    {
        var kept = new Dictionary<Guid, MusicArtistMember>();
        foreach (var role in artist.PersonRoles.OfType<MusicArtistMember>().ToList())
        {
            if (!kept.TryGetValue(role.PersonId, out var existing))
            {
                kept[role.PersonId] = role;
                continue;
            }

            existing.Role = MergeRoles(existing.Role, role.Role);
            if (role.IsActive)
                existing.IsActive = true;

            artist.PersonRoles.Remove(role);
            _context.PersonRoles.Remove(role);
        }
    }

    private static List<ExternalMusicArtistMember> CollapseArtistMembers(IReadOnlyList<ExternalMusicArtistMember> members) =>
        members
            .GroupBy(
                member => !string.IsNullOrWhiteSpace(member.MusicBrainzArtistId)
                    ? "mb:" + member.MusicBrainzArtistId
                    : "name:" + member.Name,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExternalMusicArtistMember
            {
                Name = group.Select(member => member.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Unknown",
                MusicBrainzArtistId = group.Select(member => member.MusicBrainzArtistId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)),
                Role = group.Select(member => member.Role).Aggregate(default(string?), MergeRoles),
                IsActive = group.Any(member => member.IsActive)
            })
            .ToList();

    private static string? MergeRoles(string? left, string? right)
    {
        var roles = new[] { left, right }
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .SelectMany(role => role!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return roles.Count == 0 ? null : string.Join(", ", roles);
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

    private static async Task<ExternalEpisodeMetadata?> ResolveEpisodeMetadataAsync(
        ISerieMetadataProvider provider,
        string providerId,
        int seasonNumber,
        int episodeNumber,
        string language,
        string? fallbackLanguage,
        SerieEpisode episode,
        bool incremental,
        CancellationToken cancellationToken)
    {
        var catalogMetadata = await provider.TryBuildEpisodeMetadataFromCatalogAsync(
            providerId,
            seasonNumber,
            episodeNumber,
            language,
            fallbackLanguage,
            cancellationToken);

        if (catalogMetadata is null)
        {
            return await provider.FetchEpisodeMetadataAsync(
                providerId,
                seasonNumber,
                episodeNumber,
                language,
                cancellationToken,
                fallbackLanguage);
        }

        // Catalog builds may omit cast. Fall back to a full episode fetch when roles are still empty.
        var needsCredits = !episode.IsFieldLocked(nameof(SerieEpisode.PersonRoles))
            && catalogMetadata.PersonRoles.Count == 0
            && (!incremental || episode.PersonRoles.Count == 0);

        if (!needsCredits)
            return catalogMetadata;

        var fullEpisodeMetadata = await provider.FetchEpisodeMetadataAsync(
            providerId,
            seasonNumber,
            episodeNumber,
            language,
            cancellationToken,
            fallbackLanguage);

        if (fullEpisodeMetadata is null)
            return catalogMetadata;

        if (fullEpisodeMetadata.PersonRoles.Count > 0)
        {
            catalogMetadata.PersonRoles.Clear();
            foreach (var role in fullEpisodeMetadata.PersonRoles)
                catalogMetadata.PersonRoles.Add(role);
        }

        if (catalogMetadata.ExternalIds.Count == 0 && fullEpisodeMetadata.ExternalIds.Count > 0)
        {
            foreach (var externalId in fullEpisodeMetadata.ExternalIds)
                catalogMetadata.ExternalIds.Add(externalId);
        }

        if (string.IsNullOrWhiteSpace(catalogMetadata.StillImageUrl)
            && !string.IsNullOrWhiteSpace(fullEpisodeMetadata.StillImageUrl))
        {
            return new ExternalEpisodeMetadata
            {
                RemoteId = catalogMetadata.RemoteId ?? fullEpisodeMetadata.RemoteId,
                EpisodeNumber = catalogMetadata.EpisodeNumber,
                SeasonNumber = catalogMetadata.SeasonNumber,
                Title = catalogMetadata.Title ?? fullEpisodeMetadata.Title,
                SortTitle = catalogMetadata.SortTitle ?? fullEpisodeMetadata.SortTitle,
                Overview = catalogMetadata.Overview ?? fullEpisodeMetadata.Overview,
                AirDate = catalogMetadata.AirDate ?? fullEpisodeMetadata.AirDate,
                Runtime = catalogMetadata.Runtime ?? fullEpisodeMetadata.Runtime,
                StillImageUrl = fullEpisodeMetadata.StillImageUrl,
                ExternalIds = catalogMetadata.ExternalIds,
                PersonRoles = catalogMetadata.PersonRoles,
                Ratings = catalogMetadata.Ratings.Count > 0 ? catalogMetadata.Ratings : fullEpisodeMetadata.Ratings
            };
        }

        return catalogMetadata;
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

    private async Task<string?> PreferHdEpisodeStillUrlAsync(
        SerieEpisode episode,
        string? canonStillUrl,
        ISerieMetadataProvider? enrichmentProvider,
        string? enrichmentExternalId,
        string? enrichmentProviderName,
        int seasonNumber,
        int episodeNumber,
        string language,
        string? fallbackLanguage,
        bool incremental,
        CancellationToken cancellationToken)
    {
        if (!incremental
            || enrichmentProvider is null
            || string.IsNullOrWhiteSpace(enrichmentExternalId)
            || !string.Equals(enrichmentProviderName, MetadataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase))
            return canonStillUrl;

        var missingTmdbId = !episode.ExternalIds.Any(e =>
            string.Equals(e.ProviderName, MetadataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase));

        MetadataImageUrlHelper.TryCreateRemoteUri(canonStillUrl, out var canonUri);
        var shouldFetchStill = string.IsNullOrWhiteSpace(canonStillUrl)
            || MetadataImageUrlHelper.ShouldReplaceEpisodeStillWithHdAlternate(null, null, canonUri);
        if (!shouldFetchStill && !missingTmdbId)
            return canonStillUrl;

        try
        {
            var tmdbMetadata = await enrichmentProvider.TryBuildEpisodeMetadataFromCatalogAsync(
                enrichmentExternalId,
                seasonNumber,
                episodeNumber,
                language,
                fallbackLanguage,
                cancellationToken);

            tmdbMetadata ??= await enrichmentProvider.FetchEpisodeMetadataAsync(
                enrichmentExternalId,
                seasonNumber,
                episodeNumber,
                language,
                cancellationToken,
                fallbackLanguage);

            if (tmdbMetadata is not null)
                SupplementalEpisodeMetadataResolver.MergeSupplementalExternalIds(episode, tmdbMetadata.ExternalIds);

            if (shouldFetchStill && !string.IsNullOrWhiteSpace(tmdbMetadata?.StillImageUrl))
                return tmdbMetadata.StillImageUrl;
        }
        catch (InvalidOperationException)
        {
        }

        return canonStillUrl;
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
