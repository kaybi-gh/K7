using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;

public class ReidentifyIndexedFileCommand : IRequest
{
    public required Guid IndexedFileId { get; init; }
    public required string SelectedProvider { get; init; }
    public required string SelectedExternalId { get; init; }
}

public class ReidentifyIndexedFileCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IMediaLibraryAvailabilityService mediaLibraryAvailabilityService,
    IMusicIntelligenceCatalogReconciler musicIntelligenceCatalogReconciler,
    IPlaybackBookmarkService bookmarkService,
    ILogger<ReidentifyIndexedFileCommandHandler> logger)
    : IRequestHandler<ReidentifyIndexedFileCommand>
{
    public async Task Handle(ReidentifyIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);

        var library = await context.Libraries.FindAsync([indexedFile.LibraryId], cancellationToken);
        Guard.Against.Null(library);

        var providerName = MetadataProviderHostMapper.NormalizeProviderName(request.SelectedProvider);
        if (string.IsNullOrWhiteSpace(providerName) || providerName == MetadataProviderNames.Local)
            providerName = request.SelectedProvider.Trim();

        if (library.MediaType == LibraryMediaType.Music)
        {
            await HandleMusicAsync(indexedFile, library, providerName, request, cancellationToken);
            return;
        }

        Guid? formerMediaId = null;
        var formerWasSerieEpisode = false;
        var formerWasMovie = false;
        if (indexedFile.MediaId.HasValue)
        {
            var oldMedia = await context.Medias
                .Include(m => m.IndexedFiles)
                .FirstOrDefaultAsync(m => m.Id == indexedFile.MediaId.Value, cancellationToken);

            if (oldMedia != null)
            {
                oldMedia.IndexedFiles?.Remove(indexedFile);
            }

            if (oldMedia is SerieEpisode)
            {
                formerMediaId = indexedFile.MediaId;
                formerWasSerieEpisode = true;
            }
            else if (oldMedia is Movie)
            {
                formerMediaId = indexedFile.MediaId;
                formerWasMovie = true;
            }

            indexedFile.MediaId = null;
        }

        var existingExternalId = await context.ExternalIds
            .Include(x => x!.Media)
                .ThenInclude(x => x!.IndexedFiles)
            .FirstOrDefaultAsync(x => x.Value == request.SelectedExternalId && x.ProviderName == providerName, cancellationToken);

        if (existingExternalId?.Media is not null)
        {
            await AttachIndexedFileAsync(existingExternalId.Media, indexedFile, library, cancellationToken);
            await TransferAndCleanupFormerMediaAsync(
                formerMediaId,
                indexedFile.MediaId,
                formerWasSerieEpisode,
                formerWasMovie,
                cleanupMusic: false,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);
            return;
        }

        if (context.Entry(indexedFile).State == EntityState.Detached)
        {
            context.IndexedFiles.Attach(indexedFile);
        }

        BaseMedia newMedia = library.MediaType switch
        {
            LibraryMediaType.Serie => new Serie { Id = Guid.NewGuid() },
            _ => new Movie { Id = Guid.NewGuid(), IndexedFiles = [indexedFile] }
        };

        context.Medias.Add(newMedia);

        if (newMedia is Serie serie)
            await AttachIndexedFileToSerieAsync(serie, indexedFile, library, cancellationToken);

        newMedia.ExternalIds.Add(new ExternalId
        {
            ProviderName = providerName,
            Value = request.SelectedExternalId
        });

        newMedia.AddDomainEvent(new MediaCreatedEvent(newMedia));

        await TransferAndCleanupFormerMediaAsync(
            formerMediaId,
            indexedFile.MediaId,
            formerWasSerieEpisode,
            formerWasMovie,
            cleanupMusic: false,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);

        await QueueRefreshAsync(newMedia.Id, request.SelectedExternalId, providerName, library, cancellationToken);
    }

    private async Task HandleMusicAsync(
        IndexedFile indexedFile,
        Library library,
        string providerName,
        ReidentifyIndexedFileCommand request,
        CancellationToken cancellationToken)
    {
        MusicTrack? currentTrack = null;
        MusicAlbum? currentAlbum = null;

        if (indexedFile.MediaId is Guid mediaId)
        {
            currentTrack = await context.Medias
                .OfType<MusicTrack>()
                .Include(t => t.IndexedFiles)
                .Include(t => t.Album)
                    .ThenInclude(a => a.ExternalIds)
                .FirstOrDefaultAsync(t => t.Id == mediaId, cancellationToken);

            if (currentTrack is not null)
            {
                currentAlbum = currentTrack.Album;
            }
            else
            {
                currentAlbum = await context.Medias
                    .OfType<MusicAlbum>()
                    .Include(a => a.ExternalIds)
                    .Include(a => a.Tracks)
                        .ThenInclude(t => t.IndexedFiles)
                    .FirstOrDefaultAsync(a => a.Id == mediaId, cancellationToken);
            }
        }

        var existingExternalId = await context.ExternalIds
            .Include(x => x!.Media)
                .ThenInclude(x => x!.IndexedFiles)
            .FirstOrDefaultAsync(
                x => x.Value == request.SelectedExternalId && x.ProviderName == providerName,
                cancellationToken);

        if (existingExternalId?.Media is MusicAlbum targetAlbum)
        {
            if (currentAlbum is not null && currentAlbum.Id == targetAlbum.Id)
            {
                await QueueRefreshAsync(
                    targetAlbum.Id,
                    request.SelectedExternalId,
                    providerName,
                    library,
                    cancellationToken);
                return;
            }

            var formerTrackId = currentTrack?.Id;
            if (currentTrack is not null)
                currentTrack.IndexedFiles?.Remove(indexedFile);

            indexedFile.MediaId = null;
            await AttachIndexedFileToMusicAlbumAsync(targetAlbum, indexedFile, library, cancellationToken);

            var deletedMusic = await TransferAndCleanupFormerMediaAsync(
                formerTrackId,
                indexedFile.MediaId,
                formerWasSerieEpisode: false,
                formerWasMovie: false,
                cleanupMusic: true,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);

            if (deletedMusic)
                musicIntelligenceCatalogReconciler.RequestReconcile();

            await QueueAudioAnalysisForIndexedFileAsync(indexedFile.Id, library, cancellationToken);
            return;
        }

        // ExternalId is free: prefer in-place album identity update so track Guids stay stable.
        if (currentAlbum is not null)
        {
            if (!UpsertExternalId(currentAlbum, providerName, request.SelectedExternalId))
            {
                await QueueRefreshAsync(
                    currentAlbum.Id,
                    request.SelectedExternalId,
                    providerName,
                    library,
                    cancellationToken);
                return;
            }

            await context.SaveChangesAsync(cancellationToken);
            await QueueRefreshAsync(
                currentAlbum.Id,
                request.SelectedExternalId,
                providerName,
                library,
                cancellationToken);
            return;
        }

        if (context.Entry(indexedFile).State == EntityState.Detached)
            context.IndexedFiles.Attach(indexedFile);

        var album = new MusicAlbum { Id = Guid.NewGuid() };
        context.Medias.Add(album);
        await AttachIndexedFileToMusicAlbumAsync(album, indexedFile, library, cancellationToken);
        album.ExternalIds.Add(new ExternalId
        {
            ProviderName = providerName,
            Value = request.SelectedExternalId
        });
        album.AddDomainEvent(new MediaCreatedEvent(album));

        await context.SaveChangesAsync(cancellationToken);
        await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);
        await QueueRefreshAsync(album.Id, request.SelectedExternalId, providerName, library, cancellationToken);
        await QueueAudioAnalysisForIndexedFileAsync(indexedFile.Id, library, cancellationToken);
    }

    private async Task<bool> TransferAndCleanupFormerMediaAsync(
        Guid? formerMediaId,
        Guid? targetMediaId,
        bool formerWasSerieEpisode,
        bool formerWasMovie,
        bool cleanupMusic,
        CancellationToken cancellationToken)
    {
        if (formerMediaId is not Guid fromId || targetMediaId is not Guid toId)
            return false;

        await MediaUserStateTransferHelper.TransferAsync(
            context,
            fromId,
            toId,
            logger,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (formerWasSerieEpisode)
        {
            await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                context,
                fromId,
                logger,
                cancellationToken);
            return false;
        }

        if (formerWasMovie)
        {
            await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(
                context,
                fromId,
                logger,
                cancellationToken);
            return false;
        }

        if (cleanupMusic)
        {
            return await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(
                context,
                fromId,
                logger,
                cancellationToken);
        }

        return false;
    }

    private async Task AttachIndexedFileAsync(
        BaseMedia media,
        IndexedFile indexedFile,
        Library library,
        CancellationToken cancellationToken)
    {
        switch (media)
        {
            case Movie movie:
                movie.IndexedFiles ??= [];
                movie.IndexedFiles.Add(indexedFile);
                indexedFile.MediaId = movie.Id;
                break;

            case Serie serie:
                await AttachIndexedFileToSerieAsync(serie, indexedFile, library, cancellationToken);
                break;

            case MusicAlbum album:
                await AttachIndexedFileToMusicAlbumAsync(album, indexedFile, library, cancellationToken);
                break;

            default:
                media.IndexedFiles ??= [];
                media.IndexedFiles.Add(indexedFile);
                break;
        }
    }

    private async Task AttachIndexedFileToSerieAsync(
        Serie serie,
        IndexedFile indexedFile,
        Library library,
        CancellationToken cancellationToken)
    {
        await context.Entry(serie).Collection(s => s.Seasons)
            .Query()
            .Include(s => s.Episodes)
                .ThenInclude(e => e.IndexedFiles)
            .Include(s => s.Episodes)
                .ThenInclude(e => e.RemoteIndexedFiles)
            .LoadAsync(cancellationToken);

        var (seasonNumber, episodeNumber) = ResolveSerieEpisodeNumbers(indexedFile, library);

        var season = serie.Seasons.FirstOrDefault(s =>
            s.SeasonNumber == seasonNumber && context.Entry(s).State != EntityState.Deleted);
        if (season is null)
        {
            season = new SerieSeason
            {
                Id = Guid.NewGuid(),
                SerieId = serie.Id,
                Serie = serie,
                SeasonNumber = seasonNumber,
                Title = seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}",
                SortTitle = MediaSortTitleHelper.Compute(seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}")
            };
            serie.Seasons.Add(season);
            context.Medias.Add(season);
        }

        var existingEpisode = season.Episodes.FirstOrDefault(e =>
            e.EpisodeNumber == episodeNumber && context.Entry(e).State != EntityState.Deleted);
        if (existingEpisode is not null)
        {
            var becamePlayable = (existingEpisode.IndexedFiles is null || existingEpisode.IndexedFiles.Count == 0)
                && (existingEpisode.RemoteIndexedFiles is null || existingEpisode.RemoteIndexedFiles.Count == 0);
            existingEpisode.IndexedFiles ??= [];
            existingEpisode.IndexedFiles.Add(indexedFile);
            if (becamePlayable)
                await bookmarkService.RefreshSeriesBookmarksForSerieAsync(serie.Id, DateTime.UtcNow, cancellationToken);
            return;
        }

        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = episodeNumber,
            AbsoluteNumber = indexedFile.Identification?.AbsoluteNumber,
            Title = $"Episode {episodeNumber}",
            SortTitle = MediaSortTitleHelper.Compute($"Episode {episodeNumber}"),
            IndexedFiles = [indexedFile]
        };
        season.Episodes.Add(episode);
        context.Medias.Add(episode);
        episode.AddDomainEvent(new MediaCreatedEvent(episode));
    }

    private async Task AttachIndexedFileToMusicAlbumAsync(
        MusicAlbum album,
        IndexedFile indexedFile,
        Library library,
        CancellationToken cancellationToken)
    {
        await context.Entry(album).Collection(a => a.Tracks)
            .Query()
            .Include(t => t.IndexedFiles)
            .LoadAsync(cancellationToken);

        indexedFile.Identification ??= indexedFile.TryIdentifyMusicTrack(library, [indexedFile])
            ? indexedFile.Identification
            : null;

        var identification = indexedFile.Identification;
        var trackTitle = identification?.Title ?? indexedFile.Name;
        var trackNumber = identification?.TrackNumber;

        var existingTrack = album.Tracks.FirstOrDefault(t =>
            !t.IndexedFiles.Any()
            && (t.TrackNumber == trackNumber
                || string.Equals(t.Title, trackTitle, StringComparison.OrdinalIgnoreCase)))
            ?? album.Tracks.FirstOrDefault(t =>
                t.TrackNumber == trackNumber
                || string.Equals(t.Title, trackTitle, StringComparison.OrdinalIgnoreCase));

        if (existingTrack is not null)
        {
            existingTrack.IndexedFiles.Add(indexedFile);
            return;
        }

        var track = new MusicTrack
        {
            Title = trackTitle,
            SortTitle = MediaSortTitleHelper.Compute(trackTitle),
            TrackNumber = trackNumber,
            ReleaseDate = identification?.ReleaseYear,
            AlbumId = album.Id,
            Album = album,
            IndexedFiles = [indexedFile]
        };
        album.Tracks.Add(track);
        context.Medias.Add(track);
        track.AddDomainEvent(new MediaCreatedEvent(track));
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
            Value = value
        });
        return true;
    }

    private static (int SeasonNumber, int EpisodeNumber) ResolveSerieEpisodeNumbers(IndexedFile indexedFile, Library library)
    {
        // Always re-parse from the current path so renames are not ignored.
        indexedFile.TryIdentifySerieEpisode(library, [indexedFile]);

        var identification = indexedFile.Identification;
        var seasonNumber = identification?.SeasonNumber ?? 1;
        var episodeNumber = identification?.EpisodeNumber ?? identification?.AbsoluteNumber ?? 1;
        return (seasonNumber, episodeNumber);
    }

    private Task QueueRefreshAsync(
        Guid mediaId,
        string externalId,
        string providerName,
        Library library,
        CancellationToken cancellationToken) =>
        sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = mediaId,
                MetadataProviderExternalId = externalId,
                MetadataProviderName = providerName,
                Language = library.MetadataLanguage,
                FallbackLanguage = library.MetadataFallbackLanguage
            },
            TargetEntityId = mediaId,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(providerName),
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 1
        }, cancellationToken);

    private async Task QueueAudioAnalysisForIndexedFileAsync(
        Guid indexedFileId,
        Library library,
        CancellationToken cancellationToken)
    {
        if (!library.MusicAudioAnalysisEnabled)
            return;

        var trackId = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.Id == indexedFileId && f.MediaId != null)
            .Select(f => f.MediaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (trackId is null)
            return;

        var needsAnalysis = await context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .AnyAsync(t => t.Id == trackId && t.AudioAnalysis == null, cancellationToken);

        if (!needsAnalysis)
            return;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new AnalyzeMusicTrackAudioCommand { TrackId = trackId.Value },
            TargetEntityId = trackId.Value,
            TargetEntityTypeName = nameof(MusicTrack),
            Lane = BackgroundTaskLane.MediaAnalysis,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 2
        }, cancellationToken);
    }
}
