using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface IRemoteControlService
{
    bool IsControlling { get; }
    bool IsAudio { get; }
    bool IsCastSession { get; }
    Guid? TargetDeviceId { get; }
    string? TargetDeviceName { get; }

    // Current remote state
    RemotePlaybackState PlaybackState { get; }
    double Position { get; }
    double Duration { get; }
    double Volume { get; }
    int? SelectedAudioTrackIndex { get; }
    int? SelectedSubtitleTrackIndex { get; }
    IReadOnlyList<RemoteTrackInfoDto> AudioTracks { get; }
    IReadOnlyList<RemoteTrackInfoDto> SubtitleTracks { get; }

    // Media info (from the original request)
    string? Title { get; }
    string? Artist { get; }
    string? AlbumTitle { get; }
    string? CoverUrl { get; }
    Guid? MediaId { get; }
    Guid? IndexedFileId { get; }

    // Events
    event Action? SessionChanged;
    event Action? StateChanged;

    // Session management
    void StartSession(Guid targetDeviceId, string targetDeviceName, RemotePlaybackRequestDto request);
    void StartCastSession(string deviceName, bool isAudio, string? title, string? artist, string? albumTitle, string? coverUrl, double duration, double startPosition);
    void EndSession();

    // Transport commands
    Task SendPlayAsync();
    Task SendPauseAsync();
    Task SendStopAsync();
    Task SendSeekAsync(double position);
    Task SendVolumeAsync(double volume);
    Task SendAudioTrackAsync(int trackIndex);
    Task SendSubtitleTrackAsync(int trackIndex);
}
