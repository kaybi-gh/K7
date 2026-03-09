using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;

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

    public CreateMediaCommandHandler(IApplicationDbContext context, ISender sender, IMetadataProvider<ExternalMovieMetadata> metadataProvider)
    {
        _context = context;
        _sender = sender;
        _metadataProvider = metadataProvider;
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

        if (_context.Entry(indexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(indexedFile);
        }

        // Find or create album
        var album = await FindOrCreateAlbum(indexedFile, identification, cancellationToken);

        // Create the track
        var track = new MusicTrack()
        {
            Title = identification.Title,
            TrackNumber = identification.TrackNumber,
            ReleaseDate = identification.ReleaseYear,
            AlbumId = album.Id,
            IndexedFiles = [indexedFile]
        };

        _context.Medias.Add(track);
        track.AddDomainEvent(new MediaCreatedEvent(track));
        await _context.SaveChangesAsync(cancellationToken);

        return track.Id;
    }

    private async Task<MusicAlbum> FindOrCreateAlbum(IndexedFile indexedFile, MediaIdentification identification, CancellationToken cancellationToken)
    {
        var albumName = identification.AlbumName ?? "Unknown Album";

        // Try to find existing album in the same library by matching album name and directory
        var existingAlbum = await _context.Medias
            .OfType<MusicAlbum>()
            .Include(a => a.IndexedFiles)
            .FirstOrDefaultAsync(a =>
                a.Title == albumName &&
                a.IndexedFiles.Any(f => f.LibraryId == indexedFile.LibraryId),
                cancellationToken);

        if (existingAlbum != null)
        {
            return existingAlbum;
        }

        var album = new MusicAlbum()
        {
            Title = albumName,
            ReleaseDate = identification.ReleaseYear,
        };

        _context.Medias.Add(album);
        album.AddDomainEvent(new MediaCreatedEvent(album));
        await _context.SaveChangesAsync(cancellationToken);

        return album;
    }
}
