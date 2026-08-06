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
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private async Task<OpenSubsonicActionResult> GetStarredAsync(CancellationToken cancellationToken, string key)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var starredMediaIds = await context.Ratings.OfType<UserRating>()
            .AsNoTracking()
            .Where(r => r.UserId == userId.Value && r.Value > OpenSubsonicConstants.StarredThreshold)
            .Select(r => r.MediaId)
            .ToListAsync(cancellationToken);

        var accessible = mediaAccessFilter.GetAccessibleMediaIds(userId.Value);
        var ids = await accessible.Where(id => starredMediaIds.Contains(id)).ToListAsync(cancellationToken);

        var artists = await context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Include(a => a.Albums)
            .Include(a => a.Ratings)
            .ToListAsync(cancellationToken);

        var albums = await context.Medias.OfType<MusicAlbum>()
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Include(a => a.Artist)
            .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Tracks)
            .Include(a => a.Ratings)
            .Include(a => a.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var songs = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [key] = new Dictionary<string, object?>
            {
                ["artist"] = artists.Select(a => MapArtist(a, userId.Value)).ToList(),
                ["album"] = albums.Select(a => MapAlbum(a, userId.Value, includeSongs: false)).ToList(),
                ["song"] = songs.Select(t => MapSong(t, userId.Value)).ToList()
            }
        });
    }

    private async Task<OpenSubsonicActionResult> StarAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var ids = GetGuids(parameters, "id")
            .Concat(GetGuids(parameters, "albumId"))
            .Concat(GetGuids(parameters, "artistId"))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        foreach (var id in ids)
            await sender.Send(new RateMediaCommand(id, 10), cancellationToken);

        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task<OpenSubsonicActionResult> UnstarAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var ids = GetGuids(parameters, "id")
            .Concat(GetGuids(parameters, "albumId"))
            .Concat(GetGuids(parameters, "artistId"))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var ratings = await context.Ratings.OfType<UserRating>()
            .Where(r => r.UserId == userId.Value && ids.Contains(r.MediaId))
            .ToListAsync(cancellationToken);

        context.Ratings.RemoveRange(ratings);
        await context.SaveChangesAsync(cancellationToken);
        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task<OpenSubsonicActionResult> SetRatingAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var rating = GetInt(parameters, "rating", -1, 0, 5);
        if (rating < 0)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing rating.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        if (rating == 0)
        {
            var existing = await context.Ratings.OfType<UserRating>()
                .FirstOrDefaultAsync(r => r.UserId == userId.Value && r.MediaId == id.Value, cancellationToken);
            if (existing is not null)
            {
                context.Ratings.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
            }

            return OpenSubsonicActionResult.OkEmpty();
        }

        await sender.Send(new RateMediaCommand(id.Value, rating * OpenSubsonicConstants.RatingScaleFactor), cancellationToken);
        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task<OpenSubsonicActionResult> ScrobbleAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var submission = GetBool(parameters, "submission") ?? true;
        var track = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .FirstOrDefaultAsync(t => t.Id == id.Value, cancellationToken);

        if (track is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Song not found.");

        var duration = GetDurationSeconds(track);
        var safeDuration = duration > 0 ? duration : 1;
        var deviceId = await EnsureClientDeviceAsync(parameters, cancellationToken);

        if (!submission)
        {
            // Now playing: reuse the open history session for this track when present so
            // play/pause/resume does not spam empty history rows (Tempus sends this often).
            var sessionId = Guid.NewGuid();
            var position = 0.0;
            if (currentUser.Id is { } nowPlayingUserId)
            {
                // Close the previous open listen on this device (Tempus often skips
                // scrobble=true when advancing). Do not backfill a backlog of abandoned plays.
                await ClosePreviousOpenHistorySessionAsync(
                    nowPlayingUserId,
                    exceptMediaId: id.Value,
                    deviceId,
                    cancellationToken);

                var open = await context.MediaPlaybackSessions
                    .AsNoTracking()
                    .Where(s => s.UserId == nowPlayingUserId
                                && s.MediaId == id.Value
                                && s.CompletedAt == null
                                && s.State != PlaybackState.Ended
                                && s.State != PlaybackState.Idle)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => new { s.SessionId, s.PositionSeconds })
                    .FirstOrDefaultAsync(cancellationToken);

                if (open is not null)
                {
                    sessionId = open.SessionId;
                    position = open.PositionSeconds;
                }
            }

            await sender.Send(new UpdatePlaybackProgressCommand(
                MediaId: id.Value,
                SessionId: sessionId,
                ReferenceId: sessionId,
                Position: position,
                Duration: safeDuration,
                State: PlaybackState.Playing,
                DeviceId: deviceId), cancellationToken);

            // UpdatePlaybackProgress upserts under the history session id; keep a single
            // device-scoped active stream for admin UI instead.
            activeStreamTracker.Remove(sessionId);
            await TrackOpenSubsonicStreamAsync(
                track,
                username,
                deviceId,
                cancellationToken,
                replaceNowPlaying: true);

            return OpenSubsonicActionResult.OkEmpty();
        }

        // submission=true: count as a completed listen. Close the open session for this track if any.
        var sessionIdToClose = Guid.NewGuid();
        if (currentUser.Id is { } userId)
        {
            var openSessionId = await context.MediaPlaybackSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId
                            && s.MediaId == id.Value
                            && s.CompletedAt == null
                            && s.State != PlaybackState.Ended
                            && s.State != PlaybackState.Idle)
                .OrderByDescending(s => s.StartedAt)
                .Select(s => (Guid?)s.SessionId)
                .FirstOrDefaultAsync(cancellationToken);

            if (openSessionId is { } existingSessionId)
                sessionIdToClose = existingSessionId;
        }

        await sender.Send(new UpdatePlaybackProgressCommand(
            MediaId: id.Value,
            SessionId: sessionIdToClose,
            ReferenceId: sessionIdToClose,
            Position: safeDuration,
            Duration: safeDuration,
            State: PlaybackState.Ended,
            DeviceId: deviceId), cancellationToken);

        if (currentUser.Id is { } activeUserId && deviceId is { } activeDeviceId)
        {
            var activeSessionId = CreateOpenSubsonicSessionId(activeUserId, activeDeviceId);
            var active = activeStreamTracker.GetStreamInfo(activeSessionId);
            if (active?.MediaId == id.Value)
                activeStreamTracker.Remove(activeSessionId);
        }

        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task<OpenSubsonicActionResult> ReportPlaybackAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        bool canWrite,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var mediaId = GetGuid(parameters, "mediaId");
        if (mediaId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing mediaId.");

        var mediaType = GetParam(parameters, "mediaType");
        if (string.IsNullOrWhiteSpace(mediaType))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing mediaType.");

        if (!string.Equals(mediaType, "song", StringComparison.OrdinalIgnoreCase))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Only mediaType=song is supported.");

        var positionMsRaw = GetParam(parameters, "positionMs");
        if (!long.TryParse(positionMsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionMs)
            || positionMs < 0)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing or invalid positionMs.");

        var stateRaw = GetParam(parameters, "state");
        if (string.IsNullOrWhiteSpace(stateRaw))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing state.");

        var playbackState = MapReportPlaybackState(stateRaw);
        if (playbackState is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorGeneric, "Invalid state.");

        var playbackRate = GetDouble(parameters, "playbackRate") ?? 1.0;
        if (playbackRate <= 0)
            playbackRate = 1.0;

        var ignoreScrobble = GetBool(parameters, "ignoreScrobble") ?? false;

        await accessGuard.EnsureAccessAsync(mediaId.Value, cancellationToken);

        var track = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .FirstOrDefaultAsync(t => t.Id == mediaId.Value, cancellationToken);

        if (track is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Song not found.");

        var duration = GetDurationSeconds(track);
        var safeDuration = duration > 0 ? duration : 1;
        var positionSeconds = Math.Min(safeDuration, positionMs / 1000.0);
        var deviceId = await EnsureClientDeviceAsync(parameters, cancellationToken);

        if (playbackState == PlaybackState.Ended)
        {
            if (!ignoreScrobble)
            {
                // Close the open history session with the real position. Completion uses the
                // user's effective K7 audio playback policy inside UpdatePlaybackProgress
                // (not a hardcoded OpenSubsonic 50%/4min gate).
                var sessionIdToClose = Guid.NewGuid();
                if (currentUser.Id is { } userId)
                {
                    var openSessionId = await context.MediaPlaybackSessions
                        .AsNoTracking()
                        .Where(s => s.UserId == userId
                                    && s.MediaId == mediaId.Value
                                    && s.CompletedAt == null
                                    && s.State != PlaybackState.Ended
                                    && s.State != PlaybackState.Idle)
                        .OrderByDescending(s => s.StartedAt)
                        .Select(s => (Guid?)s.SessionId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (openSessionId is { } existingSessionId)
                        sessionIdToClose = existingSessionId;
                }

                await sender.Send(new UpdatePlaybackProgressCommand(
                    MediaId: mediaId.Value,
                    SessionId: sessionIdToClose,
                    ReferenceId: sessionIdToClose,
                    Position: positionSeconds,
                    Duration: safeDuration,
                    State: PlaybackState.Ended,
                    DeviceId: deviceId), cancellationToken);
            }

            if (currentUser.Id is { } activeUserId && deviceId is { } activeDeviceId)
            {
                var activeSessionId = CreateOpenSubsonicSessionId(activeUserId, activeDeviceId);
                var active = activeStreamTracker.GetStreamInfo(activeSessionId);
                if (active?.MediaId == mediaId.Value)
                    activeStreamTracker.Remove(activeSessionId);
            }

            return OpenSubsonicActionResult.OkEmpty();
        }

        // starting / playing / paused: update now-playing with accurate timeline.
        await TrackOpenSubsonicStreamAsync(
            track,
            username,
            deviceId,
            cancellationToken,
            replaceNowPlaying: true);

        if (!ignoreScrobble)
        {
            if (currentUser.Id is { } historyUserId
                && playbackState is PlaybackState.Playing)
            {
                await ClosePreviousOpenHistorySessionAsync(
                    historyUserId,
                    exceptMediaId: mediaId.Value,
                    deviceId,
                    cancellationToken);
            }

            var historySessionId = Guid.NewGuid();
            if (currentUser.Id is { } openHistoryUserId)
            {
                var openSessionId = await context.MediaPlaybackSessions
                    .AsNoTracking()
                    .Where(s => s.UserId == openHistoryUserId
                                && s.MediaId == mediaId.Value
                                && s.CompletedAt == null
                                && s.State != PlaybackState.Ended
                                && s.State != PlaybackState.Idle)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => (Guid?)s.SessionId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (openSessionId is { } existing)
                    historySessionId = existing;
            }

            await sender.Send(new UpdatePlaybackProgressCommand(
                MediaId: mediaId.Value,
                SessionId: historySessionId,
                ReferenceId: historySessionId,
                Position: positionSeconds,
                Duration: safeDuration,
                State: playbackState.Value,
                DeviceId: deviceId), cancellationToken);

            // UpdatePlaybackProgress upserts under the history session id and may drop the
            // device-scoped active stream (same user+media). Restore admin now-playing.
            activeStreamTracker.Remove(historySessionId);
            await TrackOpenSubsonicStreamAsync(
                track,
                username,
                deviceId,
                cancellationToken,
                replaceNowPlaying: true);
        }

        if (currentUser.Id is { } reportUserId && deviceId is { } reportDeviceId)
        {
            var sessionId = CreateOpenSubsonicSessionId(reportUserId, reportDeviceId);
            var info = activeStreamTracker.GetStreamInfo(sessionId);
            if (info is not null && info.MediaId == mediaId.Value)
            {
                info.Position = positionSeconds;
                info.Duration = safeDuration;
                info.State = (int)playbackState.Value;
                info.HasPlaybackProgress = true;
                info.PlaybackRate = playbackRate;
                info.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        return OpenSubsonicActionResult.OkEmpty();
    }

    private async Task ClosePreviousOpenHistorySessionAsync(
        Guid userId,
        Guid exceptMediaId,
        Guid? deviceId,
        CancellationToken cancellationToken)
    {
        // Only the latest open session (prefer same device). Closing every abandoned
        // Playing row would backfill the whole history as completed on the next skip.
        var openQuery = context.MediaPlaybackSessions
            .Where(s => s.UserId == userId
                        && s.MediaId != exceptMediaId
                        && s.CompletedAt == null
                        && s.State != PlaybackState.Ended
                        && s.State != PlaybackState.Idle);

        MediaPlaybackSession? previous = null;
        if (deviceId is { } sameDeviceId)
        {
            previous = await openQuery
                .Where(s => s.DeviceId == sameDeviceId)
                .OrderByDescending(s => s.LastUpdateAt ?? s.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (previous is null)
        {
            previous = await openQuery
                .OrderByDescending(s => s.LastUpdateAt ?? s.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (previous is null)
            return;

        // Tempus often leaves the prior track as Playing after a pause. Ending it while
        // still Playing would credit the wall-clock pause gap up to full duration and
        // falsely mark the listen completed. Freeze at known progress first.
        if (previous.State == PlaybackState.Playing)
        {
            previous.State = PlaybackState.Paused;
            previous.StoppedAt ??= DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        var duration = previous.DurationSeconds > 0 ? previous.DurationSeconds : 1;
        var position = Math.Max(previous.PositionSeconds, previous.WatchedDurationSeconds);
        await sender.Send(new UpdatePlaybackProgressCommand(
            MediaId: previous.MediaId,
            SessionId: previous.SessionId,
            ReferenceId: previous.SessionId,
            Position: position,
            Duration: duration,
            State: PlaybackState.Ended,
            DeviceId: deviceId), cancellationToken);
    }

    private static PlaybackState? MapReportPlaybackState(string state) =>
        state.Trim().ToLowerInvariant() switch
        {
            "starting" => PlaybackState.Playing,
            "playing" => PlaybackState.Playing,
            "paused" => PlaybackState.Paused,
            "stopped" => PlaybackState.Ended,
            _ => null
        };

    private async Task<OpenSubsonicActionResult> GetNowPlayingAsync(CancellationToken cancellationToken)
    {
        var streams = activeStreamTracker.GetActiveStreams()
            .Where(s => string.Equals(s.MediaType, nameof(MediaType.MusicTrack), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.MediaType, "MusicTrack", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.MediaType, "music", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entries = new List<OpenSubsonicNowPlayingEntry>();
        var playerId = 0;
        foreach (var stream in streams)
        {
            if (stream.MediaId is null)
                continue;

            string? reportState = null;
            long? positionMs = null;
            double? playbackRate = null;
            if (stream.HasPlaybackProgress)
            {
                reportState = stream.State switch
                {
                    (int)PlaybackState.Paused => "paused",
                    (int)PlaybackState.Ended => "stopped",
                    _ => "playing"
                };
                positionMs = (long)Math.Round(Math.Max(0, stream.Position) * 1000);
                playbackRate = stream.PlaybackRate > 0 ? stream.PlaybackRate : 1.0;
            }

            entries.Add(new OpenSubsonicNowPlayingEntry
            {
                Username = stream.UserName ?? string.Empty,
                MinutesAgo = Math.Max(0, (int)(DateTime.UtcNow - stream.LastUpdatedAt).TotalMinutes),
                PlayerId = playerId++,
                PlayerName = stream.DeviceName,
                Id = stream.MediaId.Value.ToString("D"),
                Title = stream.MediaTitle ?? string.Empty,
                State = reportState,
                PositionMs = positionMs,
                PlaybackRate = playbackRate
            });
        }

        await Task.CompletedTask;
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["nowPlaying"] = new Dictionary<string, object?> { ["entry"] = entries }
        });
    }

}
