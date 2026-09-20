using K7.Clients.Shared.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Clients.Shared.Interfaces;

public interface IRemoteControlService
{
    bool IsControlling { get; }
    bool IsAudio { get; }
    bool IsCastSession { get; }
    Guid? TargetDeviceId { get; }
    string? TargetDeviceName { get; }

    RemotePlaybackState PlaybackState { get; }
    double Position { get; }
    double Duration { get; }
    double Volume { get; }
    double PlaybackRate { get; }
    AspectRatioMode AspectRatio { get; }
    int? SelectedAudioTrackIndex { get; }
    int? SelectedSubtitleTrackIndex { get; }
    IReadOnlyList<RemoteTrackInfoDto> AudioTracks { get; }
    IReadOnlyList<RemoteTrackInfoDto> SubtitleTracks { get; }
    IReadOnlyList<ChapterMarkerDto> Chapters { get; }

    string? Title { get; }
    string? Artist { get; }
    string? AlbumTitle { get; }
    string? CoverUrl { get; }
    string? ThumbnailsUrl { get; }
    Guid? MediaId { get; }
    Guid? IndexedFileId { get; }

    event Action? SessionChanged;
    event Action? StateChanged;

    void StartSession(Guid targetDeviceId, string targetDeviceName, RemotePlaybackRequestDto request);
    void StartCastSession(string deviceName, bool isAudio, string? title, string? artist, string? albumTitle, string? coverUrl, double duration, double startPosition);
    void EndSession();

    Task SendPlayAsync();
    Task SendPauseAsync();
    Task SendStopAsync();
    Task ReleaseControlAsync();
    Task SendSeekAsync(double position);
    Task SendVolumeAsync(double volume);
    Task SendAudioTrackAsync(int trackIndex);
    Task SendSubtitleTrackAsync(int trackIndex);
    Task SendPlaybackRateAsync(double rate);
    Task SendAspectRatioAsync(AspectRatioMode mode);
}
