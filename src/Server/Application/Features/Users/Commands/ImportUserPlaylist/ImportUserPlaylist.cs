using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.ImportUserPlaylist;

[Authorize(Roles = Roles.Administrator)]
public record ImportUserPlaylistCommand : IRequest<ImportUserPlaylistResponse>
{
    public required Guid UserId { get; init; }
    public required ImportUserPlaylistRequest Request { get; init; }
}

public class ImportUserPlaylistCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ImportUserPlaylistCommand, ImportUserPlaylistResponse>
{
    public async Task<ImportUserPlaylistResponse> Handle(ImportUserPlaylistCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        Guard.Against.NotFound(command.UserId, user);

        if (!AllowedPlaylistMediaTypes.Values.Contains(request.MediaType))
            throw new ValidationException($"MediaType must be one of: {string.Join(", ", AllowedPlaylistMediaTypes.Values)}");

        if (request.MediaIds.Count == 0)
        {
            var existingOnly = await context.Playlists
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.UserId == command.UserId && p.Title.ToLower() == request.Title.ToLower(),
                    cancellationToken);

            if (existingOnly is null)
                throw new ValidationException("Cannot import an empty playlist that does not already exist.");

            return new ImportUserPlaylistResponse
            {
                PlaylistId = existingOnly.Id,
                AddedItemCount = 0,
                WasCreated = false
            };
        }

        var playlist = await context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(
                p => p.UserId == command.UserId && p.Title.ToLower() == request.Title.ToLower(),
                cancellationToken);

        var wasCreated = false;
        if (playlist is null)
        {
            playlist = new Playlist
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                MediaType = request.MediaType,
                UserId = command.UserId
            };
            playlist.AddDomainEvent(new PlaylistCreatedEvent(playlist));
            context.Playlists.Add(playlist);
            wasCreated = true;
        }
        else if (playlist.MediaType != request.MediaType)
        {
            throw new ValidationException(
                $"Existing playlist '{request.Title}' has media type {playlist.MediaType}, expected {request.MediaType}.");
        }

        var existingMediaIds = playlist.Items.Select(i => i.MediaId).ToHashSet();
        var maxOrder = playlist.Items.Count > 0 ? playlist.Items.Max(i => i.Order) : -1;
        var addedCount = 0;

        foreach (var mediaId in request.MediaIds.Distinct())
        {
            if (existingMediaIds.Contains(mediaId))
                continue;

            var media = await context.Medias
                .Where(m => m.Id == mediaId)
                .Select(m => new { m.Id, m.Type })
                .FirstOrDefaultAsync(cancellationToken);

            if (media is null || media.Type != playlist.MediaType)
                continue;

            maxOrder++;
            var item = new PlaylistItem
            {
                PlaylistId = playlist.Id,
                MediaId = mediaId,
                Order = maxOrder
            };
            context.PlaylistItems.Add(item);
            playlist.AddDomainEvent(new PlaylistItemAddedEvent(playlist, item));
            existingMediaIds.Add(mediaId);
            addedCount++;
        }

        if (wasCreated || addedCount > 0)
            await context.SaveChangesAsync(cancellationToken);

        return new ImportUserPlaylistResponse
        {
            PlaylistId = playlist.Id,
            AddedItemCount = addedCount,
            WasCreated = wasCreated
        };
    }
}
