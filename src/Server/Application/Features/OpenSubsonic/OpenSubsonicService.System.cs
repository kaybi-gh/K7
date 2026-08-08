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
    private static OpenSubsonicActionResult Ping() => OpenSubsonicActionResult.OkEmpty();

    private static OpenSubsonicActionResult GetLicense() =>
        OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["license"] = new Dictionary<string, object?>
            {
                ["valid"] = true,
                ["email"] = "k7@localhost",
                ["licenseExpires"] = "2099-12-31T23:59:59"
            }
        });

    private static OpenSubsonicActionResult GetOpenSubsonicExtensions() =>
        OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["openSubsonicExtensions"] = new List<OpenSubsonicExtension>
            {
                new() { Name = "apiKeyAuthentication", Versions = [1] },
                new() { Name = "songLyrics", Versions = [1] },
                new() { Name = "formPost", Versions = [1] },
                new() { Name = "playbackReport", Versions = [1] },
                new() { Name = "transcodeOffset", Versions = [1] }
            }
        });

    private static OpenSubsonicActionResult TokenInfo(string username) =>
        OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["tokenInfo"] = new Dictionary<string, object?>
            {
                ["username"] = username
            }
        });


    private async Task<OpenSubsonicActionResult> GetUserAsync(string username, bool canWrite, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["user"] = new OpenSubsonicUser
            {
                Username = username,
                AdminRole = false,
                SettingsRole = canWrite,
                DownloadRole = true,
                PlaylistRole = canWrite,
                StreamRole = true,
                ScrobblingEnabled = canWrite
            }
        });
    }

    private async Task<OpenSubsonicActionResult> GetAvatarAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var username = GetParam(parameters, "username");
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        Guid targetUserId = userId.Value;
        if (!string.IsNullOrWhiteSpace(username))
        {
            // Avatar lookup by username is limited to the current user in V1.
            _ = username;
        }

        var picture = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId == targetUserId && p.Type == MetadataPictureType.UserAvatar)
            .OrderByDescending(p => p.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (picture?.LocalPath is null || !System.IO.File.Exists(picture.LocalPath))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Avatar not found.");

        return OpenSubsonicActionResult.File(picture.LocalPath, GuessImageContentType(picture.LocalPath));
    }

    private async Task<OpenSubsonicActionResult> StartScanAsync(bool canWrite, CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var libraries = await context.Libraries
            .AsNoTracking()
            .Where(l => l.MediaType == LibraryMediaType.Music && l.PeerServerId == null)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        foreach (var libraryId in libraries)
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new IndexLibraryFilesCommand(libraryId),
                TargetEntityId = libraryId,
                TargetEntityTypeName = nameof(Library),
                Lane = BackgroundTaskLane.LibraryScan,
                WorkClass = BackgroundTaskWorkClass.CriticalLink,
                TriggeredBy = BackgroundTaskTriggeredBy.User,
                TimeoutSeconds = 3600
            }, cancellationToken);
        }

        return await GetScanStatusAsync(cancellationToken);
    }

    private async Task<OpenSubsonicActionResult> GetScanStatusAsync(CancellationToken cancellationToken)
    {
        var scanning = await context.BackgroundTasks
            .AsNoTracking()
            .AnyAsync(t =>
                t.Name == nameof(IndexLibraryFilesCommand)
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.InProgress
                    || t.Status == BackgroundTaskStatus.WaitingForRetry),
                cancellationToken);

        var count = await context.Medias.OfType<MusicTrack>().AsNoTracking().LongCountAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["scanStatus"] = new OpenSubsonicScanStatus
            {
                Scanning = scanning,
                Count = count
            }
        });
    }

}
