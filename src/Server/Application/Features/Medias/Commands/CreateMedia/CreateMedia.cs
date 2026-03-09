using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Server.Infrastructure.Configuration;
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
    private readonly IMetadataProvider<ExternalMovieMetadata> _metadataProvider;
    private readonly IMetadataProvider<ExternalMusicAlbumMetadata> _musicMetadataProvider;
    private readonly IAudioTagReader _audioTagReader;
    private readonly PathsConfiguration _pathsConfiguration;

    public CreateMediaCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IMetadataProvider<ExternalMovieMetadata> metadataProvider,
        IMetadataProvider<ExternalMusicAlbumMetadata> musicMetadataProvider,
        IAudioTagReader audioTagReader,
        IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _sender = sender;
        _metadataProvider = metadataProvider;
        _musicMetadataProvider = musicMetadataProvider;
        _audioTagReader = audioTagReader;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<Guid> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .FindAsync([request.IndexedFileId], cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        return request.MediaType switch
        {
            MediaType.Movie => await HandleMovie(indexedFile, cancellationToken),
            MediaType.MusicTrack => await HandleMusicTrack(indexedFile, cancellationToken),
            _ => throw new NotImplementedException($"Media type {request.MediaType} is not supported.")
        };
    }

    private async Task<Guid> HandleMovie(IndexedFile indexedFile, CancellationToken cancellationToken)
    {
        var metadataProviderExternalId = await _metadataProvider.SearchAsync(indexedFile.Identification!, cancellationToken);

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
                    Language = "fr",
                    FallbackLanguage = "en"
                },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = movie.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxRetryCount = 1
            }, cancellationToken);
        }

        return movie.Id;
    }

    private async Task<Guid> HandleMusicTrack(IndexedFile indexedFile, CancellationToken cancellationToken)
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
            // Set album artist
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
            var musicBrainzId = await _musicMetadataProvider.SearchAsync(albumIdentification, cancellationToken);
            if (!string.IsNullOrEmpty(musicBrainzId))
            {
                await _sender.Send(new CreateBackgroundTaskCommand()
                {
                    Request = new RefreshMediaMetadatasCommand()
                    {
                        MediaId = album.Id,
                        MetadataProviderExternalId = musicBrainzId,
                        Language = "en",
                        FallbackLanguage = "en"
                    },
                    Priority = BackgroundTaskPriority.Low,
                    TargetEntityId = album.Id,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxRetryCount = 3
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
            Bpm = tags?.Bpm,
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
            .Include(a => a.IndexedFiles)
            .FirstOrDefaultAsync(a =>
                a.Title == resolvedAlbumName &&
                a.IndexedFiles.Any(f => f.LibraryId == indexedFile.LibraryId),
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
                    album.Pictures.Add(new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        LocalPath = coverPath
                    });
                    return;
                }
            }
        }

        // Extract embedded cover art from audio tags
        if (tags?.CoverArtData is { Length: > 0 })
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

            album.Pictures.Add(new MetadataPicture
            {
                Type = MetadataPictureType.Poster,
                LocalPath = coverPath
            });
        }
    }
}
