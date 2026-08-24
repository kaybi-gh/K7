using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Web.Services;

public class PlayerService(IStreamUriService streamUriService, IDeviceStorageService deviceStorageService) : IPlayerService
{
    public event Func<Task>? PlayRequested;
    public event Func<Task>? PauseRequested;
    public event Func<Task>? StopRequested;
    public event Func<double, Task>? SeekRequested;
    public event Func<Task>? EnterFullScreenRequested;
    public event Func<Task>? ExitFullScreenRequested;
    public event Func<Task>? MuteRequested;
    public event Func<Task>? UnmuteRequest;
    public event Func<double, Task>? VolumeChangeRequested;
    public event Func<double, Task>? PlaybackRateChangeRequested;
#pragma warning disable CS0067 // Reserved for muxed/container audio track switching; demuxed HLS reloads the manifest instead.
    public event Action<string>? SwitchAudioTrackRequested;
#pragma warning restore CS0067
    public event Action<string?>? SwitchSubtitleTrackRequested;
    public event Action<AspectRatioMode>? AspectRatioModeChangeRequested;

#pragma warning disable CS0067
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
#pragma warning restore CS0067
    public event Action<bool>? IsFullScreenChanged;
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<double>? PlaybackRateChanged;
    public event Action<bool>? IsMutedChanged;
    public event Action<AudioFileTrackDto?>? AudioTrackChanged;
    public event Action<SubtitleFileTrackDto?>? SubtitleTrackChanged;
    public event Action? SubtitleTracksChanged;
    public event Action<VideoQualityOption?>? QualityChanged;
    public event Action<AspectRatioMode>? AspectRatioModeChanged;

    public event Action? BackPressed;
    public event Action? PlaybackStartFailed;

    public string? PlaybackStartFailureMessageKey { get; private set; }

    public PlayerSource Source { get; set
        {
            if (field != value)
            {
                field = value;
                CurrentTime = 0;
                Duration = 0;
                BufferedTime = 0;
                PlaybackState = PlaybackState.Idle;
                SourceChanged?.Invoke(value);
            }
        } } = new();

    public bool IsVisible { get; private set; } = false;

    public PlaybackState PlaybackState { get; set
        {
            if (field != value)
            {
                field = value;
                PlaybackStateChanged?.Invoke(value);
            }
        } } = PlaybackState.Unknown;

    public bool IsFullScreen { get; set
        {
            if (field != value)
            {
                field = value;
                IsFullScreenChanged?.Invoke(value);
            }
        } } = false;

    public double Duration { get; set
        {
            if (field != value)
            {
                field = value;
                DurationChanged?.Invoke(value);
            }
        } } = 0;

    public double CurrentTime { get; set
        {
            if (field != value)
            {
                field = value;
                CurrentTimeChanged?.Invoke(value);
            }
        } } = 0;

    public double BufferedTime { get; set
        {
            if (field != value)
            {
                field = value;
                BufferedTimeChanged?.Invoke(value);
            }
        } } = 0;

