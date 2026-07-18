using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
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

    public CreateMediaCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IServiceProvider serviceProvider,
        IAudioTagReader audioTagReader,
        IOptions<PathsConfiguration> pathsConfiguration,
        IMediaMetadataTagSyncService metadataTagSyncService,
        MediaIdentityLookupService identityLookup)
    {
        _context = context;
        _sender = sender;
        _serviceProvider = serviceProvider;
        _audioTagReader = audioTagReader;
        _pathsConfiguration = pathsConfiguration.Value;
        _metadataTagSyncService = metadataTagSyncService;
        _identityLookup = identityLookup;
    }

    public async Task<Guid> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        var library = await _context.Libraries
            .FindAsync([request.LibraryId], cancellationToken);
        Guard.Against.NotFound(request.LibraryId, library);

        var indexedFiles = await _context.IndexedFiles
            .Where(f => request.IndexedFileIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        return request.MediaType switch
        {
            MediaType.Movie => await HandleMovieAsync(indexedFiles, library, cancellationToken),
            MediaType.MusicAlbum => await HandleMusicAlbumAsync(indexedFiles, library, cancellationToken),
            MediaType.Serie => await HandleSerieAsync(indexedFiles, library, cancellationToken),
            _ => throw new NotImplementedException($"Media type {request.MediaType} is not supported.")
        };
    }

    private async Task<Guid> HandleMovieAsync(List<IndexedFile> indexedFiles, Library library, CancellationToken cancellationToken)
    {
        var primaryFile = indexedFiles.First();
        Guard.Against.NullOrEmpty(primaryFile.Path);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(library.MetadataProviderName);
        var metadataProviderExternalId = await metadataProvider.SearchAsync(primaryFile.Identification!, cancellationToken);

        if (string.IsNullOrEmpty(metadataProviderExternalId))
        {
            throw new NullReferenceException($"No result returned by metadata provider for {primaryFile.Identification?.Title} - {primaryFile.Identification?.ReleaseYear}");
        }

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

        var identification = primaryFile.Identification;
        if (identification?.Title is not null)
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

        var movie = new Movie { IndexedFiles = indexedFiles };
        movie.ExternalIds.Add(new ExternalId
        {
            ProviderName = library.MetadataProviderName!,
            Value = metadataProviderExternalId
        });
        _context.Medias.Add(movie);
        movie.AddDomainEvent(new MediaCreatedEvent(movie));
        await _context.SaveChangesAsync(cancellationToken);

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
            Priority = BackgroundTaskPriority.Low,
            TargetEntityId = movie.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 3,
            ConcurrencyGroup = library.MetadataProviderName
        }, cancellationToken);

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
            .SearchAsync(albumIdentification, cancellationToken);

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
                    Priority = BackgroundTaskPriority.Low,
                    TargetEntityId = album.Id,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxAttempts = 3,
                    ConcurrencyGroup = library.MetadataProviderName
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
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = trackId,
                TargetEntityTypeName = nameof(MusicTrack),
                MaxAttempts = 2,
                ConcurrencyGroup = "ffmpeg"
            }, cancellationToken);
        }
    }

    private async Task<Guid> HandleSerieAsync(List<IndexedFile> indexedFiles, Library library, CancellationToken cancellationToken)
    {
        var firstIdentification = indexedFiles.First().Identification;
        Guard.Against.Null(firstIdentification);
        Guard.Against.NullOrEmpty(firstIdentification.SeriesTitle);

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(library.MetadataProviderName);
        var (serie, _, providerExternalId) = await FindOrCreateSerieAsync(firstIdentification, metadataProvider, cancellationToken);

        // Load the full season+episode tree once - no more per-episode lazy loads
        await _context.Entry(serie).Collection(s => s.Seasons)
            .Query()
            .Include(s => s.Episodes)
            .LoadAsync(cancellationToken);

        var hasNewEpisodes = false;

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
                existingEpisode.IndexedFiles.Add(indexedFile);
                continue;
            }

            var episode = new SerieEpisode
            {
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
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (hasNewEpisodes && !string.IsNullOrEmpty(providerExternalId))
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = serie.Id,
                    MetadataProviderExternalId = providerExternalId,
                    MetadataProviderName = library.MetadataProviderName!,
                    Language = library.MetadataLanguage,
                    FallbackLanguage = library.MetadataFallbackLanguage
                },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = serie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 3,
                ConcurrencyGroup = library.MetadataProviderName
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
            Priority = BackgroundTaskPriority.Lowest,
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            MaxAttempts = 3,
            ConcurrencyGroup = "image-processing"
        }, cancellationToken);
    }

    private async Task<(Serie Serie, bool IsNew, string? ProviderExternalId)> FindOrCreateSerieAsync(
        MediaIdentification identification, ISerieMetadataProvider metadataProvider, CancellationToken cancellationToken)
    {
        var seriesTitle = identification.SeriesTitle ?? identification.Title;

        var existingSerie = await _identityLookup.FindSerieByTitleAsync(seriesTitle, cancellationToken);

        if (existingSerie is not null)
        {
            var externalId = existingSerie.ExternalIds
                .FirstOrDefault(e => e.ProviderName == metadataProvider.ProviderName)?.Value;
            return (existingSerie, false, externalId);
        }

        var providerExternalId = await metadataProvider.SearchSerieAsync(identification, cancellationToken);

        if (!string.IsNullOrEmpty(providerExternalId))
        {
            var existingSerieById = await _identityLookup.FindMediaByExternalIdAsync<Serie>(
                metadataProvider.ProviderName, providerExternalId, cancellationToken);

            if (existingSerieById is not null)
                return (existingSerieById, false, providerExternalId);
        }

        var serie = new Serie
        {
            Title = seriesTitle,
            SortTitle = MediaSortTitleHelper.Compute(seriesTitle),
            ReleaseDate = identification.ReleaseYear
        };
        _context.Medias.Add(serie);
        serie.AddDomainEvent(new MediaCreatedEvent(serie));

        if (!string.IsNullOrEmpty(providerExternalId))
        {
            serie.ExternalIds.Add(new ExternalId
            {
                ProviderName = metadataProvider.ProviderName,
                Value = providerExternalId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (serie, true, providerExternalId);
    }
}
