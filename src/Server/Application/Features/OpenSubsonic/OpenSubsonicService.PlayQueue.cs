using K7.Server.Application.Features.AudioPlayerSettings.Queries.GetEffectiveAudioPlayerSettings;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.MusicSessions;
using Microsoft.Extensions.Logging;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private async Task<OpenSubsonicActionResult> GetPlayQueueAsync(
        string username,
        string? clientName,
        bool byIndex,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        if (!await RemembersMusicSessionAsync(cancellationToken))
            return EmptyPlayQueue(username);

        var deviceId = await sender.Send(new EnsureOpenSubsonicDeviceCommand(clientName), cancellationToken);
        var session = await context.DeviceMusicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.UserId == userId && s.DeviceId == deviceId && s.SharedProfileId == null,
                cancellationToken);

        if (session is null || session.SourceKind != MusicSessionSourceKind.AdHoc)
            return EmptyPlayQueue(username);

        var snapshot = DeviceMusicSessionMapper.ToSnapshot(session);
        if (snapshot.Items.Count == 0)
            return EmptyPlayQueue(username);

        var mediaIds = snapshot.Items.Select(i => i.MediaId).Distinct().ToList();
        var tracks = await LoadMappedTracksAsync(mediaIds, userId.Value, cancellationToken);
        var byId = tracks.ToDictionary(t => t.Id);
        var songs = new List<OpenSubsonicSong>();
        foreach (var item in snapshot.Items)
        {
            if (byId.TryGetValue(item.MediaId, out var track))
                songs.Add(MapSong(track, userId.Value));
        }

        var currentIndex = snapshot.CurrentIndex;
        if (currentIndex < 0 || currentIndex >= snapshot.Items.Count)
            currentIndex = snapshot.Items.ToList().FindIndex(i => i.MediaId == snapshot.CurrentMediaId);
        if (currentIndex < 0)
            currentIndex = 0;

        var deviceName = await context.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => d.DeviceName)
            .FirstOrDefaultAsync(cancellationToken);

        var playQueue = new Dictionary<string, object?>
        {
            ["position"] = (int)Math.Round(snapshot.PositionSeconds * 1000),
            ["username"] = username,
            ["changed"] = FormatDate(snapshot.UpdatedAt),
            ["changedBy"] = deviceName ?? clientName ?? "OpenSubsonic",
            ["entry"] = songs
        };

        if (byIndex)
            playQueue["currentIndex"] = currentIndex;
        else if (snapshot.CurrentMediaId is { } currentId)
            playQueue["current"] = currentId.ToString("D");

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["playQueue"] = playQueue
        });
    }

    private async Task<OpenSubsonicActionResult> SavePlayQueueAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string? clientName,
        bool canWrite,
        bool byIndex,
        CancellationToken cancellationToken)
    {
        if (!canWrite)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Write access required.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var deviceId = await sender.Send(new EnsureOpenSubsonicDeviceCommand(clientName), cancellationToken);
        var device = await context.Devices
            .Include(d => d.Users)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device is null || device.Users.All(u => u.Id != userId))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorUnauthorized, "Device is not registered for this user.");

        var existing = await context.DeviceMusicSessions.FirstOrDefaultAsync(
            s => s.UserId == userId && s.DeviceId == deviceId && s.SharedProfileId == null,
            cancellationToken);

        if (!await RemembersMusicSessionAsync(cancellationToken))
        {
            if (existing is not null)
            {
                context.DeviceMusicSessions.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
            }

            return OpenSubsonicActionResult.OkEmpty();
        }

        var ids = GetGuids(parameters, "id");
        if (ids.Count == 0)
        {
            if (existing is not null)
            {
                context.DeviceMusicSessions.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
            }

            return OpenSubsonicActionResult.OkEmpty();
        }

        var currentIndex = ResolvePlayQueueIndex(parameters, ids, byIndex);
        var slice = MusicSessionWindow.Slice(ids, currentIndex, hasSource: false);
        var tracks = await LoadMappedTracksAsync(slice.Items.Distinct().ToList(), userId.Value, cancellationToken);
        var byId = tracks.ToDictionary(t => t.Id);
        var items = new List<MusicSessionTrackDto>();
        foreach (var id in slice.Items)
        {
            if (!byId.TryGetValue(id, out var track))
                continue;

            var file = track.IndexedFiles.OrderBy(f => f.Created).FirstOrDefault();
            items.Add(new MusicSessionTrackDto
            {
                IndexedFileId = file?.Id ?? Guid.Empty,
                MediaId = track.Id,
                Title = track.Title ?? string.Empty,
                Artist = track.Artist?.Title ?? track.Album?.Artist?.Title,
                AlbumTitle = track.Album?.Title,
                AlbumId = track.AlbumId,
                ArtistId = track.ArtistId ?? track.Album?.ArtistId,
                Duration = GetDurationSeconds(track)
            });
        }

        if (items.Count == 0)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "No playable songs in the queue.");

        var currentSlot = Math.Clamp(slice.CurrentIndex, 0, items.Count - 1);
        var current = items[currentSlot];
        var positionMs = 0L;
        var positionRaw = GetParam(parameters, "position");
        if (long.TryParse(positionRaw, out var parsedMs) && parsedMs > 0)
            positionMs = parsedMs;

        var snapshot = new MusicSessionSnapshotDto
        {
            SourceKind = MusicSessionSourceKind.AdHoc,
            CurrentMediaId = current.MediaId,
            CurrentIndexedFileId = current.IndexedFileId == Guid.Empty ? null : current.IndexedFileId,
            CurrentIndex = currentSlot,
            PositionSeconds = positionMs / 1000d,
            Items = items
        };

        var row = existing ?? new DeviceMusicSession
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            DeviceId = deviceId
        };
        if (existing is null)
            context.DeviceMusicSessions.Add(row);

        DeviceMusicSessionMapper.Apply(row, snapshot, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        return OpenSubsonicActionResult.OkEmpty();
    }

    private static int ResolvePlayQueueIndex(
        IReadOnlyDictionary<string, string[]> parameters,
        IReadOnlyList<Guid> ids,
        bool byIndex)
    {
        if (byIndex && GetNullableInt(parameters, "currentIndex") is int index && index >= 0 && index < ids.Count)
            return index;

        var current = GetGuid(parameters, "current");
        if (current is Guid currentId)
        {
            var found = ids.ToList().IndexOf(currentId);
            if (found >= 0)
                return found;
        }

        return 0;
    }

    private async Task<bool> RemembersMusicSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await sender.Send(new GetEffectiveAudioPlayerSettingsQuery(), cancellationToken);
            return settings?.RememberMusicSession != false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Music session preference unavailable, keeping the queue");
            return true;
        }
    }

    private static OpenSubsonicActionResult EmptyPlayQueue(string username) =>
        OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["playQueue"] = new Dictionary<string, object?>
            {
                ["username"] = username,
                ["entry"] = new List<OpenSubsonicSong>()
            }
        });
}
