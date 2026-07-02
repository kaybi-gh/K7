using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ISender sender,
    IOptions<PathsConfiguration> pathsConfiguration,
    IUser currentUser,
    ILogger<UploadPlaylistCoverCommandHandler> logger)
    : IRequestHandler<UploadPlaylistCoverCommand, Guid>
{
    private readonly PathsConfiguration _pathsConfiguration = pathsConfiguration.Value;

    public async Task<Guid> Handle(UploadPlaylistCoverCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.CoverPicture)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        if (playlist.CoverPicture is not null)
            context.MetadataPictures.Remove(playlist.CoverPicture);

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
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = picture.Id,
                TargetEntityTypeName = nameof(MetadataPicture)
            }, cancellationToken);
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
            var ext = Path.GetExtension(request.FileName);
            var directory = Path.Combine(_pathsConfiguration.Metadatas, "playlists", $"{playlistId}");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"cover{ext}");

            await using (var fs = File.Create(filePath))
                await request.FileStream.CopyToAsync(fs, cancellationToken);

            logger.LogInformation("Saved uploaded playlist cover for {PlaylistId} to {Path}", playlistId, filePath);
            return _pathsConfiguration.ToRelativeMetadataPath(filePath);
        }

        if (request.SourcePictureId is not null)
        {
            var source = await context.MetadataPictures
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.SourcePictureId, cancellationToken);

            Guard.Against.NotFound(request.SourcePictureId.Value, source);

            var mediaInPlaylist = await context.PlaylistItems
                .AnyAsync(i => i.PlaylistId == playlistId && i.MediaId == source.MediaId, cancellationToken);

            if (!mediaInPlaylist)
                throw new ForbiddenAccessException();

            return source.LocalPath ?? string.Empty;
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
