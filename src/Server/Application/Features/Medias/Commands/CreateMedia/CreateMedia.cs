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
            MediaType.Movie => await HandleMovieAsync(indexedFiles, library, cancellationToken),
            MediaType.MusicAlbum => await HandleMusicAlbumAsync(indexedFiles, library, cancellationToken),
            MediaType.Serie => await HandleSerieAsync(indexedFiles, library, cancellationToken),
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

    private async Task<Guid> HandleMovieAsync(List<IndexedFile> indexedFiles, Library library, CancellationToken cancellationToken)
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
                await AttachMovieIndexedFilesAsync(existingMovie, indexedFiles, cancellationToken);
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
                await AttachMovieIndexedFilesAsync(existingByTitle, indexedFiles, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        foreach (var file in indexedFiles.Where(f => movie.IndexedFiles.All(i => i.Id != f.Id)))
            movie.IndexedFiles.Add(file);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> HandleMusicAlbumAsync(List<IndexedFile> indexedFiles, Library library, CancellationToken cancellationToken)
    {
        var firstFile = indexedFiles.First();
        var firstTags = _audioTagReader.ReadTags(firstFile.Path);
        var firstIdentification = firstFile.Identification;
        Guard.Against.Null(firstIdentification);

        var albumName = firstTags?.Album ?? firstIdentification.AlbumName;
        var releaseYear = firstTags?.Year != null ? new DateOnly(firstTags.Year.Value, 1, 1) : firstIdentification.ReleaseYear;
        var albumArtistName = firstTags?.AlbumArtists.FirstOrDefault() ?? firstTags?.Artists.FirstOrDefault() ?? firstIdentification.ArtistName;

        // Search metadata provider for the album external ID upfront (like movies do)
        var albumIdentification = new MediaIdentification(albumName ?? "Unknown Album")
        {
            AlbumName = albumName,
            ArtistName = albumArtistName,
            ReleaseYear = releaseYear
        };
        var metadataProviderExternalId = await _serviceProvider
            .GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(library.MetadataProviderName)
            .SearchAsync(albumIdentification, library.MetadataLanguage, library.MetadataFallbackLanguage, cancellationToken);

        // Try to find existing album by provider ExternalId first (most reliable)
        MusicAlbum? existingAlbum = null;
        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            var existingExternalId = await _context.ExternalIds
                .Include(x => x.Media)
                .FirstOrDefaultAsync(x => x.Value == metadataProviderExternalId
                    && x.ProviderName == library.MetadataProviderName
                    && x.Media != null, cancellationToken);

            existingAlbum = existingExternalId?.Media as MusicAlbum;
        }

        // Fallback: find by title/artist/year (handles case where ExternalId not yet set)
        var (album, isNewAlbum) = existingAlbum is not null
            ? (existingAlbum, false)
            : await FindOrCreateAlbumAsync(firstFile, albumName, albumArtistName, releaseYear, cancellationToken);

        if (isNewAlbum)
        {
            if (!string.IsNullOrEmpty(albumArtistName))
            {
                var artist = await FindOrCreateMusicArtistAsync(albumArtistName, cancellationToken);
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
                await _sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new RefreshMediaMetadatasCommand
                    {
                        MediaId = album.Id,
                        MetadataProviderExternalId = metadataProviderExternalId,
                        MetadataProviderName = library.MetadataProviderName,
                        Language = library.MetadataLanguage,
                        FallbackLanguage = library.MetadataFallbackLanguage
                    },
                    TargetEntityId = album.Id,
                    TargetEntityTypeName = nameof(BaseMedia),
                    Lane = BackgroundTaskLane.Metadata,
                    MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName),
                    WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
                    TriggeredBy = BackgroundTaskTriggeredBy.System,
                    MaxAttempts = 3
                }, cancellationToken);
            }
        }

        if (!isNewAlbum)
        {
            await _context.Entry(album).Collection(a => a.Tracks)
                .Query()
                .Include(t => t.IndexedFiles)
                .LoadAsync(cancellationToken);
        }

        foreach (var indexedFile in indexedFiles)
        {
            if (_context.Entry(indexedFile).State == EntityState.Detached)
                _context.IndexedFiles.Attach(indexedFile);

            var identification = indexedFile.Identification;
            if (identification is null) continue;

            var tags = _audioTagReader.ReadTags(indexedFile.Path);
            var trackTitle = tags?.Title ?? identification.Title;
            var trackNumber = tags?.TrackNumber ?? identification.TrackNumber;

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
            if (!string.IsNullOrEmpty(artistName))
            {
                var trackArtist = await FindOrCreateMusicArtistAsync(artistName, cancellationToken);
                track.ArtistId = trackArtist.Id;
            }

            for (var i = 0; i < trackArtists.Count; i++)
            {
                var creditArtist = await FindOrCreateMusicArtistAsync(trackArtists[i], cancellationToken);
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
        await QueueAudioAnalysisForIndexedFilesAsync(indexedFiles, library, cancellationToken);
        return album.Id;
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

    private async Task<Guid> HandleSerieAsync(List<IndexedFile> indexedFiles, Library library, CancellationToken cancellationToken)
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
                library,
                cancellationToken);

        var resolveProviderName = matchedProviderName
            ?? MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName);
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
                    var formerMediaId = await DetachIndexedFileFromPreviousEpisodeAsync(indexedFile, cancellationToken);
                    if (formerMediaId is Guid formerId)
                        orphanTransfers.Add((formerId, existingEpisode.Id));

                    existingEpisode.IndexedFiles.Add(indexedFile);
                }

                continue;
            }

            var formerIdForNew = await DetachIndexedFileFromPreviousEpisodeAsync(indexedFile, cancellationToken);

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

            if (formerIdForNew is Guid formerNewId)
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

        var existingAlbum = await _context.Medias
            .OfType<MusicAlbum>()
            .FirstOrDefaultAsync(a =>
                a.Title == resolvedAlbumName &&
                (a.Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == indexedFile.LibraryId))
                    || !a.Tracks.Any(t => t.IndexedFiles.Any())) &&
                (artistName == null || (a.Artist != null && a.Artist.Title == artistName)) &&
                (releaseYear == null || a.ReleaseDate == null || a.ReleaseDate.Value.Year == releaseYear.Value.Year),
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

    private async Task<Domain.Entities.Medias.MusicArtist> FindOrCreateMusicArtistAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await _identityLookup.FindMusicArtistByNameAsync(name, cancellationToken);

        if (existing is not null) return existing;

        var artist = new Domain.Entities.Medias.MusicArtist
        {
            Title = name,
            SortTitle = MediaSortTitleHelper.Compute(name)
        };
        _context.Medias.Add(artist);
        await _context.SaveChangesAsync(cancellationToken);

        return artist;
    }

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
        Library library,
        CancellationToken cancellationToken)
    {
        var seriesTitle = identification.SeriesTitle ?? identification.Title;

        // Prefer provider external id (like movies), then title + year. Title alone wrongly merges
        // homonyms such as One Piece anime (1999) and live-action (2023).
        string? matchedProviderName = null;
        string? providerExternalId = null;
        foreach (var providerKey in SerieMetadataProviderCascade.ResolveSearchProviders(library.MetadataProviderName))
        {
            var provider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(providerKey);
            providerExternalId = await provider.SearchSerieAsync(
                identification,
                library.MetadataLanguage,
                library.MetadataFallbackLanguage,
                cancellationToken);
            if (!string.IsNullOrEmpty(providerExternalId))
            {
                matchedProviderName = provider.ProviderName;
                break;
            }
        }

        if (!string.IsNullOrEmpty(providerExternalId) && !string.IsNullOrEmpty(matchedProviderName))
        {
            var existingSerieById = await _identityLookup.FindMediaByExternalIdAsync<Serie>(
                matchedProviderName, providerExternalId, cancellationToken);

            if (existingSerieById is not null)
                return (existingSerieById, false, matchedProviderName, providerExternalId);
        }

        var existingSerie = await _identityLookup.FindSerieByTitleAndYearAsync(
            seriesTitle, identification.ReleaseYear, cancellationToken);

        if (existingSerie is not null)
        {
            var existing = PickSerieExternalId(existingSerie, library.MetadataProviderName);
            return (existingSerie, false, existing.ProviderName, existing.ExternalId);
        }

        var serie = new Serie
        {
            Title = seriesTitle,
            SortTitle = MediaSortTitleHelper.Compute(seriesTitle),
            ReleaseDate = identification.ReleaseYear
        };
        _context.Medias.Add(serie);
        serie.AddDomainEvent(new MediaCreatedEvent(serie));

        if (!string.IsNullOrEmpty(providerExternalId) && !string.IsNullOrEmpty(matchedProviderName))
        {
            serie.ExternalIds.Add(new ExternalId
            {
                ProviderName = matchedProviderName,
                Value = providerExternalId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (serie, true, matchedProviderName, providerExternalId);
    }

    private async Task<Guid?> DetachIndexedFileFromPreviousEpisodeAsync(
        IndexedFile indexedFile,
        CancellationToken cancellationToken)
    {
        if (indexedFile.MediaId is not Guid formerMediaId)
            return null;

        var formerEpisode = await _context.Medias
            .OfType<SerieEpisode>()
            .Include(e => e.IndexedFiles)
            .FirstOrDefaultAsync(e => e.Id == formerMediaId, cancellationToken);

        if (formerEpisode is null)
        {
            indexedFile.MediaId = null;
            return formerMediaId;
        }

        formerEpisode.IndexedFiles.Remove(indexedFile);
        indexedFile.MediaId = null;
        return formerMediaId;
    }

    private static (string? ProviderName, string? ExternalId) PickSerieExternalId(Serie serie, string? primaryProviderName)
    {
        var preferred = MetadataProviderHostMapper.NormalizeProviderName(primaryProviderName);
        var cascade = SerieMetadataProviderCascade.ResolveSearchProviders(primaryProviderName)
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