    public double Volume { get; set
        {
            if (field != value)
            {
                field = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_VOLUME, value);
                VolumeChanged?.Invoke(value);
            }
        } } = deviceStorageService.Get(PreferenceKeys.PLAYER_VOLUME, 1);

    public double PlaybackRate { get; set
        {
            if (field != value)
            {
                field = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_PLAYBACK_RATE, value);
                PlaybackRateChanged?.Invoke(value);
            }
        } } = deviceStorageService.Get(PreferenceKeys.PLAYER_PLAYBACK_RATE, 1);

    public bool IsMuted { get; set
        {
            if (field != value)
            {
                field = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_IS_MUTED, value);
                IsMutedChanged?.Invoke(value);
            }
        } } = deviceStorageService.Get(PreferenceKeys.PLAYER_IS_MUTED, false);

    public event Action? PlayerUxSettingsChanged;

    private VideoPlayerSettingsDto? _videoPlayerUxSettings;
    public VideoPlayerSettingsDto? VideoPlayerUxSettings => _videoPlayerUxSettings;

    private int _skipBackSeconds = deviceStorageService.Get(PreferenceKeys.VIDEO_SKIP_BACK_SECONDS, 10);
    public int SkipBackSeconds => _skipBackSeconds;

    private int _skipForwardSeconds = deviceStorageService.Get(PreferenceKeys.VIDEO_SKIP_FORWARD_SECONDS, 10);
    public int SkipForwardSeconds => _skipForwardSeconds;

    public void ApplyVideoPlayerUxSettings(VideoPlayerSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _videoPlayerUxSettings = settings;
        _skipBackSeconds = Math.Max(1, settings.SkipBackSeconds);
        _skipForwardSeconds = Math.Max(1, settings.SkipForwardSeconds);
        deviceStorageService.Set(PreferenceKeys.VIDEO_SKIP_BACK_SECONDS, _skipBackSeconds);
        deviceStorageService.Set(PreferenceKeys.VIDEO_SKIP_FORWARD_SECONDS, _skipForwardSeconds);
        PlayerUxSettingsChanged?.Invoke();
    }

    public void SetSkipBackSeconds(int seconds)
    {
        _skipBackSeconds = Math.Max(1, seconds);
        deviceStorageService.Set(PreferenceKeys.VIDEO_SKIP_BACK_SECONDS, _skipBackSeconds);
        PlayerUxSettingsChanged?.Invoke();
    }

    public void SetSkipForwardSeconds(int seconds)
    {
        _skipForwardSeconds = Math.Max(1, seconds);
        deviceStorageService.Set(PreferenceKeys.VIDEO_SKIP_FORWARD_SECONDS, _skipForwardSeconds);
        PlayerUxSettingsChanged?.Invoke();
    }

    private Guid? _currentIndexedFileId;
    private List<AudioFileTrackDto> _audioTracks = [];
    public IReadOnlyList<AudioFileTrackDto> AudioTracks => _audioTracks;

    public AudioFileTrackDto? SelectedAudioTrack { get; private set; }

    private List<SubtitleFileTrackDto> _subtitleTracks = [];
    public IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks => _subtitleTracks;

    public SubtitleFileTrackDto? SelectedSubtitleTrack { get; private set; }

    private List<VideoQualityOption> _availableQualities = [];
    public IReadOnlyList<VideoQualityOption> AvailableQualities => _availableQualities;

    public VideoQualityOption? SelectedQuality { get; private set; }
    public AspectRatioMode AspectRatio { get; private set; } = AspectRatioMode.Fit;

    /// <summary>
    /// Base manifest URL (without Quality param) used to rebuild the source when switching quality.
    /// </summary>
    private string? _baseManifestUrl;
    private int _playGeneration;
    private CancellationTokenSource? _playCts;

    public async Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, double? startPosition = null, IReadOnlyList<ChapterMarkerDto>? chapters = null, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _playGeneration);
        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var playToken = _playCts.Token;

        _currentIndexedFileId = indexedFileId;
        _audioTracks = audioTracks.ToList();
        SetSubtitleTracks(subtitleTracks);
        SelectedSubtitleTrack = null;
        SelectedAudioTrack = audioTrackIndex is int idx
            ? _audioTracks.FirstOrDefault(t => t.Index == idx)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        _availableQualities = videoResolution is not null
            ? VideoQualityOption.BuildOptionsForResolution(videoResolution.Value).ToList()
            : [];
        SelectedQuality = _availableQualities.FirstOrDefault(q => q.IsOriginal)
            ?? _availableQualities.FirstOrDefault();

        Source = new PlayerSource();

        try
        {
            await ShowAsync();

            var session = await streamUriService.GetOrCreateSessionAsync(indexedFileId, cancellationToken: playToken);

            if (generation != _playGeneration)
                return;

            if (session.Source is null)
            {
                await HideAsync();
                throw new InvalidOperationException("Streaming session did not return a source URI.");
            }

            SelectedAudioTrack = _audioTracks.FirstOrDefault(t => t.Index == session.PlaybackSettings.AudioTrackIndex)
                ?? SelectedAudioTrack;

            if (session.SubtitleTracks is { Count: > 0 })
                SetSubtitleTracks(session.SubtitleTracks);

            SelectedSubtitleTrack = session.PlaybackSettings.SubtitleTrackIndex is int subIdx
                ? _subtitleTracks.FirstOrDefault(t => t.Index == subIdx)
                : null;

            _baseManifestUrl = session.Source.Uri.OriginalString;

            Source = new PlayerSource
            {
                MediaId = mediaId,
                StreamSessionId = session.Id,
                IndexedFileId = indexedFileId,
                Url = BuildManifestUrlWithStartPosition(_baseManifestUrl, startPosition),
                MimeType = session.Source.MimeType,
                ThumbnailsUrl = thumbnailsUrl,
                Chapters = chapters ?? session.Chapters,
                Title = title,
                CoverUrl = coverUrl,
                PendingSeekTime = startPosition is > 0 ? startPosition : null
            };

            Play();
            AudioTrackChanged?.Invoke(SelectedAudioTrack);
            SubtitleTrackChanged?.Invoke(SelectedSubtitleTrack);
            QualityChanged?.Invoke(SelectedQuality);
        }
        catch (OperationCanceledException) when (generation != _playGeneration)
        {
        }
        catch (Exception) when (generation == _playGeneration)
        {
            await HideAsync();
            throw;
        }
    }

    public async Task PlayRemoteIndexedFileAsync(Guid remoteFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, double? startPosition = null, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _playGeneration);
        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var playToken = _playCts.Token;

        _currentIndexedFileId = null;
        _audioTracks = audioTracks.ToList();
        SetSubtitleTracks(subtitleTracks);
        SelectedSubtitleTrack = subtitleTrackIndex is int subIdx
            ? _subtitleTracks.FirstOrDefault(t => t.Index == subIdx)
            : null;
        SelectedAudioTrack = audioTrackIndex is int idx
            ? _audioTracks.FirstOrDefault(t => t.Index == idx)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        _availableQualities = videoResolution is not null
            ? VideoQualityOption.BuildOptionsForResolution(videoResolution.Value).ToList()
            : [];
        SelectedQuality = _availableQualities.FirstOrDefault(q => q.IsOriginal)
            ?? _availableQualities.FirstOrDefault();

        Source = new PlayerSource();

        try
        {
            await ShowAsync();

            var session = await streamUriService.GetOrCreateRemoteSessionAsync(remoteFileId, audioTrackIndex, playToken);

            if (generation != _playGeneration)
                return;

            if (session?.Source is null)
            {
                await HideAsync();
                return;
            }

            if (session.SubtitleTracks is { Count: > 0 })
                SetSubtitleTracks(session.SubtitleTracks);

            if (session.AudioTracks is { Count: > 0 })
            {
                _audioTracks = session.AudioTracks.ToList();
                SelectedAudioTrack = audioTrackIndex is int idxFromSession
                    ? _audioTracks.FirstOrDefault(t => t.Index == idxFromSession)
                    : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();
            }

            _baseManifestUrl = session.Source.Uri.OriginalString;

            Source = new PlayerSource
            {
                MediaId = mediaId,
                StreamSessionId = session.Id,
                Url = BuildManifestUrlWithStartPosition(_baseManifestUrl, startPosition),
                MimeType = session.Source.MimeType,
                Title = title,
                CoverUrl = coverUrl,
                ThumbnailsUrl = thumbnailsUrl,
                PendingSeekTime = startPosition is > 0 ? startPosition : null
            };

            Play();
            AudioTrackChanged?.Invoke(SelectedAudioTrack);
            SubtitleTrackChanged?.Invoke(SelectedSubtitleTrack);
            QualityChanged?.Invoke(SelectedQuality);
        }
        catch (OperationCanceledException) when (generation != _playGeneration)
        {
        }
        catch (Exception) when (generation == _playGeneration)
        {
            await HideAsync();
            throw;
        }
    }

    public void SetSubtitleTracks(IEnumerable<SubtitleFileTrackDto>? tracks)
    {
        _subtitleTracks = tracks?
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList() ?? [];
        SelectedSubtitleTrack = null;
        SubtitleTracksChanged?.Invoke();
    }

    public Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default)
    {
        if (_baseManifestUrl is null || Source is null)
            return Task.CompletedTask;

        var matched = _audioTracks.FirstOrDefault(t => t.Index == track.Index);
        if (matched is null)
            return Task.CompletedTask;

        SelectedAudioTrack = matched;
        AudioTrackChanged?.Invoke(matched);

        // Demuxed HLS: reload master with DefaultAudioTrackIndex. Do not also call
        // switchAudioTrack (VHS label match is unreliable, especially over federation).
        var seekTime = CurrentTime;
        var newUrl = BuildManifestUrlWithAudioTrack(_baseManifestUrl, matched.Index);
        newUrl = BuildManifestUrlWithQuality(newUrl, SelectedQuality);
        newUrl = BuildManifestUrlWithSubtitleSettings(newUrl, SelectedSubtitleTrack);
        _baseManifestUrl = newUrl;

        Source = new PlayerSource
        {
            MediaId = Source.MediaId,
            StreamSessionId = Source.StreamSessionId,
            IndexedFileId = Source.IndexedFileId,
            Url = BuildManifestUrlWithStartPosition(newUrl, seekTime),
            MimeType = Source.MimeType ?? "application/vnd.apple.mpegurl",
            ThumbnailsUrl = Source.ThumbnailsUrl,
            Chapters = Source.Chapters,
            Title = Source.Title,
            CoverUrl = Source.CoverUrl,
            PendingSeekTime = seekTime > 0 ? seekTime : null
        };

        return Task.CompletedTask;
    }

    public Task ChangeSubtitleTrackAsync(SubtitleFileTrackDto? track, CancellationToken cancellationToken = default)
    {
        if (_baseManifestUrl is null)
            return Task.CompletedTask;

        SelectedSubtitleTrack = track;
        SubtitleTrackChanged?.Invoke(track);

        if (!RequiresManifestReloadForSubtitleChange(track))
        {
            var slug = track is not null ? BuildSubtitleTrackSlug(track) : null;
            SwitchSubtitleTrackRequested?.Invoke(slug);
            return Task.CompletedTask;
        }

        var seekTime = CurrentTime;
        var newUrl = BuildManifestUrlWithSubtitleSettings(_baseManifestUrl, track);
        newUrl = BuildManifestUrlWithQuality(newUrl, SelectedQuality);
        if (SelectedAudioTrack is not null)
            newUrl = BuildManifestUrlWithAudioTrack(newUrl, SelectedAudioTrack.Index);
        _baseManifestUrl = newUrl;

        Source = new PlayerSource
        {
            MediaId = Source.MediaId,
            StreamSessionId = Source.StreamSessionId,
            IndexedFileId = Source.IndexedFileId,
            Url = BuildManifestUrlWithStartPosition(newUrl, seekTime),
            MimeType = Source.MimeType ?? "application/vnd.apple.mpegurl",
            ThumbnailsUrl = Source.ThumbnailsUrl,
            Chapters = Source.Chapters,
            Title = Source.Title,
            CoverUrl = Source.CoverUrl,
            PendingSeekTime = seekTime > 0 ? seekTime : null
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// Switches the video quality by rebuilding the manifest URL with the requested quality param.
    /// The player source changes, causing Video.js to reload the stream at the new resolution.
    /// </summary>
    public Task ChangeQualityAsync(VideoQualityOption? quality, CancellationToken cancellationToken = default)
    {
        if (_baseManifestUrl is null)
            return Task.CompletedTask;

        SelectedQuality = quality;
        QualityChanged?.Invoke(quality);

        var seekTime = CurrentTime;
        var newUrl = BuildManifestUrlWithQuality(_baseManifestUrl, quality);
        newUrl = BuildManifestUrlWithSubtitleSettings(newUrl, SelectedSubtitleTrack);
        if (SelectedAudioTrack is not null)
            newUrl = BuildManifestUrlWithAudioTrack(newUrl, SelectedAudioTrack.Index);
        _baseManifestUrl = newUrl;

        Source = new PlayerSource
        {
            MediaId = Source.MediaId,
            StreamSessionId = Source.StreamSessionId,
            IndexedFileId = Source.IndexedFileId,
            Url = BuildManifestUrlWithStartPosition(newUrl, seekTime),
            MimeType = Source.MimeType ?? "application/vnd.apple.mpegurl",
            ThumbnailsUrl = Source.ThumbnailsUrl,
            Chapters = Source.Chapters,
            Title = Source.Title,
            CoverUrl = Source.CoverUrl,
            PendingSeekTime = seekTime > 0 ? seekTime : null
        };

        return Task.CompletedTask;
    }

    public Task ShowAsync()
    {
        IsVisible = true;
        IsVisibleChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        if (PlaybackState is PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Buffering)
        {
            PlaybackState = PlaybackState.Idle;
        }

        IsVisible = false;
        IsVisibleChanged?.Invoke();
        return Task.CompletedTask;
    }

    public void OnBackPressed() => BackPressed?.Invoke();

    public void Play() => PlayRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Seek(double time) => SeekRequested?.Invoke(time);
    public void Mute() => MuteRequested?.Invoke();
    public void Unmute() => UnmuteRequest?.Invoke();
    public void SetVolume(double volume) => VolumeChangeRequested?.Invoke(volume);
    public void SetPlaybackRate(double rate) => PlaybackRateChangeRequested?.Invoke(rate);
    public void Stop() => StopRequested?.Invoke();
    public void EnterFullScreen() => EnterFullScreenRequested?.Invoke();
    public void ExitFullScreen() => ExitFullScreenRequested?.Invoke();

    public void SetAspectRatioMode(AspectRatioMode mode)
    {
        AspectRatio = mode;
        AspectRatioModeChanged?.Invoke(mode);
        AspectRatioModeChangeRequested?.Invoke(mode);
    }

    private static string BuildSubtitleTrackSlug(SubtitleFileTrackDto track) => $"sub-{track.Index}";

    private bool RequiresManifestReloadForSubtitleChange(SubtitleFileTrackDto? track)
    {
        if (IsSubtitleBurnInActive())
            return true;

        return track is { IsTextBased: false };
    }

    private bool IsSubtitleBurnInActive() =>
        Source?.Url?.Contains("SubtitleBurnInStreamIndex=", StringComparison.OrdinalIgnoreCase) == true
        || _baseManifestUrl?.Contains("SubtitleBurnInStreamIndex=", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Appends or replaces subtitle query parameters on the manifest URL.
    /// Text tracks use HLS sidecar subtitles; bitmap tracks (PGS) use server-side burn-in.
    /// </summary>
    private static string BuildManifestUrlWithSubtitleSettings(string baseUrl, SubtitleFileTrackDto? track)
    {
        var url = baseUrl;
        url = System.Text.RegularExpressions.Regex.Replace(url, @"[&?]DefaultSubtitleTrackIndex=[^&]*", "");
        url = System.Text.RegularExpressions.Regex.Replace(url, @"[&?]SubtitleBurnInStreamIndex=[^&]*", "");

        if (track is null)
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        if (track.IsTextBased)
            return $"{url}{separator}DefaultSubtitleTrackIndex={track.Index}";

        return $"{url}{separator}SubtitleBurnInStreamIndex={track.Index}";
    }

    private static string BuildManifestUrlWithAudioTrack(string baseUrl, int audioTrackIndex)
    {
        var url = System.Text.RegularExpressions.Regex.Replace(
            baseUrl, @"[&?]DefaultAudioTrackIndex=[^&]*", "");
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}DefaultAudioTrackIndex={audioTrackIndex}";
    }

    /// <summary>
    /// Appends or replaces the Quality query parameter on the manifest URL.
    /// </summary>
    private static string BuildManifestUrlWithQuality(string baseUrl, VideoQualityOption? quality)
    {
        var url = baseUrl;
        var qualityValue = quality is null || quality.IsOriginal ? (string?)null : quality.Label;

        // Strip existing Quality param
        url = System.Text.RegularExpressions.Regex.Replace(url, @"[&?]Quality=[^&]*", "");

        if (!string.IsNullOrEmpty(qualityValue))
        {
            var separator = url.Contains('?') ? "&" : "?";
            url += $"{separator}Quality={Uri.EscapeDataString(qualityValue)}";
        }

        return url;
    }

    /// <summary>
    /// Appends startSeconds so the HLS playlist can emit #EXT-X-START and avoid buffering from segment 0.
    /// </summary>
    private static string BuildManifestUrlWithStartPosition(string baseUrl, double? startPosition)
    {
        var url = System.Text.RegularExpressions.Regex.Replace(baseUrl, @"[&?]startSeconds=[^&]*", "");
        url = url.TrimEnd('?', '&');

        if (startPosition is not > 0)
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}startSeconds={startPosition.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public Task<bool> TryRecoverPlaybackStartAsync(bool allowQualityLadder = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public async Task AbortPlaybackStartAsync(string? messageKey = null, CancellationToken cancellationToken = default)
    {
        if (!IsVisible)
            return;

        PlaybackStartFailureMessageKey = messageKey
            ?? (Source?.StreamSessionId is not null ? "StreamPlaybackTimedOut" : "StreamNotReady");

        Stop();
        await HideAsync();
        PlaybackStartFailed?.Invoke();
    }
}
