using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Playlists.Commands.UploadPlaylistCover;

public record UploadPlaylistCoverCommand : IRequest<Guid>
{
    public required Guid PlaylistId { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public Guid? SourcePictureId { get; init; }
}

public class UploadPlaylistCoverCommandHandler(
    IApplicationDbContext context,
    ICoverPictureUploadService coverUpload,
    IUser currentUser) : IRequestHandler<UploadPlaylistCoverCommand, Guid>
{
    public async Task<Guid> Handle(UploadPlaylistCoverCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.CoverPicture)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        if (playlist.CoverPicture is not null)
        {
            context.MetadataPictures.Remove(playlist.CoverPicture);
        }

        var localPath = await ResolveLocalPathAsync(request, playlist.Id, cancellationToken);

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            PlaylistId = playlist.Id,
            LocalPath = localPath
        };

        context.MetadataPictures.Add(picture);
        await context.SaveChangesAsync(cancellationToken);

        if (request.FileStream is not null)
        {
            await coverUpload.EnqueueVariantGenerationAsync(picture.Id, cancellationToken);
        }

        return picture.Id;
    }

    private async Task<string> ResolveLocalPathAsync(
        UploadPlaylistCoverCommand request,
        Guid playlistId,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is not null && request.FileName is not null)
        {
            return await coverUpload.SaveUploadedCoverAsync(
                request.FileStream,
                request.FileName,
                "playlists",
                playlistId,
                cancellationToken);
        }

        if (request.SourcePictureId is not null)
        {
            return await coverUpload.ResolveSourcePicturePathAsync(
                request.SourcePictureId.Value,
                async (source, ct) =>
                {
                    var mediaInPlaylist = await context.PlaylistItems
                        .AnyAsync(i => i.PlaylistId == playlistId && i.MediaId == source.MediaId, ct);

                    if (!mediaInPlaylist)
                    {
                        throw new ForbiddenAccessException();
                    }
                },
                cancellationToken);
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
