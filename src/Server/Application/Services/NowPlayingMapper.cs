using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Services;

public static class NowPlayingMapper
{
    public static IReadOnlyList<NowPlayingSessionDto> FromTracker(
        IActiveStreamTracker tracker,
        string identityUserId)
    {
        var streams = tracker.GetActiveStreams();
        if (streams.Count == 0)
            return [];

        List<NowPlayingSessionDto>? result = null;
        foreach (var stream in streams)
        {
            if (!string.Equals(stream.IdentityUserId, identityUserId, StringComparison.Ordinal))
                continue;

            result ??= [];
            result.Add(FromStream(stream));
        }

        return result ?? [];
    }

    public static NowPlayingSessionDto FromStream(ActiveStreamInfo stream)
    {
        var isAudio = string.Equals(stream.MediaType, nameof(MediaType.MusicTrack), StringComparison.Ordinal)
            || string.Equals(stream.MediaType, nameof(MediaType.MusicAlbum), StringComparison.Ordinal);
        var isExternal = string.Equals(stream.DeviceClient, nameof(ClientType.External), StringComparison.Ordinal);

        return new NowPlayingSessionDto
        {
            SessionId = stream.SessionId,
            DeviceId = stream.DeviceId,
            DeviceName = stream.DeviceName,
            DeviceType = stream.DeviceType,
            DeviceClient = stream.DeviceClient,
            MediaId = stream.MediaId,
            IndexedFileId = stream.IndexedFileId,
            MediaTitle = stream.MediaTitle,
            MediaType = stream.MediaType,
            ParentId = stream.ParentId,
            SeasonNumber = stream.SeasonNumber,
            EpisodeNumber = stream.EpisodeNumber,
            ThumbnailUrl = stream.ThumbnailUrl,
            Position = stream.Position,
            Duration = stream.Duration,
            State = stream.State,
            IsAudio = isAudio,
            CanControl = stream.DeviceId.HasValue && !isExternal,
            AudioTrackIndex = stream.AudioTrackIndex,
            SubtitleTrackIndex = stream.SubtitleTrackIndex,
            PlaybackRate = stream.PlaybackRate > 0 ? stream.PlaybackRate : 1
        };
    }
}
