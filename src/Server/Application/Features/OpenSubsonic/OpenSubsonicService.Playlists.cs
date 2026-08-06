using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Application.Features.Medias.Queries.GetSimilarMusicArtists;
using K7.Server.Application.Features.MusicIntelligence.Queries.GetSimilarTracks;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;
using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private async Task<OpenSubsonicActionResult> GetPlaylistsAsync(string username, CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var playlists = await context.Playlists
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value && p.MediaType == MediaType.MusicTrack)
            .OrderBy(p => p.Title)
            .Include(p => p.Items)
            .Include(p => p.CoverPicture)
            .ToListAsync(cancellationToken);

        var mapped = playlists.Select(p => MapPlaylist(p, username, includeEntries: false)).ToList();
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["playlists"] = new Dictionary<string, object?> { ["playlist"] = mapped }
        });
    }

    private async Task<OpenSubsonicActionResult> GetPlaylistAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var playlist = await context.Playlists
            .AsNoTracking()
            .Include(p => p.CoverPicture)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id.Value && p.UserId == userId.Value, cancellationToken);

        if (playlist is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Playlist not found.");

        var mediaIds = playlist.Items.OrderBy(i => i.Order).Select(i => i.MediaId).ToList();
        var tracks = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id))
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var byId = tracks.ToDictionary(t => t.Id);
        var entries = mediaIds
            .Where(byId.ContainsKey)
            .Select(mid => MapSong(byId[mid], userId.Value))
            .ToList();

        var mapped = MapPlaylist(playlist, username, includeEntries: true);
        mapped = new OpenSubsonicPlaylist
        {
            Id = mapped.Id,
            Name = mapped.Name,
            Comment = mapped.Comment,
            Owner = mapped.Owner,
            Public = mapped.Public,
            SongCount = entries.Count,
            Duration = entries.Sum(e => e.Duration ?? 0),
            Created = mapped.Created,
            Changed = mapped.Changed,
            CoverArt = mapped.CoverArt,
            Entry = entries
        };

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?> { ["playlist"] = mapped });
    }

    private async Task<OpenSubsonicActionResult> CreatePlaylistAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        string username,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var playlistId = GetGuid(parameters, "playlistId");
        var name = GetParam(parameters, "name");
        var songIds = GetGuids(parameters, "songId");
        var isPublic = GetBool(parameters, "public");

        Guid id;
        if (playlistId is not null)
        {
            var existing = await context.Playlists
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == playlistId.Value && p.UserId == userId.Value, cancellationToken);

            if (existing is null)
                return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Playlist not found.");

            if (!string.IsNullOrWhiteSpace(name) || isPublic is not null)
            {
                await sender.Send(new UpdatePlaylistCommand
                {
                    Id = existing.Id,
                    Title = !string.IsNullOrWhiteSpace(name) ? name! : existing.Title,
                    Description = existing.Description,
                    MediaType = existing.MediaType,
                    VisibilityScope = isPublic == true ? VisibilityScope.LocalServer : existing.VisibilityScope
                }, cancellationToken);
            }

            foreach (var item in existing.Items.ToList())
            {
                await sender.Send(new RemovePlaylistItemCommand
                {
                    PlaylistId = existing.Id,
                    ItemId = item.Id
                }, cancellationToken);
            }

            foreach (var songId in songIds)
            {
                await sender.Send(new AddPlaylistItemCommand
                {
                    PlaylistId = existing.Id,
                    MediaId = songId
                }, cancellationToken);
            }

            id = existing.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(name))
                return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing name.");

            id = await sender.Send(new CreatePlaylistCommand
            {
                Title = name!,
                MediaType = MediaType.MusicTrack,
                VisibilityScope = isPublic == true ? VisibilityScope.LocalServer : VisibilityScope.Nobody
            }, cancellationToken);

            foreach (var songId in songIds)
            {
                await sender.Send(new AddPlaylistItemCommand
                {
                    PlaylistId = id,
                    MediaId = songId
                }, cancellationToken);
            }
        }

        return await GetPlaylistAsync(
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = [id.ToString("D")]
            },
            username,
            cancellationToken);
    }

    private async Task<OpenSubsonicActionResult> UpdatePlaylistAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var playlistId = GetGuid(parameters, "playlistId");
        if (playlistId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing playlistId.");

        var playlist = await context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId.Value && p.UserId == userId.Value, cancellationToken);

        if (playlist is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Playlist not found.");

        var name = GetParam(parameters, "name");
        var comment = GetParam(parameters, "comment");
        var isPublic = GetBool(parameters, "public");

        if (!string.IsNullOrWhiteSpace(name) || comment is not null || isPublic is not null)
        {
            await sender.Send(new UpdatePlaylistCommand
            {
                Id = playlist.Id,
                Title = !string.IsNullOrWhiteSpace(name) ? name! : playlist.Title,
                Description = comment ?? playlist.Description,
                MediaType = playlist.MediaType,
                VisibilityScope = isPublic switch
                {
                    true => VisibilityScope.LocalServer,
                    false => VisibilityScope.Nobody,
                    null => playlist.VisibilityScope
                }
            }, cancellationToken);
        }

        var indexesToRemove = GetParams(parameters, "songIndexToRemove")
            .Select(s => int.TryParse(s, out var i) ? i : -1)
            .Where(i => i >= 0)
            .OrderByDescending(i => i)
            .ToList();

        var orderedItems = playlist.Items.OrderBy(i => i.Order).ToList();
        foreach (var index in indexesToRemove)
        {
            if (index >= orderedItems.Count)
                continue;

            await sender.Send(new RemovePlaylistItemCommand
            {
                PlaylistId = playlist.Id,
                ItemId = orderedItems[index].Id
            }, cancellationToken);
        }

        foreach (var songId in GetGuids(parameters, "songIdToAdd"))
        {
            await sender.Send(new AddPlaylistItemCommand
            {
                PlaylistId = playlist.Id,
                MediaId = songId
            }, cancellationToken);
        }

        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task<OpenSubsonicActionResult> DeletePlaylistAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await sender.Send(new DeletePlaylistCommand(id.Value), cancellationToken);
        return OpenSubsonicActionResult.OkEmpty();
    }

}
