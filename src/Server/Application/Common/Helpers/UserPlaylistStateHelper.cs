using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Helpers;

internal static class UserPlaylistStateHelper
{
    internal static async Task TouchLastListenedAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid playlistId,
        CancellationToken cancellationToken)
    {
        var ownsPlaylist = await context.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.Id == playlistId && p.UserId == userId, cancellationToken);

        if (!ownsPlaylist)
            return;

        var state = await context.UserPlaylistStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PlaylistId == playlistId, cancellationToken);

        var now = DateTime.UtcNow;
        if (state is null)
        {
            context.UserPlaylistStates.Add(new UserPlaylistState
            {
                UserId = userId,
                PlaylistId = playlistId,
                LastListenedAt = now
            });
            return;
        }

        state.LastListenedAt = now;
    }
}
