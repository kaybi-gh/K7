using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Server.Application.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<Guid>
{
    public required MediaType MediaType { get; init; }
    public required Guid IndexedFileId { get; init; }
}

public class CreateMediaCommandHandler : IRequestHandler<CreateMediaCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAudioTagReader _audioTagReader;
    private readonly PathsConfiguration _pathsConfiguration;

    public CreateMediaCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IServiceProvider serviceProvider,
        IAudioTagReader audioTagReader,
        IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _sender = sender;
        _serviceProvider = serviceProvider;
        _audioTagReader = audioTagReader;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<Guid> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .FindAsync([request.IndexedFileId], cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var library = await _context.Libraries
            .FindAsync([indexedFile.LibraryId], cancellationToken);
        Guard.Against.NotFound(indexedFile.LibraryId, library);

        return request.MediaType switch
        {
            MediaType.Movie => await HandleMovie(indexedFile, library, cancellationToken),
            MediaType.MusicTrack => await HandleMusicTrack(indexedFile, library, cancellationToken),
            MediaType.SerieEpisode => await HandleSerieEpisode(indexedFile, library, cancellationToken),
            _ => throw new NotImplementedException($"Media type {request.MediaType} is not supported.")
        };
    }

    private async Task<Guid> HandleMovie(IndexedFile indexedFile, Library library, CancellationToken cancellationToken)
    {
        var metadataProvider = _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(library.MetadataProviderName);
        var metadataProviderExternalId = await metadataProvider.SearchAsync(indexedFile.Identification!, cancellationToken);

        if (string.IsNullOrEmpty(metadataProviderExternalId))
        {
            throw new NullReferenceException($"No result returned by metadataprovider for {indexedFile.Identification?.Title} - {indexedFile.Identification?.ReleaseYear}");
        }

        // Try to fetch existing Media
        var existingExternalId = await _context.ExternalIds
            .Include(x => x!.Media)
                .ThenInclude(x => x!.IndexedFiles)
            .FirstOrDefaultAsync(x => x.Value == metadataProviderExternalId, cancellationToken);

        if (existingExternalId != null
            && existingExternalId.Media != null
            && existingExternalId.Media.IndexedFiles != null)
        {
            existingExternalId.Media.IndexedFiles.Add(indexedFile);
            await _context.SaveChangesAsync(cancellationToken);
            return existingExternalId.Media.Id;
        }

        if (_context.Entry(indexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(indexedFile);
        }

        var movie = new Movie() { IndexedFiles = [indexedFile] };
        _context.Medias.Add(movie);
        movie.AddDomainEvent(new MediaCreatedEvent(movie));
        await _context.SaveChangesAsync(cancellationToken);

        if (metadataProviderExternalId != null)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new RefreshMediaMetadatasCommand()
                {
                    MediaId = movie.Id,
                    MetadataProviderExternalId = metadataProviderExternalId,
                    MetadataProviderName = library.MetadataProviderName!,
                    Language = "fr",
                    FallbackLanguage = "en"
                },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = movie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 1,
                ConcurrencyGroup = library.MetadataProviderName
            }, cancellationToken);
        }

        return movie.Id;
    }

    private async Task<Guid> HandleMusicTrack(IndexedFile indexedFile, Library library, CancellationToken cancellationToken)
    {
        var identification = indexedFile.Identification;
        Guard.Against.Null(identification);

        // Tags are the primary source of metadata, filesystem identification is fallback
        var tags = _audioTagReader.ReadTags(indexedFile.Path);

        if (_context.Entry(indexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(indexedFile);
        }

        var trackTitle = tags?.Title ?? identification.Title;
        var albumName = tags?.Album ?? identification.AlbumName;
        var trackNumber = tags?.TrackNumber ?? identification.TrackNumber;
        var releaseYear = tags?.Year != null ? new DateOnly(tags.Year.Value, 1, 1) : identification.ReleaseYear;
        var artistName = tags?.Artists.FirstOrDefault() ?? identification.ArtistName;
        var albumArtistName = tags?.AlbumArtists.FirstOrDefault() ?? artistName;

        // Find or create album
        var (album, isNewAlbum) = await FindOrCreateAlbum(indexedFile, albumName, releaseYear, cancellationToken);

        if (isNewAlbum)
        {
            if (!string.IsNullOrEmpty(albumArtistName))
            {
                var albumArtistPerson = await FindOrCreatePerson(albumArtistName, cancellationToken);
                album.PersonRoles.Add(new MusicArtist { PersonId = albumArtistPerson.Id, IsGuest = false });
            }

            // Set album genres from first track's tags
            if (tags?.Genres is { Count: > 0 })
            {
                foreach (var genre in tags.Genres) album.Genres.Add(genre);
            }

            // Attach cover art to album
            await TryAttachAlbumCover(indexedFile, album, tags, cancellationToken);

            // Queue MusicBrainz enrichment for the new album
            var albumIdentification = new MediaIdentification(album.Title ?? albumName ?? "Unknown Album")
            {
                AlbumName = album.Title,
                ArtistName = albumArtistName,
                ReleaseYear = releaseYear
            };
            var musicBrainzId = await _serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(library.MetadataProviderName)
                .SearchAsync(albumIdentification, cancellationToken);
            if (!string.IsNullOrEmpty(musicBrainzId))
            {
                await _sender.Send(new CreateBackgroundTaskCommand()
                {
                    Request = new RefreshMediaMetadatasCommand()
                    {
                        MediaId = album.Id,
                        MetadataProviderExternalId = musicBrainzId,
                        MetadataProviderName = library.MetadataProviderName,
                        Language = "en",
                        FallbackLanguage = "en"
                    },
                    Priority = BackgroundTaskPriority.Low,
                    TargetEntityId = album.Id,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxAttempts = 3,
                    ConcurrencyGroup = library.MetadataProviderName
                }, cancellationToken);
            }
        }

        // Create the track
        var track = new MusicTrack()
        {
            Title = trackTitle,
            TrackNumber = trackNumber,
            DiscNumber = tags?.DiscNumber,
            ReleaseDate = releaseYear,
            Lyrics = tags?.Lyrics,
            AlbumId = album.Id,
            IndexedFiles = [indexedFile]
        };

        if (tags?.Genres is { Count: > 0 })
        {
            foreach (var genre in tags.Genres) track.Genres.Add(genre);
        }

        // Look for .lrc lyrics file next to the audio file
        var lrcPath = Path.ChangeExtension(indexedFile.Path, ".lrc");
        if (File.Exists(lrcPath))
        {
            track.LyricsLrc = await File.ReadAllTextAsync(lrcPath, cancellationToken);
        }

        _context.Medias.Add(track);

        // Create track artist PersonRole
        if (!string.IsNullOrEmpty(artistName))
        {
            var trackArtistPerson = await FindOrCreatePerson(artistName, cancellationToken);
            track.PersonRoles.Add(new MusicArtist { PersonId = trackArtistPerson.Id, IsGuest = false });
        }

        track.AddDomainEvent(new MediaCreatedEvent(track));
        await _context.SaveChangesAsync(cancellationToken);

        return track.Id;
    }

    private async Task<(MusicAlbum Album, bool IsNew)> FindOrCreateAlbum(
        IndexedFile indexedFile, string? albumName, DateOnly? releaseYear, CancellationToken cancellationToken)
    {
        var resolvedAlbumName = albumName ?? "Unknown Album";

        var existingAlbum = await _context.Medias
            .OfType<MusicAlbum>()
            .FirstOrDefaultAsync(a =>
                a.Title == resolvedAlbumName &&
                a.Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == indexedFile.LibraryId)) &&
                (releaseYear == null || a.ReleaseDate == null || a.ReleaseDate == releaseYear),
                cancellationToken);

        if (existingAlbum != null)
        {
            return (existingAlbum, false);
        }

        var album = new MusicAlbum()
        {
            Title = resolvedAlbumName,
            ReleaseDate = releaseYear,
        };

        _context.Medias.Add(album);
        album.AddDomainEvent(new MediaCreatedEvent(album));
        await _context.SaveChangesAsync(cancellationToken);

        return (album, true);
    }

    private async Task<Person> FindOrCreatePerson(string name, CancellationToken cancellationToken)
    {
        var existing = await _context.Persons
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

        if (existing != null) return existing;

        var person = new Person { Name = name };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync(cancellationToken);

        return person;
    }

    private async Task TryAttachAlbumCover(
        IndexedFile indexedFile, MusicAlbum album, AudioTagData? tags, CancellationToken cancellationToken)
    {
        MetadataPicture? picture = null;

        // Look for cover image files in the audio file's directory
        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            string[] coverFileNames = ["cover.jpg", "cover.png", "folder.jpg", "folder.png", "front.jpg", "front.png"];
            foreach (var fileName in coverFileNames)
            {
                var coverPath = Path.Combine(directory, fileName);
                if (File.Exists(coverPath))
                {
                    picture = new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        LocalPath = coverPath
                    };
                    break;
                }
            }
        }

        // Extract embedded cover art from audio tags
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

            picture = new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                LocalPath = coverPath
            };
        }

        if (picture is null) return;

        album.Pictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand
            {
                MetadataPictureId = picture.Id
            },
            Priority = BackgroundTaskPriority.Lowest,
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            MaxAttempts = 3,
            ConcurrencyGroup = "image-processing"
        }, cancellationToken);
    }

    private async Task<Guid> HandleSerieEpisode(IndexedFile indexedFile, Library library, CancellationToken cancellationToken)
    {
        var identification = indexedFile.Identification;
        Guard.Against.Null(identification);
        Guard.Against.NullOrEmpty(identification.SeriesTitle);

        if (_context.Entry(indexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(indexedFile);
        }

        var metadataProvider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(library.MetadataProviderName);

        var (serie, isNewSerie, providerExternalId) = await FindOrCreateSerie(identification, metadataProvider, cancellationToken);

        // Resolve absolute number to season/episode if needed
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

        if (!seasonNumber.HasValue || !episodeNumber.HasValue)
        {
            throw new InvalidOperationException(
                $"Could not determine season/episode for {indexedFile.Path}");
        }

        // Find or create season
        await _context.Entry(serie).Collection(s => s.Seasons).LoadAsync(cancellationToken);
        var season = serie.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber.Value);
        if (season is null)
        {
            season = new SerieSeason
            {
                SerieId = serie.Id,
                Serie = serie,
                SeasonNumber = seasonNumber.Value,
                Title = seasonNumber.Value == 0 ? "Specials" : $"Season {seasonNumber.Value}"
            };
            serie.Seasons.Add(season);
            _context.Medias.Add(season);
        }

        // Find or create episode
        await _context.Entry(season).Collection(s => s.Episodes).LoadAsync(cancellationToken);
        var existingEpisode = season.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber.Value);

        if (existingEpisode is not null)
        {
            existingEpisode.IndexedFiles.Add(indexedFile);
            await _context.SaveChangesAsync(cancellationToken);
            return existingEpisode.Id;
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
            IndexedFiles = [indexedFile]
        };

        season.Episodes.Add(episode);
        _context.Medias.Add(episode);
        episode.AddDomainEvent(new MediaCreatedEvent(episode));
        await _context.SaveChangesAsync(cancellationToken);

        // Queue metadata refresh for the serie (only if newly created)
        if (isNewSerie && !string.IsNullOrEmpty(providerExternalId))
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = serie.Id,
                    MetadataProviderExternalId = providerExternalId,
                    MetadataProviderName = library.MetadataProviderName!,
                    Language = "fr",
                    FallbackLanguage = "en"
                },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = serie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 3,
                ConcurrencyGroup = library.MetadataProviderName
            }, cancellationToken);
        }

        return episode.Id;
    }

    private async Task<(Serie Serie, bool IsNew, string? ProviderExternalId)> FindOrCreateSerie(
        MediaIdentification identification, ISerieMetadataProvider metadataProvider, CancellationToken cancellationToken)
    {
        var seriesTitle = identification.SeriesTitle ?? identification.Title;

        // Check DB first by title to avoid redundant API calls for subsequent episodes
        var existingSerie = await _context.Medias
            .OfType<Serie>()
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync(s => s.Title == seriesTitle, cancellationToken);

        if (existingSerie is not null)
        {
            var externalId = existingSerie.ExternalIds
                .FirstOrDefault(e => e.ProviderName == metadataProvider.ProviderName)?.Value;
            return (existingSerie, false, externalId);
        }

        // Search provider
        var providerExternalId = await metadataProvider.SearchSerieAsync(identification, cancellationToken);

        // Double-check by external ID (title may differ from what's in DB after metadata refresh)
        if (!string.IsNullOrEmpty(providerExternalId))
        {
            var existingExternalId = await _context.ExternalIds
                .Include(x => x.Media)
                .FirstOrDefaultAsync(x => x.Value == providerExternalId
                    && x.ProviderName == metadataProvider.ProviderName
                    && x.Media is Serie, cancellationToken);

            if (existingExternalId?.Media is Serie existingSerieById)
            {
                return (existingSerieById, false, providerExternalId);
            }
        }

        // Create new serie
        var serie = new Serie
        {
            Title = seriesTitle,
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
