using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.MAUI.Services;

internal class PlayerService(
    IStreamUriService streamUriService,
    IDeviceStorageService deviceStorageService) : IPlayerService
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
    public event Action<string>? SwitchAudioTrackRequested;
    public event Action<string?>? SwitchSubtitleTrackRequested;
    public event Action<AspectRatioMode>? AspectRatioModeChangeRequested;

    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
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

    private PlayerSource _source = new();
    public PlayerSource Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                var keepClock = _source.StreamSessionId is { } sessionId
                    && value.StreamSessionId == sessionId
                    && _source.IndexedFileId is { } fileId
                    && value.IndexedFileId == fileId
                    && Duration > 1;
                var keepTime = CurrentTime;
                var keepDuration = Duration;
                _source = value;
                if (!keepClock)
                {
                    CurrentTime = 0;
                    // Apply metadata duration before SourceChanged: Windows VLC opens there.
                    Duration = value.KnownDurationSeconds is double known && known > 1
                        ? known
                        : 0;
                }

                BufferedTime = 0;
                PlaybackState = PlaybackState.Idle;
                SourceChanged?.Invoke(value);
                if (keepClock)
                {
                    if (keepTime > 0)
                        CurrentTime = keepTime;
                    Duration = keepDuration;
                }
            }
        }
    }

    public bool IsVisible { get; private set; } = false;

    private PlaybackState _playbackState = PlaybackState.Unknown;
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            if (_playbackState != value)
            {
                _playbackState = value;
                PlaybackStateChanged?.Invoke(value);
            }
        }
    }

    private bool _isFullScreen = false;
    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (_isFullScreen != value)
            {
                _isFullScreen = value;
                IsFullScreenChanged?.Invoke(value);
            }
        }
    }

    private double _duration = 0;
    public double Duration
    {
        get => _duration;
        set
        {
            if (_duration != value)
            {
                _duration = value;
                DurationChanged?.Invoke(value);
            }
        }
    }

    private double _currentTime = 0;
    private double _lastKnownPlaybackTime;
    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime != value)
            {
                _currentTime = value;
                if (value > 1)
                    _lastKnownPlaybackTime = value;
                CurrentTimeChanged?.Invoke(value);
            }
        }
    }

    private double _bufferedTime = 0;
    public double BufferedTime
    {
        get => _bufferedTime;
        set
        {
            if (_bufferedTime != value)
            {
                _bufferedTime = value;
                BufferedTimeChanged?.Invoke(value);
            }
        }
    }

    private double _volume = deviceStorageService.Get(PreferenceKeys.PLAYER_VOLUME, 1);
    public double Volume
    {
        get => _volume;
        set
        {
            if (_volume != value)
            {
                _volume = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_VOLUME, value);
                VolumeChanged?.Invoke(value);
            }
        }
    }

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

    private double _playbackRate = deviceStorageService.Get(PreferenceKeys.PLAYER_PLAYBACK_RATE, 1);
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate != value)
            {
                _playbackRate = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_PLAYBACK_RATE, value);
                PlaybackRateChanged?.Invoke(value);
            }
        }
    }

    private bool _isMuted = deviceStorageService.Get(PreferenceKeys.PLAYER_IS_MUTED, false);
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_IS_MUTED, value);
                IsMutedChanged?.Invoke(value);
            }
        }
    }

    private Guid? _currentIndexedFileId;
    private List<AudioFileTrackDto> _audioTracks = [];
    public IReadOnlyList<AudioFileTrackDto> AudioTracks => _audioTracks;

    private AudioFileTrackDto? _selectedAudioTrack;
    public AudioFileTrackDto? SelectedAudioTrack => _selectedAudioTrack;

    private List<SubtitleFileTrackDto> _subtitleTracks = [];
    public IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks => _subtitleTracks;

    private SubtitleFileTrackDto? _selectedSubtitleTrack;
    public SubtitleFileTrackDto? SelectedSubtitleTrack => _selectedSubtitleTrack;

    private List<VideoQualityOption> _availableQualities = [];
    public IReadOnlyList<VideoQualityOption> AvailableQualities => _availableQualities;

    private VideoQualityOption? _selectedQuality;
    public VideoQualityOption? SelectedQuality => _selectedQuality;

    private AspectRatioMode _aspectRatioMode = AspectRatioMode.Fit;
    public AspectRatioMode AspectRatio => _aspectRatioMode;

    /// <summary>
    /// Base manifest URL (without Quality param) used to rebuild the source when switching quality.
    /// </summary>
    private string? _baseManifestUrl;

    private int _playbackStartRecoveryAttempts;
    private const int MaxPlaybackStartRecoveryAttempts = 4;
    // Windows Video.js only: avoid stacking burn-in jobs / reload thrash on hard SRC_NOT_SUPPORTED.
    private static readonly TimeSpan MinQualityFallbackInterval = TimeSpan.FromSeconds(25);
    private DateTime _lastQualityFallbackUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _playbackStartRecoveryLock = new(1, 1);

    public async Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, double? startPosition = null, IReadOnlyList<ChapterMarkerDto>? chapters = null, double? durationSeconds = null, CancellationToken cancellationToken = default)
    {
        _currentIndexedFileId = indexedFileId;
        _lastKnownPlaybackTime = startPosition is > 1 ? startPosition.Value : 0;
        _audioTracks = audioTracks.ToList();
        SetSubtitleTracks(subtitleTracks);
        _selectedSubtitleTrack = subtitleTrackIndex is int subInit
            ? _subtitleTracks.FirstOrDefault(t => t.Index == subInit)
            : null;
        _selectedAudioTrack = audioTrackIndex is int idx
            ? _audioTracks.FirstOrDefault(t => t.Index == idx)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        _availableQualities = videoResolution is not null
            ? VideoQualityOption.BuildOptionsForResolution(videoResolution.Value).ToList()
            : [];
        _selectedQuality = SelectInitialQuality(_availableQualities);

        Source = new PlayerSource();

        await ShowAsync();

        var session = await streamUriService.GetOrCreateSessionAsync(
            indexedFileId, audioTrackIndex, subtitleTrackIndex, cancellationToken);

        if (session.Source is null)
        {
            throw new InvalidOperationException("Streaming session did not return a source URI.");
        }

        if (session.SubtitleTracks is { Count: > 0 })
            SetSubtitleTracks(session.SubtitleTracks);

        PlaybackSessionTrackSelection.Apply(
            _audioTracks,
            _subtitleTracks,
            session.PlaybackSettings,
            audioTrackIndex,
            subtitleTrackIndex,
            out _selectedAudioTrack,
            out _selectedSubtitleTrack);

        _baseManifestUrl = session.Source.Uri.OriginalString;
        _playbackStartRecoveryAttempts = 0;
        _lastQualityFallbackUtc = DateTime.MinValue;
        PlaybackStartFailureMessageKey = null;

        var manifestUrl = BuildManifestUrlWithQuality(_baseManifestUrl, _selectedQuality);

        var resolvedChapters = chapters ?? session.Chapters;
        var playerSource = new PlayerSource
        {
            MediaId = mediaId,
            StreamSessionId = session.Id,
            IndexedFileId = indexedFileId,
            Url = BuildManifestUrlWithStartPosition(manifestUrl, startPosition),
            MimeType = session.Source.MimeType,
            ThumbnailsUrl = thumbnailsUrl,
            Chapters = resolvedChapters,
            KnownDurationSeconds = ResolveKnownDurationSeconds(durationSeconds, resolvedChapters),
            Title = title,
            CoverUrl = coverUrl,
            PendingSeekTime = startPosition is > 0 ? startPosition : null
        };

        Source = playerSource;
        AudioTrackChanged?.Invoke(_selectedAudioTrack);
        SubtitleTrackChanged?.Invoke(_selectedSubtitleTrack);
        QualityChanged?.Invoke(_selectedQuality);
    }

    public async Task PlayRemoteIndexedFileAsync(Guid remoteFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, double? startPosition = null, CancellationToken cancellationToken = default)
    {
        _currentIndexedFileId = null;
        _lastKnownPlaybackTime = startPosition is > 1 ? startPosition.Value : 0;
        _audioTracks = audioTracks.ToList();
        SetSubtitleTracks(subtitleTracks);
        _selectedSubtitleTrack = subtitleTrackIndex is int subIdx2
            ? _subtitleTracks.FirstOrDefault(t => t.Index == subIdx2)
            : null;
        _selectedAudioTrack = audioTrackIndex is int idx2
            ? _audioTracks.FirstOrDefault(t => t.Index == idx2)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        _availableQualities = videoResolution is not null
            ? VideoQualityOption.BuildOptionsForResolution(videoResolution.Value).ToList()
            : [];
        _selectedQuality = SelectInitialQuality(_availableQualities);

        Source = new PlayerSource();

        await ShowAsync();

        var session = await streamUriService.GetOrCreateRemoteSessionAsync(
            remoteFileId, audioTrackIndex, subtitleTrackIndex, cancellationToken);

        if (session?.Source is null)
        {
            return;
        }

        if (session.AudioTracks is { Count: > 0 })
            _audioTracks = session.AudioTracks.ToList();

        if (session.SubtitleTracks is { Count: > 0 })
            SetSubtitleTracks(session.SubtitleTracks);

        PlaybackSessionTrackSelection.Apply(
            _audioTracks,
            _subtitleTracks,
            session.PlaybackSettings,
            audioTrackIndex,
            subtitleTrackIndex,
            out _selectedAudioTrack,
            out _selectedSubtitleTrack);

        _baseManifestUrl = session.Source.Uri.OriginalString;
        _playbackStartRecoveryAttempts = 0;
        _lastQualityFallbackUtc = DateTime.MinValue;
        PlaybackStartFailureMessageKey = null;

        var manifestUrl = BuildManifestUrlWithQuality(_baseManifestUrl, _selectedQuality);

        Source = new PlayerSource
        {
            MediaId = mediaId,
            StreamSessionId = session.Id,
            Url = BuildManifestUrlWithStartPosition(manifestUrl, startPosition),
            MimeType = session.Source.MimeType,
            Title = title,
            CoverUrl = coverUrl,
            ThumbnailsUrl = thumbnailsUrl,
            PendingSeekTime = startPosition is > 0 ? startPosition : null
        };

        AudioTrackChanged?.Invoke(_selectedAudioTrack);
        SubtitleTrackChanged?.Invoke(_selectedSubtitleTrack);
        QualityChanged?.Invoke(_selectedQuality);
    }

    public void SetSubtitleTracks(IEnumerable<SubtitleFileTrackDto>? tracks)
    {
        _subtitleTracks = tracks?
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList() ?? [];
        _selectedSubtitleTrack = null;
        SubtitleTracksChanged?.Invoke();
    }

    public Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default)
    {
        if (_baseManifestUrl is null || Source is null)
            return Task.CompletedTask;

        var matched = _audioTracks.FirstOrDefault(t => t.Index == track.Index);
        if (matched is null)
            return Task.CompletedTask;

        _selectedAudioTrack = matched;
        AudioTrackChanged?.Invoke(matched);

        if (!StreamingSourceKind.IsHls(Source.MimeType, Source.Url))
        {
            SwitchAudioTrackRequested?.Invoke(BuildAudioTrackSlug(matched));
            return Task.CompletedTask;
        }

#if ANDROID
        // Demuxed HLS on Exo: all AUDIO renditions are already in the master. Reload cuts
        // audio; in-player override keeps the previous language until the new one buffers.
        if (_baseManifestUrl is not null)
        {
            _baseManifestUrl = BuildManifestUrlWithAudioTrack(_baseManifestUrl, matched.Index);
            _baseManifestUrl = BuildManifestUrlWithQuality(_baseManifestUrl, _selectedQuality);
            _baseManifestUrl = BuildManifestUrlWithSubtitleSettings(
                _baseManifestUrl, _selectedSubtitleTrack);
        }

        SwitchAudioTrackRequested?.Invoke(BuildAudioTrackSlug(matched));
        return Task.CompletedTask;
#else
        var seekTime = CaptureResumeTime();
        var previousDuration = Duration;
        var newUrl = BuildManifestUrlWithAudioTrack(_baseManifestUrl, matched.Index);
        newUrl = BuildManifestUrlWithQuality(newUrl, _selectedQuality);
        newUrl = BuildManifestUrlWithSubtitleSettings(newUrl, _selectedSubtitleTrack);
        _baseManifestUrl = newUrl;

        ReplaceStreamingSource(
            BuildManifestUrlWithStartPosition(newUrl, seekTime),
            seekTime > 0 ? seekTime : null);

        if (seekTime > 0)
        {
            CurrentTime = seekTime;
            Duration = previousDuration;
        }

        ResumeWebPlaybackIfNeeded();

        return Task.CompletedTask;
#endif
    }

    public Task ChangeSubtitleTrackAsync(SubtitleFileTrackDto? track, CancellationToken cancellationToken = default)
    {
        _selectedSubtitleTrack = track;
        SubtitleTrackChanged?.Invoke(track);

        if (!RequiresManifestReloadForSubtitleChange(track))
        {
            var slug = track is not null ? BuildSubtitleTrackSlug(track) : null;
            SwitchSubtitleTrackRequested?.Invoke(slug);
            return Task.CompletedTask;
        }

        if (_baseManifestUrl is null)
            return Task.CompletedTask;

        if (!StreamingSourceKind.IsHls(Source?.MimeType, _baseManifestUrl)
            && !TryPromoteDirectToHls())
        {
            return Task.CompletedTask;
        }

        var seekTime = CaptureResumeTime();
        var previousDuration = Duration;

        var newUrl = BuildManifestUrlWithSubtitleSettings(_baseManifestUrl, track);
        newUrl = BuildManifestUrlWithQuality(newUrl, _selectedQuality);
        if (_selectedAudioTrack is not null)
            newUrl = BuildManifestUrlWithAudioTrack(newUrl, _selectedAudioTrack.Index);
        _baseManifestUrl = newUrl;

        ReplaceStreamingSource(
            BuildManifestUrlWithStartPosition(newUrl, seekTime),
            seekTime > 0 ? seekTime : null);

        if (seekTime > 0)
        {
            CurrentTime = seekTime;
            Duration = previousDuration;
        }

        ResumeWebPlaybackIfNeeded();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Switches the video quality by rebuilding the manifest URL with the requested quality param.
    /// </summary>
    public Task ChangeQualityAsync(VideoQualityOption? quality, CancellationToken cancellationToken = default)
    {
        if (_baseManifestUrl is null)
            return Task.CompletedTask;

        // Explicit user quality changes reset Windows Video.js recovery budget for the new selection.
        _playbackStartRecoveryAttempts = 0;
        var previousQuality = _selectedQuality;

        if (quality is { IsOriginal: false }
            && !StreamingSourceKind.IsHls(Source?.MimeType, _baseManifestUrl)
            && !TryPromoteDirectToHls())
        {
            NativeVideoDebug.Log(
                "ChangeQuality promote fail session="
                + (Source?.StreamSessionId?.ToString("D") ?? "null")
                + " base="
                + SummarizePlaybackUrl(_baseManifestUrl));
            // Keep UI + stream on the previous quality when Direct cannot become HLS.
            return Task.CompletedTask;
        }

        _selectedQuality = quality;
        QualityChanged?.Invoke(quality);

        if (quality is null || quality.IsOriginal)
            TryDemoteHlsToDirect();

        var seekTime = CaptureResumeTime();
        var previousDuration = Duration;

        var newUrl = BuildManifestUrlWithQuality(_baseManifestUrl, quality);
        newUrl = BuildManifestUrlWithSubtitleSettings(newUrl, _selectedSubtitleTrack);
        if (_selectedAudioTrack is not null)
            newUrl = BuildManifestUrlWithAudioTrack(newUrl, _selectedAudioTrack.Index);
#if WINDOWS
        // Direct->HLS promotes start without GetStreamUri Video.js flags.
        if (StreamingSourceKind.IsHls("application/vnd.apple.mpegurl", newUrl))
            newUrl = StreamingSourceKind.EnsureVideoJsHlsManifestQuery(newUrl);
#endif
        _baseManifestUrl = newUrl;

        NativeVideoDebug.Log(
            "ChangeQuality label="
            + (quality?.Label ?? "null")
            + " original="
            + (quality?.IsOriginal ?? false)
            + " prev="
            + (previousQuality?.Label ?? "null")
            + " url="
            + SummarizePlaybackUrl(newUrl));

        ReplaceStreamingSource(
            BuildManifestUrlWithStartPosition(newUrl, seekTime),
            seekTime > 0 ? seekTime : null);

        // Restore time/duration so the overlay keeps showing the correct position
        // while the new quality loads
        if (seekTime > 0)
        {
            CurrentTime = seekTime;
            Duration = previousDuration;
        }

        ResumeWebPlaybackIfNeeded();

        return Task.CompletedTask;
    }

    public async Task<bool> TryRecoverPlaybackStartAsync(bool allowQualityLadder = false, CancellationToken cancellationToken = default)
    {
        // Web Video.js watchdog lives in VideoPlayer.razor. Native LibVLC / MediaElement
        // do not use this ABR ladder.
        if (!WindowsVideoPlayback.ShouldUseWebVideoPlayer(Source?.MimeType, Source?.Url))
            return false;

        if (!IsVisible || string.IsNullOrEmpty(Source?.Url) || _baseManifestUrl is null)
            return false;

        await _playbackStartRecoveryLock.WaitAsync(cancellationToken);
        try
        {
            if (_playbackStartRecoveryAttempts >= MaxPlaybackStartRecoveryAttempts)
                return false;

            // Growing buffer / playing: black frames are a display issue, not a ladder issue.
            if (BufferedTime > 0
                || CurrentTime > 0
                || PlaybackState is PlaybackState.Playing)
            {
                return true;
            }

            if (!allowQualityLadder)
                return false;

            var sinceLastFallback = DateTime.UtcNow - _lastQualityFallbackUtc;
            if (_lastQualityFallbackUtc != DateTime.MinValue
                && sinceLastFallback < MinQualityFallbackInterval)
            {
                return true;
            }

            _playbackStartRecoveryAttempts++;

            if (_selectedQuality?.IsOriginal == true)
            {
                var fallbackQuality = _availableQualities.FirstOrDefault(q => !q.IsOriginal);
                if (fallbackQuality is not null)
                {
                    _lastQualityFallbackUtc = DateTime.UtcNow;
                    await ChangeQualityAsync(fallbackQuality, cancellationToken);
                    return true;
                }
            }

            var nextQuality = GetNextLowerTranscodedQuality();
            if (nextQuality is not null)
            {
                _lastQualityFallbackUtc = DateTime.UtcNow;
                await ChangeQualityAsync(nextQuality, cancellationToken);
                return true;
            }

            if (_playbackStartRecoveryAttempts <= 2)
            {
                _lastQualityFallbackUtc = DateTime.UtcNow;
                ReloadCurrentSource();
                return true;
            }

            return false;
        }
        finally
        {
            _playbackStartRecoveryLock.Release();
        }
    }

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

    private void ReloadCurrentSource()
    {
        if (_baseManifestUrl is null || Source is null)
            return;

        var seekTime = CaptureResumeTime();
        if (seekTime <= 1)
            seekTime = Source.PendingSeekTime ?? 0;
        var previousDuration = Duration;

        var url = BuildManifestUrlWithStartPosition(
            BuildManifestUrlWithQuality(
                BuildManifestUrlWithSubtitleSettings(_baseManifestUrl, _selectedSubtitleTrack),
                _selectedQuality),
            seekTime);

        ReplaceStreamingSource(url, seekTime > 0 ? seekTime : null);

        if (seekTime > 0)
        {
            CurrentTime = seekTime;
            if (previousDuration > 0)
                Duration = previousDuration;
        }
    }

    /// <summary>
    /// Rebuilds the HLS source URL without dropping session metadata. Quality / audio /
    /// subtitle reloads were dropping chapters and sprite thumbnails.
    /// </summary>
    private void ReplaceStreamingSource(string url, double? pendingSeekTime)
    {
        Source = new PlayerSource
        {
            MediaId = Source.MediaId,
            StreamSessionId = Source.StreamSessionId,
            IndexedFileId = Source.IndexedFileId,
            Url = url,
            MimeType = StreamingSourceKind.IsHls(Source.MimeType, url)
                ? "application/vnd.apple.mpegurl"
                : Source.MimeType ?? "application/vnd.apple.mpegurl",
            ThumbnailsUrl = Source.ThumbnailsUrl,
            Chapters = Source.Chapters,
            KnownDurationSeconds = Source.KnownDurationSeconds,
            Title = Source.Title,
            CoverUrl = Source.CoverUrl,
            PendingSeekTime = pendingSeekTime
        };
    }

    private static string SummarizePlaybackUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "-";
        if (LocalPlaybackUrl.IsLocalFile(url))
            return "file";
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var query = uri.Query;
            if (query.Length > 80)
                query = query[..80] + "...";
            return path + query;
        }
        catch (UriFormatException)
        {
            return url.Length > 96 ? url[..96] + "..." : url;
        }
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
    public void Mute()
    {
        IsMuted = true;
        MuteRequested?.Invoke();
    }

    public void Unmute()
    {
        IsMuted = false;
        UnmuteRequest?.Invoke();
    }
    public void SetVolume(double volume)
    {
        Volume = Math.Clamp(volume, 0, 1);
        VolumeChangeRequested?.Invoke(Volume);
    }
    public void SetPlaybackRate(double rate) => PlaybackRateChangeRequested?.Invoke(rate);

    public void Stop() => StopRequested?.Invoke();
    public void EnterFullScreen() => EnterFullScreenRequested?.Invoke();
    public void ExitFullScreen() => ExitFullScreenRequested?.Invoke();

    public void SetAspectRatioMode(AspectRatioMode mode)
    {
        _aspectRatioMode = mode;
        AspectRatioModeChanged?.Invoke(mode);
        AspectRatioModeChangeRequested?.Invoke(mode);
    }

    private double CaptureResumeTime()
    {
        if (CurrentTime > 1)
            return CurrentTime;
        if (_lastKnownPlaybackTime > 1)
            return _lastKnownPlaybackTime;
        return Source?.PendingSeekTime is double pending && pending > 1 ? pending : 0;
    }

    public double GetResumePosition() => CaptureResumeTime();

    private static string BuildAudioTrackSlug(AudioFileTrackDto track) => $"audio-{track.Index}";

    private static string BuildSubtitleTrackSlug(SubtitleFileTrackDto track) => $"sub-{track.Index}";

    private void ResumeWebPlaybackIfNeeded()
    {
        if (!IsVisible)
            return;

        if (!WindowsVideoPlayback.ShouldUseWebVideoPlayer(Source?.MimeType, Source?.Url))
            return;

        PlaybackState = PlaybackState.Buffering;
        Play();
    }

    private bool TryPromoteDirectToHls()
    {
        // Windows HLS is Video.js (MSE); Android LibVLC needs video+audio CODECS.
        var videoJsCompatible =
#if WINDOWS
            true;
#else
            false;
#endif

        if (!StreamingSourceKind.TryBuildHlsManifestUrl(
                _baseManifestUrl,
                Source?.StreamSessionId,
                out var hlsUrl,
                videoJsCompatible))
        {
            return false;
        }

        NativeVideoDebug.Log(
            "PromoteDirectToHls session="
            + Source!.StreamSessionId!.Value.ToString("D")
            + " url="
            + SummarizePlaybackUrl(hlsUrl));
        _baseManifestUrl = hlsUrl;
        if (Source is not null)
            Source.MimeType = "application/vnd.apple.mpegurl";
        return true;
    }

    private bool TryDemoteHlsToDirect()
    {
        if (!StreamingSourceKind.TryBuildDirectStreamUrl(_baseManifestUrl, out var directUrl))
            return false;

        NativeVideoDebug.Log("DemoteHlsToDirect url=" + SummarizePlaybackUrl(directUrl));
        _baseManifestUrl = directUrl;
        if (Source is not null)
            Source.MimeType = "video/mp4";
        return true;
    }

    private bool RequiresManifestReloadForSubtitleChange(SubtitleFileTrackDto? track)
    {
        if (IsSubtitleBurnInActive())
            return true;

        // Image subs (PGS) in the muxed file: Android LibVLC can SetSpu / :sub-track.
        // Promoting Direct Play to HLS burn-in restarts at 0 and drops the audio ES.
        // HLS has no PGS rendition, so burn-in is still required there.
        if (track is not { IsTextBased: false })
            return false;

        return StreamingSourceKind.IsHls(Source?.MimeType, Source?.Url ?? _baseManifestUrl);
    }

    private bool IsSubtitleBurnInActive() =>
        Source?.Url?.Contains("SubtitleBurnInStreamIndex=", StringComparison.OrdinalIgnoreCase) == true
        || _baseManifestUrl?.Contains("SubtitleBurnInStreamIndex=", StringComparison.OrdinalIgnoreCase) == true;

    private static string BuildManifestUrlWithSubtitleSettings(string baseUrl, SubtitleFileTrackDto? track)
    {
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(baseUrl, out var localPath))
            return localPath;

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
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(baseUrl, out var localPath))
            return localPath;

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
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(baseUrl, out var localPath))
            return localPath;

        var url = baseUrl;
        var qualityValue = quality is null || quality.IsOriginal ? (string?)null : quality.Label;

        url = System.Text.RegularExpressions.Regex.Replace(url, @"[&?]Quality=[^&]*", "");

        if (!string.IsNullOrEmpty(qualityValue))
        {
            var separator = url.Contains('?') ? "&" : "?";
            url += $"{separator}Quality={Uri.EscapeDataString(qualityValue)}";
        }

        return url;
    }

    private static VideoQualityOption? SelectInitialQuality(IReadOnlyList<VideoQualityOption> availableQualities)
    {
        if (availableQualities.Count == 0)
            return null;

        return availableQualities.FirstOrDefault(q => q.IsOriginal)
            ?? availableQualities.FirstOrDefault();
    }

    private VideoQualityOption? GetNextLowerTranscodedQuality()
    {
        if (_selectedQuality is null || _selectedQuality.IsOriginal)
            return null;

        var transcodedQualities = _availableQualities.Where(q => !q.IsOriginal).ToList();
        var currentIndex = transcodedQualities.FindIndex(q => q.Height == _selectedQuality.Height);
        if (currentIndex < 0 || currentIndex >= transcodedQualities.Count - 1)
            return null;

        return transcodedQualities[currentIndex + 1];
    }

    private static string BuildManifestUrlWithStartPosition(string baseUrl, double? startPosition)
    {
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(baseUrl, out var localPath))
            return localPath;

        var url = System.Text.RegularExpressions.Regex.Replace(baseUrl, @"[&?]startSeconds=[^&]*", "");
        url = url.TrimEnd('?', '&');

        if (startPosition is not double seconds || seconds <= 0)
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}startSeconds={seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private static double? ResolveKnownDurationSeconds(
        double? durationSeconds,
        IReadOnlyList<ChapterMarkerDto>? chapters)
    {
        if (durationSeconds is double known && known > 1)
            return known;

        if (chapters is null || chapters.Count == 0)
            return null;

        var end = chapters.Max(c => c.EndSeconds ?? c.StartSeconds);
        return end > 1 ? end : null;
    }
}
