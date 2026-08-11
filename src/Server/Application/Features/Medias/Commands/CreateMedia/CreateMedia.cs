using K7.Server.Application.Common;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<Guid>
{
    public required MediaType MediaType { get; init; }
    public required IList<Guid> IndexedFileIds { get; init; }
    public required Guid LibraryId { get; init; }

    /// <summary>
    /// Optional map of indexed-file id to the media it was previously linked to.
    /// Used after bulk rematch, which clears <see cref="IndexedFile.MediaId"/> before CreateMedia runs
    /// so folder sibling consensus cannot re-stick, while still allowing user-data transfer A→B.
    /// </summary>
    public Dictionary<Guid, Guid>? FormerMediaIdsByIndexedFileId { get; init; }
}

public class CreateMediaCommandHandler : IRequestHandler<CreateMediaCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAudioTagReader _audioTagReader;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly IMediaMetadataTagSyncService _metadataTagSyncService;
    private readonly MediaIdentityLookupService _identityLookup;
    private readonly IMediaIdentityLock _identityLock;
    private readonly IMediaLibraryAvailabilityService _mediaLibraryAvailabilityService;
    private readonly SerieMetadataIdentityService _serieIdentityService;
    private readonly MusicMetadataIdentityService _musicIdentityService;
    private readonly IMusicIntelligenceCatalogReconciler _musicIntelligenceCatalogReconciler;
    private readonly ILogger<CreateMediaCommandHandler> _logger;

    public CreateMediaCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IServiceProvider serviceProvider,
        IAudioTagReader audioTagReader,
        IOptions<PathsConfiguration> pathsConfiguration,
        IMediaMetadataTagSyncService metadataTagSyncService,
        MediaIdentityLookupService identityLookup,
        IMediaIdentityLock identityLock,
        IMediaLibraryAvailabilityService mediaLibraryAvailabilityService,
        SerieMetadataIdentityService serieIdentityService,
        MusicMetadataIdentityService musicIdentityService,
        IMusicIntelligenceCatalogReconciler musicIntelligenceCatalogReconciler,
        ILogger<CreateMediaCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _serviceProvider = serviceProvider;
        _audioTagReader = audioTagReader;
        _pathsConfiguration = pathsConfiguration.Value;
        _metadataTagSyncService = metadataTagSyncService;
        _identityLookup = identityLookup;
        _identityLock = identityLock;
        _mediaLibraryAvailabilityService = mediaLibraryAvailabilityService;
        _serieIdentityService = serieIdentityService;
        _musicIdentityService = musicIdentityService;
        _musicIntelligenceCatalogReconciler = musicIntelligenceCatalogReconciler;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        var library = await _context.Libraries
            .FindAsync([request.LibraryId], cancellationToken);
        Guard.Against.NotFound(request.LibraryId, library);

        var indexedFiles = await _context.IndexedFiles
            .Where(f => request.IndexedFileIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        // Serialize on the media identity: finding an existing media then creating it is a
        // check-then-insert, and nothing in the database prevents a duplicate. Two commands for the same
        // album, serie or movie can legitimately be queued at once (two scan batches, a watcher flush
        // racing a scheduled scan), and both would otherwise miss the lookup and insert.
        var identityKey = MediaIdentityKey.Build(request.MediaType, request.LibraryId, indexedFiles);
        await using var identityGuard = await _identityLock.AcquireAsync(identityKey, cancellationToken);

        var mediaId = request.MediaType switch
        {
            MediaType.Movie => await HandleMovieAsync(
                indexedFiles, library, request.FormerMediaIdsByIndexedFileId, cancellationToken),
            MediaType.MusicAlbum => await HandleMusicAlbumAsync(
                indexedFiles, library, request.FormerMediaIdsByIndexedFileId, cancellationToken),
            MediaType.Serie => await HandleSerieAsync(
                indexedFiles, library, request.FormerMediaIdsByIndexedFileId, cancellationToken),
            _ => throw new NotImplementedException($"Media type {request.MediaType} is not supported.")
        };

        // FileIndexer rebuilds availability before background CreateMedia tasks finish.
        // Keep the denormalized table current so library-group browse (which filters on it)
        // sees media as soon as IndexedFiles are linked.
        await _mediaLibraryAvailabilityService.EnsureFromIndexedFilesAsync(
            request.LibraryId,
            request.IndexedFileIds.ToList(),
            cancellationToken);

        return mediaId;
    }

    private async Task<Guid> HandleMovieAsync(
        List<IndexedFile> indexedFiles,
        Library library,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        CancellationToken cancellationToken)
    {
        var primaryFile = indexedFiles.First();
        Guard.Against.NullOrEmpty(primaryFile.Path);

        var identification = primaryFile.Identification;
        Guard.Against.Null(identification);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(library.MetadataProviderName);
        var metadataProviderExternalId = await metadataProvider.SearchAsync(
            identification,
            library.MetadataLanguage,
            library.MetadataFallbackLanguage,
            cancellationToken);

        // Match music/serie: provider miss is not fatal - create from local identification.
        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            var existingExternalId = await _context.ExternalIds
                .Include(x => x.Media)
                    .ThenInclude(x => x!.IndexedFiles)
                .FirstOrDefaultAsync(x => x.Value == metadataProviderExternalId
                    && x.ProviderName == library.MetadataProviderName
                    && x.Media is Movie, cancellationToken);

            if (existingExternalId?.Media is Movie existingMovie)
            {
                await AttachMovieIndexedFilesAsync(
                    existingMovie,
                    indexedFiles,
                    formerMediaIdsByIndexedFileId,
                    cancellationToken);
                return existingMovie.Id;
            }
        }

        if (identification.Title is not null)
        {
            var existingByTitle = await _context.Medias
                .OfType<Movie>()
                .Include(m => m.IndexedFiles)
                .FirstOrDefaultAsync(m =>
                    m.Title == identification.Title
                    && m.ReleaseDate == identification.ReleaseYear, cancellationToken);

            if (existingByTitle is not null)
            {
                await AttachMovieIndexedFilesAsync(
                    existingByTitle,
                    indexedFiles,
                    formerMediaIdsByIndexedFileId,
                    cancellationToken);
                return existingByTitle.Id;
            }
        }

        foreach (var file in indexedFiles.Where(f => _context.Entry(f).State == EntityState.Detached))
            _context.IndexedFiles.Attach(file);

        var title = identification.Title ?? primaryFile.Name;
        var movie = new Movie
        {
            IndexedFiles = indexedFiles,
            Title = title,
            SortTitle = MediaSortTitleHelper.Compute(title),
            ReleaseDate = identification.ReleaseYear
        };

        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            movie.ExternalIds.Add(new ExternalId
            {
                ProviderName = library.MetadataProviderName!,
                Value = metadataProviderExternalId
            });
        }

        _context.Medias.Add(movie);
        movie.AddDomainEvent(new MediaCreatedEvent(movie));
        await _context.SaveChangesAsync(cancellationToken);

        await TransferAndCleanupFormerMediasAsync(
            indexedFiles,
            movie.Id,
            formerMediaIdsByIndexedFileId,
            cleanupMovies: true,
            cleanupSerieEpisodes: false,
            cancellationToken);

        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = movie.Id,
                    MetadataProviderExternalId = metadataProviderExternalId,
                    MetadataProviderName = library.MetadataProviderName!,
                    Language = library.MetadataLanguage,
                    FallbackLanguage = library.MetadataFallbackLanguage
                },
                TargetEntityId = movie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                Lane = BackgroundTaskLane.Metadata,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName),
                WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 3
            }, cancellationToken);
        }

        return movie.Id;
    }

    private async Task AttachMovieIndexedFilesAsync(
        Movie movie,
        List<IndexedFile> indexedFiles,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        CancellationToken cancellationToken)
    {
        foreach (var file in indexedFiles.Where(f => movie.IndexedFiles.All(i => i.Id != f.Id)))
            movie.IndexedFiles.Add(file);

        await _context.SaveChangesAsync(cancellationToken);

        await TransferAndCleanupFormerMediasAsync(
            indexedFiles,
            movie.Id,
            formerMediaIdsByIndexedFileId,
            cleanupMovies: true,
            cleanupSerieEpisodes: false,
            cancellationToken);
    }

    private async Task<Guid> HandleMusicAlbumAsync(
        List<IndexedFile> indexedFiles,
        Library library,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        CancellationToken cancellationToken)
    {
        var firstFile = indexedFiles.First();
        var firstTags = _audioTagReader.ReadTags(firstFile.Path);
        var firstIdentification = firstFile.Identification;
        Guard.Against.Null(firstIdentification);

        var albumName = firstTags?.Album ?? firstIdentification.AlbumName;
        var releaseYear = firstTags?.Year != null ? new DateOnly(firstTags.Year.Value, 1, 1) : firstIdentification.ReleaseYear;
        var albumArtistName = firstTags?.AlbumArtists.FirstOrDefault() ?? firstTags?.Artists.FirstOrDefault() ?? firstIdentification.ArtistName;

        var albumIdentification = BuildMusicAlbumIdentification(
            albumName,
            albumArtistName,
            releaseYear,
            firstTags,
            firstIdentification);

        var identityMatch = await _musicIdentityService.ResolveAlbumAsync(
            albumIdentification,
            library.MetadataProviderName,
            library.MetadataLanguage,
            library.MetadataFallbackLanguage,
            cancellationToken);

        var metadataProviderExternalId = identityMatch?.ExternalId;
        var albumProviderName = identityMatch?.ProviderName ?? library.MetadataProviderName;
        var albumArtistMbid = identityMatch?.ArtistMusicBrainzId
            ?? albumIdentification.MusicBrainzAlbumArtistId
            ?? albumIdentification.MusicBrainzArtistId;

        var formerTrackIds = indexedFiles
            .Select(f => ResolveFormerMediaId(f, formerMediaIdsByIndexedFileId))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var formerTracks = formerTrackIds.Count == 0
            ? []
            : await _context.Medias
                .OfType<MusicTrack>()
                .Include(t => t.Album)
                    .ThenInclude(a => a.ExternalIds)
                .Include(t => t.IndexedFiles)
                .Where(t => formerTrackIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

        var formerAlbumIdByTrackId = formerTracks.ToDictionary(t => t.Id, t => t.AlbumId);

        // Try to find existing album by provider ExternalId first (most reliable)
        MusicAlbum? existingAlbumByExternalId = null;
        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            var existingExternalId = await _context.ExternalIds
                .Include(x => x.Media)
                .FirstOrDefaultAsync(x => x.Value == metadataProviderExternalId
                    && x.ProviderName == albumProviderName
                    && x.Media != null, cancellationToken);

            existingAlbumByExternalId = existingExternalId?.Media as MusicAlbum;
        }

        var preferredFormerAlbum = PickPreferredFormerAlbum(formerTracks);
        var canReuseFormerAlbum = preferredFormerAlbum is not null
            && (existingAlbumByExternalId is null
                || existingAlbumByExternalId.Id == preferredFormerAlbum.Id);

        MusicAlbum album;
        var isNewAlbum = false;
        var shouldRefreshPreservedAlbum = false;

        if (canReuseFormerAlbum)
        {
            album = preferredFormerAlbum!;
            if (!string.IsNullOrEmpty(metadataProviderExternalId))
                shouldRefreshPreservedAlbum = UpsertExternalId(album, albumProviderName, metadataProviderExternalId);
        }
        else if (existingAlbumByExternalId is not null)
        {
            album = existingAlbumByExternalId;
        }
        else
        {
            (album, isNewAlbum) = await FindOrCreateAlbumAsync(
                firstFile, albumName, albumArtistName, releaseYear, cancellationToken);
            if (!isNewAlbum && !string.IsNullOrEmpty(metadataProviderExternalId))
                shouldRefreshPreservedAlbum = UpsertExternalId(album, albumProviderName, metadataProviderExternalId);
        }

        if (isNewAlbum)
        {
            if (!string.IsNullOrEmpty(albumArtistName))
            {
                var artist = await FindOrCreateMusicArtistAsync(albumArtistName, albumArtistMbid, cancellationToken);
                album.ArtistId = artist.Id;
            }

            if (firstTags?.Genres is { Count: > 0 })
            {
                await _metadataTagSyncService.ApplyTagsAsync(
                    album,
                    MetadataTagBuilder.FromGenres(album, firstTags.Genres),
                    cancellationToken);
            }

            await TryAttachAlbumCoverAsync(firstFile, album, firstTags, cancellationToken);

            if (!string.IsNullOrEmpty(metadataProviderExternalId))
            {
                UpsertExternalId(album, albumProviderName, metadataProviderExternalId);
                if (!string.IsNullOrWhiteSpace(identityMatch?.PreferredReleaseId))
                    UpsertExternalId(album, "musicbrainz-release", identityMatch.PreferredReleaseId);

                await QueueAlbumRefreshAsync(
                    album.Id,
                    metadataProviderExternalId,
                    albumProviderName,
                    library,
                    cancellationToken);
            }
        }
        else if (shouldRefreshPreservedAlbum && !string.IsNullOrEmpty(metadataProviderExternalId))
        {
            if (!string.IsNullOrWhiteSpace(identityMatch?.PreferredReleaseId))
                UpsertExternalId(album, "musicbrainz-release", identityMatch.PreferredReleaseId);

            await QueueAlbumRefreshAsync(
                album.Id,
                metadataProviderExternalId,
                albumProviderName,
                library,
                cancellationToken);
        }

        if (!isNewAlbum)
        {
            await _context.Entry(album).Collection(a => a.Tracks)
                .Query()
                .Include(t => t.IndexedFiles)
                .LoadAsync(cancellationToken);
        }

        var vacatedAlbumIds = new HashSet<Guid>();

        foreach (var indexedFile in indexedFiles)
        {
            if (_context.Entry(indexedFile).State == EntityState.Detached)
                _context.IndexedFiles.Attach(indexedFile);

            var identification = indexedFile.Identification;
            if (identification is null) continue;

            var tags = _audioTagReader.ReadTags(indexedFile.Path);
            var trackTitle = tags?.Title ?? identification.Title;
            var trackNumber = tags?.TrackNumber ?? identification.TrackNumber;

            var formerTrackId = ResolveFormerMediaId(indexedFile, formerMediaIdsByIndexedFileId);
            var formerTrack = formerTrackId is Guid fid
                ? formerTracks.FirstOrDefault(t => t.Id == fid)
                : null;

            // Rematch / reidentify: reparent the former track so AudioMuse and OpenSubsonic Guids stay stable.
            if (formerTrack is not null)
            {
                if (formerAlbumIdByTrackId.TryGetValue(formerTrack.Id, out var previousAlbumId)
                    && previousAlbumId != album.Id)
                {
                    vacatedAlbumIds.Add(previousAlbumId);
                    formerTrack.AlbumId = album.Id;
                    formerTrack.Album = album;
                    if (!album.Tracks.Any(t => t.Id == formerTrack.Id))
                        album.Tracks.Add(formerTrack);
                }

                if (!formerTrack.IndexedFiles.Any(f => f.Id == indexedFile.Id))
                    formerTrack.IndexedFiles.Add(indexedFile);

                if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    formerTrack.Title = trackTitle;
                    formerTrack.SortTitle = MediaSortTitleHelper.Compute(trackTitle);
                }

                if (trackNumber is not null)
                    formerTrack.TrackNumber = trackNumber;

                continue;
            }

            // Re-link to existing orphan track (no IndexedFiles) when album was reused
            if (!isNewAlbum)
            {
                var existingTrack = album.Tracks.FirstOrDefault(t =>
                    !t.IndexedFiles.Any()
                    && (t.TrackNumber == trackNumber
                        || string.Equals(t.Title, trackTitle, StringComparison.OrdinalIgnoreCase)));

                if (existingTrack is not null)
                {
                    existingTrack.IndexedFiles.Add(indexedFile);
                    continue;
                }
            }

            var track = new MusicTrack
            {
                Title = trackTitle,
                SortTitle = MediaSortTitleHelper.Compute(trackTitle),
                TrackNumber = trackNumber,
                DiscNumber = tags?.DiscNumber,
                ReleaseDate = tags?.Year != null ? new DateOnly(tags.Year.Value, 1, 1) : identification.ReleaseYear,
                Lyrics = tags?.Lyrics,
                AlbumId = album.Id,
                IndexedFiles = [indexedFile]
            };

            if (tags?.Genres is { Count: > 0 })
            {
                await _metadataTagSyncService.ApplyTagsAsync(
                    track,
                    MetadataTagBuilder.FromGenres(track, tags.Genres),
                    cancellationToken);
            }

            var lrcPath = Path.ChangeExtension(indexedFile.Path, ".lrc");
            if (File.Exists(lrcPath))
                track.LyricsLrc = await File.ReadAllTextAsync(lrcPath, cancellationToken);

            _context.Medias.Add(track);

            var trackArtists = tags?.Artists ?? [];
            var artistName = trackArtists.FirstOrDefault() ?? identification.ArtistName;
            var trackArtistMbid = string.IsNullOrEmpty(artistName)
                || string.Equals(artistName, albumArtistName, StringComparison.OrdinalIgnoreCase)
                ? albumArtistMbid ?? tags?.MusicBrainzArtistId
                : tags?.MusicBrainzArtistId;
            if (!string.IsNullOrEmpty(artistName))
            {
                var trackArtist = await FindOrCreateMusicArtistAsync(artistName, trackArtistMbid, cancellationToken);
                track.ArtistId = trackArtist.Id;
            }

            for (var i = 0; i < trackArtists.Count; i++)
            {
                var creditMbid = i == 0 ? trackArtistMbid : null;
                var creditArtist = await FindOrCreateMusicArtistAsync(trackArtists[i], creditMbid, cancellationToken);
                track.ArtistCredits.Add(new MusicArtistCredit
                {
                    MusicArtistId = creditArtist.Id,
                    MediaId = track.Id,
                    IsGuest = creditArtist.Id != album.ArtistId,
                    Order = i
                });
            }

            track.AddDomainEvent(new MediaCreatedEvent(track));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var deletedMusic = await TransferAndCleanupFormerMediaIdsAsync(
            indexedFiles,
            formerMediaIdsByIndexedFileId,
            cleanupMovies: false,
            cleanupSerieEpisodes: false,
            cleanupMusicTracks: true,
            cancellationToken);

        foreach (var vacatedAlbumId in vacatedAlbumIds)
        {
            if (await MusicOrphanCleanupHelper.TryDeleteAlbumIfOrphanAsync(
                    _context,
                    vacatedAlbumId,
                    _logger,
                    cancellationToken: cancellationToken))
            {
                deletedMusic = true;
            }
        }

        if (deletedMusic)
            await _context.SaveChangesAsync(cancellationToken);

        if (deletedMusic)
            _musicIntelligenceCatalogReconciler.RequestReconcile();

        await QueueAudioAnalysisForIndexedFilesAsync(indexedFiles, library, cancellationToken);
        return album.Id;
    }

    private Task QueueAlbumRefreshAsync(
        Guid albumId,
        string metadataProviderExternalId,
        string metadataProviderName,
        Library library,
        CancellationToken cancellationToken) =>
        _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = albumId,
                MetadataProviderExternalId = metadataProviderExternalId,
                MetadataProviderName = metadataProviderName,
                Language = library.MetadataLanguage,
                FallbackLanguage = library.MetadataFallbackLanguage
            },
            TargetEntityId = albumId,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(metadataProviderName),
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 3
        }, cancellationToken);

    private static MusicAlbum? PickPreferredFormerAlbum(IReadOnlyList<MusicTrack> formerTracks)
    {
        if (formerTracks.Count == 0)
            return null;

        return formerTracks
            .GroupBy(t => t.AlbumId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First().Album)
            .FirstOrDefault();
    }

    private static bool UpsertExternalId(BaseMedia media, string providerName, string value)
    {
        media.ExternalIds ??= [];
        var existing = media.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (string.Equals(existing.Value, value, StringComparison.Ordinal))
                return false;

            existing.Value = value;
            return true;
        }

        media.ExternalIds.Add(new ExternalId
        {
            ProviderName = providerName,
            Value = value,
            MediaId = media.Id
        });
        return true;
    }

    private async Task QueueAudioAnalysisForIndexedFilesAsync(
        List<IndexedFile> indexedFiles,
        Library library,
        CancellationToken cancellationToken)
    {
        if (!library.MusicAudioAnalysisEnabled)
            return;

        var fileIds = indexedFiles.Select(f => f.Id).ToList();
        var trackIds = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id) && f.MediaId != null)
            .Select(f => f.MediaId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var tracksNeedingAnalysis = await _context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => trackIds.Contains(t.Id) && t.AudioAnalysis == null)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var trackId in tracksNeedingAnalysis)
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new AnalyzeMusicTrackAudioCommand { TrackId = trackId },
                TargetEntityId = trackId,
                TargetEntityTypeName = nameof(MusicTrack),
                Lane = BackgroundTaskLane.MediaAnalysis,
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 2
            }, cancellationToken);
        }
    }

    private async Task<Guid> HandleSerieAsync(
        List<IndexedFile> indexedFiles,
        Library library,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        CancellationToken cancellationToken)
    {
        foreach (var directoryGroup in indexedFiles.GroupBy(
                     f => PathHelper.GetContainingDirectoryPath(f.Path),
                     StringComparer.OrdinalIgnoreCase))
            SerieIdentificationConsensus.ApplyDirectoryTitleConsensus(directoryGroup.ToList());

        var firstIdentification = indexedFiles.First().Identification;
        Guard.Against.Null(firstIdentification);
        Guard.Against.NullOrEmpty(firstIdentification.SeriesTitle);

        var folderSerie = await TryResolveSerieFromFolderSiblingsAsync(indexedFiles, library, cancellationToken);
        var (serie, _, matchedProviderName, providerExternalId) = folderSerie
            ?? await FindOrCreateSerieAsync(
                firstIdentification,
                indexedFiles.Select(f => f.Identification).Where(i => i is not null).Cast<MediaIdentification>().ToList(),
                library,
                cancellationToken);

        var resolveProviderName = matchedProviderName
            ?? serie.NumberingProviderName
            ?? MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName);
        if (SerieMetadataProviderCascade.IsAuto(resolveProviderName))
            resolveProviderName = serie.NumberingProviderName ?? MetadataProviderNames.Tmdb;
        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(resolveProviderName);

        // Load the full season+episode tree once - no more per-episode lazy loads
        await _context.Entry(serie).Collection(s => s.Seasons)
            .Query()
            .Include(s => s.Episodes)
                .ThenInclude(e => e.IndexedFiles)
            .LoadAsync(cancellationToken);

        var hasNewEpisodes = false;
        var orphanTransfers = new List<(Guid FromEpisodeId, Guid ToEpisodeId)>();

        foreach (var indexedFile in indexedFiles)
        {
            if (_context.Entry(indexedFile).State == EntityState.Detached)
                _context.IndexedFiles.Attach(indexedFile);

            var identification = indexedFile.Identification;
            if (identification is null) continue;

            var seasonNumber = identification.SeasonNumber;
            var episodeNumber = identification.EpisodeNumber;

            if (seasonNumber is null && episodeNumber is null
                && identification.AbsoluteNumber.HasValue
                && !string.IsNullOrEmpty(providerExternalId))
            {
                var resolved = await metadataProvider.ResolveAbsoluteEpisodeAsync(
                    providerExternalId, identification.AbsoluteNumber.Value, cancellationToken);
                if (resolved.HasValue)
                {
                    seasonNumber = resolved.Value.Season;
                    episodeNumber = resolved.Value.Episode;
                }
                else
                {
                    seasonNumber = 1;
                    episodeNumber = identification.AbsoluteNumber.Value;
                }
            }

            if (!seasonNumber.HasValue || !episodeNumber.HasValue) continue;

            var season = serie.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber.Value);
            if (season is null)
            {
                season = new SerieSeason
                {
                    Id = Guid.NewGuid(),
                    SerieId = serie.Id,
                    Serie = serie,
                    SeasonNumber = seasonNumber.Value,
                    Title = seasonNumber.Value == 0 ? "Specials" : $"Season {seasonNumber.Value}",
                    SortTitle = MediaSortTitleHelper.Compute(seasonNumber.Value == 0 ? "Specials" : $"Season {seasonNumber.Value}")
                };
                serie.Seasons.Add(season);
                _context.Medias.Add(season);
            }

            var existingEpisode = season.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber.Value);
            if (existingEpisode is not null)
            {
                if (indexedFile.MediaId != existingEpisode.Id)
                {
                    var formerMediaId = await DetachIndexedFileFromPreviousEpisodeAsync(
                        indexedFile,
                        formerMediaIdsByIndexedFileId,
                        cancellationToken);
                    if (formerMediaId is Guid formerId && formerId != existingEpisode.Id)
                        orphanTransfers.Add((formerId, existingEpisode.Id));

                    existingEpisode.IndexedFiles.Add(indexedFile);
                }

                continue;
            }

            var formerIdForNew = await DetachIndexedFileFromPreviousEpisodeAsync(
                indexedFile,
                formerMediaIdsByIndexedFileId,
                cancellationToken);

            var episode = new SerieEpisode
            {
                Id = Guid.NewGuid(),
                SerieId = serie.Id,
                Serie = serie,
                SeasonId = season.Id,
                Season = season,
                EpisodeNumber = episodeNumber.Value,
                AbsoluteNumber = identification.AbsoluteNumber,
                Title = $"Episode {episodeNumber.Value}",
                SortTitle = MediaSortTitleHelper.Compute($"Episode {episodeNumber.Value}"),
                IndexedFiles = [indexedFile]
            };
            season.Episodes.Add(episode);
            _context.Medias.Add(episode);
            episode.AddDomainEvent(new MediaCreatedEvent(episode));
            hasNewEpisodes = true;

            if (formerIdForNew is Guid formerNewId && formerNewId != episode.Id)
                orphanTransfers.Add((formerNewId, episode.Id));
        }

        foreach (var (fromEpisodeId, toEpisodeId) in orphanTransfers)
        {
            await MediaUserStateTransferHelper.TransferAsync(
                _context,
                fromEpisodeId,
                toEpisodeId,
                _logger,
                cancellationToken);
        }

        if (orphanTransfers.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        foreach (var (fromEpisodeId, _) in orphanTransfers)
        {
            await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                _context,
                fromEpisodeId,
                _logger,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (hasNewEpisodes && !string.IsNullOrEmpty(providerExternalId) && !string.IsNullOrEmpty(matchedProviderName))
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = serie.Id,
                    MetadataProviderExternalId = providerExternalId,
                    MetadataProviderName = matchedProviderName,
                    Language = library.MetadataLanguage,
                    FallbackLanguage = library.MetadataFallbackLanguage
                },
                TargetEntityId = serie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                Lane = BackgroundTaskLane.Metadata,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(matchedProviderName),
                WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 3
            }, cancellationToken);
        }

        return serie.Id;
    }

    private async Task<(MusicAlbum Album, bool IsNew)> FindOrCreateAlbumAsync(
        IndexedFile indexedFile, string? albumName, string? artistName, DateOnly? releaseYear, CancellationToken cancellationToken)
    {
        var resolvedAlbumName = albumName ?? "Unknown Album";

        // Compare by calendar-year date range, not ReleaseDate.Year: Npgsql translates .Year to
        // date_part(...)::int and throws 22003 (dtoi4) on out-of-range / infinity dates.
        DateOnly? yearStart = releaseYear is { } year ? new DateOnly(year.Year, 1, 1) : null;
        DateOnly? yearEnd = releaseYear is { } y ? new DateOnly(y.Year, 12, 31) : null;

        var existingAlbum = await _context.Medias
            .OfType<MusicAlbum>()
            .FirstOrDefaultAsync(a =>
                a.Title == resolvedAlbumName &&
                (a.Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == indexedFile.LibraryId))
                    || !a.Tracks.Any(t => t.IndexedFiles.Any())) &&
                (artistName == null || (a.Artist != null && a.Artist.Title == artistName)) &&
                (yearStart == null
                    || a.ReleaseDate == null
                    || (a.ReleaseDate >= yearStart && a.ReleaseDate <= yearEnd)),
                cancellationToken);

        if (existingAlbum is not null)
            return (existingAlbum, false);

        var album = new MusicAlbum
        {
            Title = resolvedAlbumName,
            SortTitle = MediaSortTitleHelper.Compute(resolvedAlbumName),
            ReleaseDate = releaseYear
        };
        _context.Medias.Add(album);
        album.AddDomainEvent(new MediaCreatedEvent(album));
        await _context.SaveChangesAsync(cancellationToken);

        return (album, true);
    }

    private async Task<Person> FindOrCreatePersonAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await _context.Persons
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

        if (existing is not null) return existing;

        var person = new Person { Name = name };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync(cancellationToken);

        return person;
    }

    private async Task<Domain.Entities.Medias.MusicArtist> FindOrCreateMusicArtistAsync(
        string name,
        string? musicBrainzId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(musicBrainzId))
        {
            var byExternalId = await _context.Medias.OfType<Domain.Entities.Medias.MusicArtist>()
                .FirstOrDefaultAsync(a => a.ExternalIds.Any(e =>
                    e.ProviderName == MetadataProviderNames.MusicBrainz
                    && e.Value == musicBrainzId), cancellationToken);
            if (byExternalId is not null)
                return byExternalId;
        }

        var existing = await FindMusicArtistByNameAsync(name, cancellationToken);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(musicBrainzId)
                && !existing.ExternalIds.Any(e => e.ProviderName == MetadataProviderNames.MusicBrainz))
            {
                existing.ExternalIds.Add(new ExternalId
                {
                    ProviderName = MetadataProviderNames.MusicBrainz,
                    Value = musicBrainzId,
                    MediaId = existing.Id
                });
            }

            return existing;
        }

        var artist = new Domain.Entities.Medias.MusicArtist
        {
            Title = name,
            SortTitle = MediaSortTitleHelper.Compute(name)
        };
        _context.Medias.Add(artist);

        if (!string.IsNullOrWhiteSpace(musicBrainzId))
        {
            artist.ExternalIds.Add(new ExternalId
            {
                ProviderName = MetadataProviderNames.MusicBrainz,
                Value = musicBrainzId,
                MediaId = artist.Id
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return artist;
    }

    private async Task<Domain.Entities.Medias.MusicArtist?> FindMusicArtistByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var normalized = MusicArtistNameNormalizer.NormalizeForMatch(name);
        var candidates = await _context.Medias.OfType<Domain.Entities.Medias.MusicArtist>()
            .Include(a => a.ExternalIds)
            .Where(a => a.Title == name
                || (normalized != null && a.Title == normalized))
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(a => MusicArtistNameNormalizer.NamesMatch(a.Title, name));
    }

    private static MediaIdentification BuildMusicAlbumIdentification(
        string? albumName,
        string? albumArtistName,
        DateOnly? releaseYear,
        AudioTagData? tags,
        MediaIdentification fallback)
    {
        return new MediaIdentification(albumName ?? "Unknown Album")
        {
            AlbumName = albumName,
            ArtistName = albumArtistName,
            ReleaseYear = releaseYear,
            MusicBrainzReleaseId = FirstNonEmpty(tags?.MusicBrainzReleaseId, fallback.MusicBrainzReleaseId),
            MusicBrainzReleaseGroupId = FirstNonEmpty(tags?.MusicBrainzReleaseGroupId, fallback.MusicBrainzReleaseGroupId),
            MusicBrainzArtistId = FirstNonEmpty(tags?.MusicBrainzArtistId, fallback.MusicBrainzArtistId),
            MusicBrainzAlbumArtistId = FirstNonEmpty(tags?.MusicBrainzAlbumArtistId, fallback.MusicBrainzAlbumArtistId),
            MusicBrainzRecordingId = FirstNonEmpty(tags?.MusicBrainzRecordingId, fallback.MusicBrainzRecordingId)
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private async Task TryAttachAlbumCoverAsync(
        IndexedFile indexedFile, MusicAlbum album, AudioTagData? tags, CancellationToken cancellationToken)
    {
        MetadataPicture? picture = null;

        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            string[] coverFileNames = ["cover.jpg", "cover.png", "folder.jpg", "folder.png", "front.jpg", "front.png"];
            foreach (var fileName in coverFileNames)
            {
                var coverPath = Path.Combine(directory, fileName);
                if (File.Exists(coverPath))
                {
                    picture = new MetadataPicture { Type = MetadataPictureType.Cover, LocalPath = coverPath };
                    break;
                }
            }
        }

        if (picture is null && tags?.CoverArtData is { Length: > 0 })
        {
            var extension = tags.CoverArtMimeType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                _ => ".jpg"
            };
            var coverDirectory = Path.Combine(_pathsConfiguration.Metadatas, "medias", album.Id.ToString());
            Directory.CreateDirectory(coverDirectory);
            var coverPath = Path.Combine(coverDirectory, $"cover{extension}");
            await File.WriteAllBytesAsync(coverPath, tags.CoverArtData, cancellationToken);
            picture = new MetadataPicture { Type = MetadataPictureType.Cover, LocalPath = coverPath };
        }

        if (picture is null) return;

        album.Pictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            Lane = BackgroundTaskLane.ImageProcessing,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 3
        }, cancellationToken);
    }

    private async Task<(Serie Serie, bool IsNew, string? ProviderName, string? ProviderExternalId)?> TryResolveSerieFromFolderSiblingsAsync(
        List<IndexedFile> indexedFiles,
        Library library,
        CancellationToken cancellationToken)
    {
        var directories = indexedFiles
            .Select(f => PathHelper.GetContainingDirectoryPath(f.Path))
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (directories.Count != 1)
            return null;

        var directory = directories[0]!;
        var parentDirectoryName = Path.GetFileName(directory);
        var excludeIds = indexedFiles.Select(f => f.Id).ToList();

        // ParentDirectory stores the folder name only (e.g. "Season 01"), so prefilter by name
        // then confirm the full containing path to avoid merging different series trees.
        var siblingRows = await (
            from file in _context.IndexedFiles.AsNoTracking()
            where file.LibraryId == library.Id
                && file.ParentDirectory == parentDirectoryName
                && file.MediaId != null
                && !excludeIds.Contains(file.Id)
            join episode in _context.Medias.OfType<SerieEpisode>().AsNoTracking()
                on file.MediaId equals episode.Id
            select new { file.Path, episode.SerieId }
        ).ToListAsync(cancellationToken);

        var siblingSerieCounts = siblingRows
            .Where(r => PathHelper.IsInContainingDirectory(r.Path, directory))
            .GroupBy(r => r.SerieId)
            .Select(g => new { SerieId = g.Key, Count = g.Count() })
            .ToList();

        if (siblingSerieCounts.Count == 0)
            return null;

        Guid? chosenSerieId = null;
        if (siblingSerieCounts.Count == 1)
        {
            chosenSerieId = siblingSerieCounts[0].SerieId;
        }
        else
        {
            var total = siblingSerieCounts.Sum(x => x.Count);
            var top = siblingSerieCounts.OrderByDescending(x => x.Count).First();
            if (top.Count >= 2 && top.Count * 100 >= total * 80)
                chosenSerieId = top.SerieId;
        }

        if (chosenSerieId is null)
            return null;

        var serie = await _context.Medias
            .OfType<Serie>()
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync(s => s.Id == chosenSerieId.Value, cancellationToken);

        if (serie is null)
            return null;

        var (providerName, externalId) = PickSerieExternalId(serie, library.MetadataProviderName);
        return (serie, false, providerName, externalId);
    }

    private async Task<(Serie Serie, bool IsNew, string? ProviderName, string? ProviderExternalId)> FindOrCreateSerieAsync(
        MediaIdentification identification,
        IReadOnlyList<MediaIdentification> fileIdentifications,
        Library library,
        CancellationToken cancellationToken)
    {
        var seriesTitle = identification.SeriesTitle ?? identification.Title;

        // Prefer provider external id (like movies), then title + year. Title alone wrongly merges
        // homonyms such as One Piece anime (1999) and live-action (2023).
        string? matchedProviderName = null;
        string? providerExternalId = null;
        IReadOnlyList<(string ProviderName, string ExternalId)> crosswalkIds = [];

        var match = await _serieIdentityService.ResolveAsync(
            identification,
            library.MetadataProviderName,
            fileIdentifications.Count > 0 ? fileIdentifications : [identification],
            library.MetadataLanguage,
            library.MetadataFallbackLanguage,
            cancellationToken);

        if (match is not null)
        {
            matchedProviderName = match.NumberingProviderName;
            providerExternalId = match.NumberingExternalId;
            crosswalkIds = match.ExternalIds;
        }

        if (!string.IsNullOrEmpty(providerExternalId) && !string.IsNullOrEmpty(matchedProviderName))
        {
            foreach (var (providerName, externalId) in crosswalkIds.DefaultIfEmpty((matchedProviderName, providerExternalId)))
            {
                var existingSerieById = await _identityLookup.FindMediaByExternalIdAsync<Serie>(
                    providerName, externalId, cancellationToken);
                if (existingSerieById is not null)
                {
                    MergeSerieExternalIds(existingSerieById, crosswalkIds);
                    if (string.IsNullOrWhiteSpace(existingSerieById.NumberingProviderName)
                        || SerieMetadataProviderCascade.IsAuto(library.MetadataProviderName))
                    {
                        existingSerieById.NumberingProviderName = matchedProviderName;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    return (existingSerieById, false, matchedProviderName, providerExternalId);
                }
            }
        }

        var existingSerie = await _identityLookup.FindSerieByTitleAndYearAsync(
            seriesTitle, identification.ReleaseYear, cancellationToken);

        if (existingSerie is not null)
        {
            MergeSerieExternalIds(existingSerie, crosswalkIds);
            if (!string.IsNullOrWhiteSpace(matchedProviderName)
                && (string.IsNullOrWhiteSpace(existingSerie.NumberingProviderName)
                    || SerieMetadataProviderCascade.IsAuto(library.MetadataProviderName)))
            {
                existingSerie.NumberingProviderName = matchedProviderName;
            }

            await _context.SaveChangesAsync(cancellationToken);
            var existing = PickSerieExternalId(existingSerie, library.MetadataProviderName);
            return (existingSerie, false, existing.ProviderName ?? matchedProviderName, existing.ExternalId ?? providerExternalId);
        }

        var serie = new Serie
        {
            Title = seriesTitle,
            SortTitle = MediaSortTitleHelper.Compute(seriesTitle),
            ReleaseDate = identification.ReleaseYear,
            NumberingProviderName = matchedProviderName
        };
        _context.Medias.Add(serie);
        serie.AddDomainEvent(new MediaCreatedEvent(serie));

        MergeSerieExternalIds(serie, crosswalkIds.Count > 0
            ? crosswalkIds
            : !string.IsNullOrEmpty(providerExternalId) && !string.IsNullOrEmpty(matchedProviderName)
                ? [(matchedProviderName, providerExternalId)]
                : []);

        await _context.SaveChangesAsync(cancellationToken);
        return (serie, true, matchedProviderName, providerExternalId);
    }

    private static void MergeSerieExternalIds(
        Serie serie,
        IReadOnlyList<(string ProviderName, string ExternalId)> externalIds)
    {
        foreach (var (providerName, externalId) in externalIds)
        {
            if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(externalId))
                continue;

            if (serie.ExternalIds.Any(e =>
                    string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Value, externalId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (serie.ExternalIds.Any(e =>
                    string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            serie.ExternalIds.Add(new ExternalId
            {
                ProviderName = providerName,
                Value = externalId
            });
        }
    }

    private async Task<Guid?> DetachIndexedFileFromPreviousEpisodeAsync(
        IndexedFile indexedFile,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        CancellationToken cancellationToken)
    {
        var formerMediaId = ResolveFormerMediaId(indexedFile, formerMediaIdsByIndexedFileId);
        if (formerMediaId is null)
            return null;

        if (indexedFile.MediaId is Guid currentMediaId)
        {
            var formerEpisode = await _context.Medias
                .OfType<SerieEpisode>()
                .Include(e => e.IndexedFiles)
                .FirstOrDefaultAsync(e => e.Id == currentMediaId, cancellationToken);

            if (formerEpisode is not null)
                formerEpisode.IndexedFiles.Remove(indexedFile);

            indexedFile.MediaId = null;
        }

        return formerMediaId;
    }

    private async Task TransferAndCleanupFormerMediasAsync(
        IReadOnlyList<IndexedFile> indexedFiles,
        Guid targetMediaId,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        bool cleanupMovies,
        bool cleanupSerieEpisodes,
        CancellationToken cancellationToken)
    {
        var transfers = new HashSet<(Guid From, Guid To)>();
        foreach (var file in indexedFiles)
        {
            var formerId = ResolveFormerMediaId(file, formerMediaIdsByIndexedFileId);
            if (formerId is null || formerId.Value == targetMediaId)
                continue;

            transfers.Add((formerId.Value, targetMediaId));
        }

        if (transfers.Count == 0)
            return;

        foreach (var (fromMediaId, toMediaId) in transfers)
        {
            await MediaUserStateTransferHelper.TransferAsync(
                _context,
                fromMediaId,
                toMediaId,
                _logger,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var fromMediaId in transfers.Select(t => t.From).Distinct())
        {
            if (cleanupMovies)
            {
                await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                    _context,
                    fromMediaId,
                    _logger,
                    cancellationToken);
            }

            if (cleanupSerieEpisodes)
            {
                await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                    _context,
                    fromMediaId,
                    _logger,
                    cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TransferAndCleanupFormerMediaIdsAsync(
        IReadOnlyList<IndexedFile> indexedFiles,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId,
        bool cleanupMovies,
        bool cleanupSerieEpisodes,
        bool cleanupMusicTracks,
        CancellationToken cancellationToken)
    {
        var transfers = new HashSet<(Guid From, Guid To)>();
        foreach (var file in indexedFiles)
        {
            if (file.MediaId is not Guid targetMediaId)
                continue;

            var formerId = ResolveFormerMediaId(file, formerMediaIdsByIndexedFileId);
            if (formerId is null || formerId.Value == targetMediaId)
                continue;

            transfers.Add((formerId.Value, targetMediaId));
        }

        if (transfers.Count == 0)
            return false;

        foreach (var (fromMediaId, toMediaId) in transfers)
        {
            await MediaUserStateTransferHelper.TransferAsync(
                _context,
                fromMediaId,
                toMediaId,
                _logger,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var deletedMusic = false;
        foreach (var fromMediaId in transfers.Select(t => t.From).Distinct())
        {
            if (cleanupMovies)
            {
                await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                    _context,
                    fromMediaId,
                    _logger,
                    cancellationToken);
            }

            if (cleanupSerieEpisodes)
            {
                await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                    _context,
                    fromMediaId,
                    _logger,
                    cancellationToken);
            }

            if (cleanupMusicTracks)
            {
                if (await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(
                        _context,
                        fromMediaId,
                        _logger,
                        cancellationToken))
                {
                    deletedMusic = true;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return deletedMusic;
    }

    private static Guid? ResolveFormerMediaId(
        IndexedFile indexedFile,
        IReadOnlyDictionary<Guid, Guid>? formerMediaIdsByIndexedFileId)
    {
        if (formerMediaIdsByIndexedFileId is not null
            && formerMediaIdsByIndexedFileId.TryGetValue(indexedFile.Id, out var mappedId)
            && mappedId != Guid.Empty)
        {
            return mappedId;
        }

        return indexedFile.MediaId;
    }

    private static (string? ProviderName, string? ExternalId) PickSerieExternalId(Serie serie, string? primaryProviderName)
    {
        var preferred = !string.IsNullOrWhiteSpace(serie.NumberingProviderName)
            ? MetadataProviderHostMapper.NormalizeProviderName(serie.NumberingProviderName)
            : MetadataProviderHostMapper.NormalizeProviderName(primaryProviderName);

        if (SerieMetadataProviderCascade.IsAuto(preferred))
            preferred = MetadataProviderNames.Tmdb;

        var cascade = SerieMetadataProviderCascade.ResolveSearchProviders(
                SerieMetadataProviderCascade.IsAuto(primaryProviderName)
                    ? MetadataProviderNames.Auto
                    : primaryProviderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var match = serie.ExternalIds
            .Where(e => cascade.Contains(MetadataProviderNames.Normalize(
                MetadataProviderHostMapper.NormalizeProviderName(e.ProviderName))))
            .OrderByDescending(e => string.Equals(
                MetadataProviderNames.Normalize(MetadataProviderHostMapper.NormalizeProviderName(e.ProviderName)),
                preferred,
                StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return match is null ? (null, null) : (match.ProviderName, match.Value);
    }
}
