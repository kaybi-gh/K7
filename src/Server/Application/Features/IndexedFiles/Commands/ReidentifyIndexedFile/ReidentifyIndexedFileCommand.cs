using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

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
    IMediaLibraryAvailabilityService mediaLibraryAvailabilityService)
    : IRequestHandler<ReidentifyIndexedFileCommand>
{
    public async Task Handle(ReidentifyIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);

        var library = await context.Libraries.FindAsync([indexedFile.LibraryId], cancellationToken);
        Guard.Against.Null(library);

        var providerName = library.MetadataProviderName;

        if (indexedFile.MediaId.HasValue)
        {
            var oldMedia = await context.Medias
                .Include(m => m.IndexedFiles)
                .FirstOrDefaultAsync(m => m.Id == indexedFile.MediaId.Value, cancellationToken);

            if (oldMedia != null)
            {
                oldMedia.IndexedFiles?.Remove(indexedFile);
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
            await context.SaveChangesAsync(cancellationToken);
            await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);
            if (library.MediaType == LibraryMediaType.Music)
                await QueueAudioAnalysisForIndexedFileAsync(indexedFile.Id, library, cancellationToken);
            return;
        }

        if (context.Entry(indexedFile).State == EntityState.Detached)
        {
            context.IndexedFiles.Attach(indexedFile);
        }

        BaseMedia newMedia = library.MediaType switch
        {
            LibraryMediaType.Serie => new Serie(),
            LibraryMediaType.Music => new MusicAlbum(),
            _ => new Movie() { IndexedFiles = [indexedFile] }
        };

        context.Medias.Add(newMedia);

        switch (newMedia)
        {
            case Serie serie:
                await AttachIndexedFileToSerieAsync(serie, indexedFile, library, cancellationToken);
                break;
            case MusicAlbum album:
                await AttachIndexedFileToMusicAlbumAsync(album, indexedFile, library, cancellationToken);
                break;
        }

        newMedia.AddDomainEvent(new MediaCreatedEvent(newMedia));
        await context.SaveChangesAsync(cancellationToken);
        await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, cancellationToken);

        await QueueRefreshAsync(newMedia.Id, request.SelectedExternalId, providerName, library, cancellationToken);

        if (library.MediaType == LibraryMediaType.Music)
            await QueueAudioAnalysisForIndexedFileAsync(indexedFile.Id, library, cancellationToken);
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
            .LoadAsync(cancellationToken);

        var (seasonNumber, episodeNumber) = ResolveSerieEpisodeNumbers(indexedFile, library);

        var season = serie.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
        if (season is null)
        {
            season = new SerieSeason
            {
                SerieId = serie.Id,
                Serie = serie,
                SeasonNumber = seasonNumber,
                Title = seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}",
                SortTitle = MediaSortTitleHelper.Compute(seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}")
            };
            serie.Seasons.Add(season);
            context.Medias.Add(season);
        }

        var existingEpisode = season.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
        if (existingEpisode is not null)
        {
            existingEpisode.IndexedFiles.Add(indexedFile);
            return;
        }

        var episode = new SerieEpisode
        {
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

    private static (int SeasonNumber, int EpisodeNumber) ResolveSerieEpisodeNumbers(IndexedFile indexedFile, Library library)
    {
        if (indexedFile.Identification is null)
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
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = mediaId,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 1,
            ConcurrencyGroup = providerName
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
            Priority = BackgroundTaskPriority.Low,
            TargetEntityId = trackId.Value,
            TargetEntityTypeName = nameof(MusicTrack),
            MaxAttempts = 2,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }
}
