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
    private async Task<OpenSubsonicActionResult> StreamOrDownloadAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        bool download,
        CancellationToken cancellationToken)
    {
        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        // Register external client device (Subsonic `c`) so it appears under Admin -> Devices.
        var deviceId = await EnsureClientDeviceAsync(parameters, cancellationToken);

        var track = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.Pictures)
            .FirstOrDefaultAsync(t => t.Id == id.Value, cancellationToken);

        if (track is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Song not found.");

        var file = track.IndexedFiles.OrderBy(f => f.Created).FirstOrDefault();
        if (file is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "No file for song.");

        Guid? transferSessionId = null;
        Guid? transferMediaId = null;
        if (!download && currentUser.Id is { } streamUserId && deviceId is { } streamDeviceId)
        {
            transferSessionId = CreateOpenSubsonicSessionId(streamUserId, streamDeviceId);
            transferMediaId = track.Id;
            await TrackOpenSubsonicStreamAsync(track, username, deviceId, cancellationToken);
        }

        var format = GetParam(parameters, "format");
        var maxBitRate = GetNullableInt(parameters, "maxBitRate");
        var timeOffset = Math.Max(0, GetNullableInt(parameters, "timeOffset") ?? 0);
        var duration = GetDurationSeconds(track);

        var shouldTranscode = OpenSubsonicStreamTranscode.TryResolve(
            download,
            format,
            maxBitRate,
            timeOffset,
            file.Extension,
            file.Size,
            duration,
            out var outputFormat,
            out var bitrateKbps);

        if (shouldTranscode)
        {
            if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "File not found.");

            if (transferSessionId is { } sessionId)
            {
                activeStreamTracker.UpdateStreamDecision(sessionId, new StreamDecisionDto
                {
                    Mode = PlaybackMode.Transcode,
                    Reason = TranscodeReason.QualityDownscale,
                    StreamAudioCodec = outputFormat,
                    Bitrate = bitrateKbps * 1000
                });
            }

            var downloadName = download
                ? null
                : $"{SanitizeFileName(track.Title ?? "track")}{OpenSubsonicStreamTranscode.ExtensionFor(outputFormat)}";

            var inputPath = file.Path;
            var contentType = OpenSubsonicStreamTranscode.ContentTypeFor(outputFormat);
            return OpenSubsonicActionResult.ProgressiveStream(
                () => openSubsonicAudioTranscoder.OpenProgressiveTranscode(
                    inputPath,
                    outputFormat,
                    bitrateKbps,
                    timeOffset),
                contentType,
                downloadName,
                transferSessionId,
                transferMediaId);
        }

        var directDownloadName = download
            ? $"{SanitizeFileName(track.Title ?? "track")}{file.Extension}"
            : null;

        return OpenSubsonicActionResult.IndexedFile(
            file.Id,
            directDownloadName,
            transferSessionId: transferSessionId,
            transferMediaId: transferMediaId);
    }

    private async Task<OpenSubsonicActionResult> GetCoverArtAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var path = await ResolveCoverPathAsync(id.Value, cancellationToken);
        if (path is null || !System.IO.File.Exists(path))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Cover art not found.");

        var contentType = GuessImageContentType(path);
        return OpenSubsonicActionResult.File(path, contentType, enableRangeProcessing: true);
    }

    private async Task<OpenSubsonicActionResult> GetLyricsBySongIdAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var track = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Include(t => t.Artist)
            .FirstOrDefaultAsync(t => t.Id == id.Value, cancellationToken);

        if (track is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Song not found.");

        var structured = BuildStructuredLyrics(track);
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["lyricsList"] = new OpenSubsonicLyricsList { StructuredLyrics = structured }
        });
    }

    private async Task<OpenSubsonicActionResult> GetLyricsAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var artist = GetParam(parameters, "artist");
        var title = GetParam(parameters, "title");
        if (string.IsNullOrWhiteSpace(title))
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["lyrics"] = new Dictionary<string, object?> { ["value"] = string.Empty }
            });

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var tracks = await GetAccessibleTracksQueryAsync(userId.Value, null, cancellationToken);
        var titleLike = EfLikeQueryExtensions.ToContainsPattern(title);
        tracks = tracks.Where(t => t.Title != null && EfLikeQueryExtensions.ILike(t.Title, titleLike));
        if (!string.IsNullOrWhiteSpace(artist))
        {
            var artistLike = EfLikeQueryExtensions.ToContainsPattern(artist);
            tracks = tracks.Where(t =>
                (t.Artist != null && t.Artist.Title != null && EfLikeQueryExtensions.ILike(t.Artist.Title, artistLike))
                || (t.Album.Artist != null && t.Album.Artist.Title != null && EfLikeQueryExtensions.ILike(t.Album.Artist.Title, artistLike)));
        }

        var track = await tracks
            .Include(t => t.Artist)
            .FirstOrDefaultAsync(cancellationToken);

        var value = track?.Lyrics ?? string.Empty;
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["lyrics"] = new Dictionary<string, object?>
            {
                ["artist"] = artist,
                ["title"] = title,
                ["value"] = value
            }
        });
    }


    private async Task TrackOpenSubsonicStreamAsync(
        MusicTrack track,
        string username,
        Guid? deviceId,
        CancellationToken cancellationToken,
        bool replaceNowPlaying = false)
    {
        if (currentUser.Id is not { } userId || deviceId is null)
            return;

        var sessionId = CreateOpenSubsonicSessionId(userId, deviceId.Value);
        var existing = activeStreamTracker.GetStreamInfo(sessionId);

        // ExoPlayer / Tempus prefetch the next tracks via /rest/stream while the current
        // transfer is still open. Keep now-playing on the current media and remember the
        // candidate; EndOpenSubsonicTransfer promotes it when the current transfer ends.
        // scrobble(submission=false) / reportPlayback use replaceNowPlaying and win immediately.
        if (!replaceNowPlaying
            && existing?.MediaId is { } currentMediaId
            && currentMediaId != track.Id
            && activeStreamTracker.IsOpenSubsonicTransferActive(sessionId, currentMediaId))
        {
            var pending = await BuildOpenSubsonicStreamInfoAsync(
                sessionId, userId, track, username, deviceId.Value, cancellationToken);
            activeStreamTracker.SetOpenSubsonicPending(sessionId, pending);
            return;
        }

        if (!replaceNowPlaying && existing?.MediaId == track.Id)
        {
            activeStreamTracker.Touch(sessionId);
            if (existing.StreamDecision is null)
            {
                activeStreamTracker.UpdateStreamDecision(sessionId, new StreamDecisionDto
                {
                    Mode = PlaybackMode.Direct,
                    Reason = TranscodeReason.None
                });
            }

            return;
        }

        var info = await BuildOpenSubsonicStreamInfoAsync(
            sessionId, userId, track, username, deviceId.Value, cancellationToken);
        activeStreamTracker.Upsert(sessionId, info);
    }

    private async Task<ActiveStreamInfo> BuildOpenSubsonicStreamInfoAsync(
        Guid sessionId,
        Guid userId,
        MusicTrack track,
        string username,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => new { d.DeviceName, ClientType = d.ClientType.ToString(), DeviceType = d.DeviceType.ToString() })
            .FirstOrDefaultAsync(cancellationToken);

        string? thumbnailUrl = null;
        var coverId = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => (p.MediaId == track.Id || p.MediaId == track.AlbumId)
                        && (p.Type == MetadataPictureType.Cover || p.Type == MetadataPictureType.Poster)
                        && p.LocalPath != null)
            .OrderBy(p => p.MediaId == track.Id ? 0 : 1)
            .ThenBy(p => p.Type)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (coverId.HasValue)
            thumbnailUrl = $"/api/metadata-pictures/{coverId.Value}?size=Small";

        var duration = GetDurationSeconds(track);

        return new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = currentUser.IdentityId ?? userId.ToString(),
            UserId = userId,
            UserName = username,
            MediaId = track.Id,
            MediaTitle = track.Title,
            MediaType = nameof(MediaType.MusicTrack),
            ParentId = track.AlbumId,
            DeviceId = deviceId,
            DeviceName = device?.DeviceName,
            DeviceClient = device?.ClientType,
            DeviceType = device?.DeviceType,
            ThumbnailUrl = thumbnailUrl,
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = duration > 0 ? duration : 1,
            State = (int)PlaybackState.Playing,
            StreamDecision = new StreamDecisionDto
            {
                Mode = PlaybackMode.Direct,
                Reason = TranscodeReason.None
            }
        };
    }

    private async Task<Guid?> EnsureClientDeviceAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
            return null;

        try
        {
            return await sender.Send(
                new EnsureOpenSubsonicDeviceCommand(GetParam(parameters, "c")),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to register OpenSubsonic client device");
            return null;
        }
    }

    private static Guid CreateOpenSubsonicSessionId(Guid userId, Guid deviceId)
    {
        var seed = $"opensubsonic-session:{userId:D}:{deviceId:D}";
#pragma warning disable CA5351
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(seed)));
#pragma warning restore CA5351
    }

}
