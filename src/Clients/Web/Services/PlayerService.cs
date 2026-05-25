using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
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
    public event Action<string>? SwitchAudioTrackRequested;
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
    public event Action<VideoQualityOption?>? QualityChanged;
    public event Action<AspectRatioMode>? AspectRatioModeChanged;

    public event Action? BackPressed;

    private PlayerSource _source = new();
    public PlayerSource Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                _source = value;
                CurrentTime = 0;
                Duration = 0;
                BufferedTime = 0;
                PlaybackState = PlaybackState.Idle;
                SourceChanged?.Invoke(value);
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
    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime != value)
            {
                _currentTime = value;
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

    public async Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, CancellationToken cancellationToken = default)
    {
        _currentIndexedFileId = indexedFileId;
        _audioTracks = audioTracks.ToList();
        _subtitleTracks = subtitleTracks
            ?.Where(t => t.IsTextBased)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList() ?? [];
        _selectedSubtitleTrack = null; // TODO - Maybe not default to null
        _selectedAudioTrack = audioTrackIndex is int idx
            ? _audioTracks.FirstOrDefault(t => t.Index == idx)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        // Build quality options from the source resolution
        _availableQualities = videoResolution is not null
            ? VideoQualityOption.BuildOptionsForResolution(videoResolution.Value).ToList()
            : [];
        _selectedQuality = _availableQualities.FirstOrDefault(q => q.IsOriginal)
            ?? _availableQualities.FirstOrDefault();

        Source = new PlayerSource();

        await ShowAsync();
        Play();

        var session = await streamUriService.GetOrCreateSessionAsync(indexedFileId, cancellationToken: cancellationToken);

        if (session.Source is null)
        {
            throw new InvalidOperationException("Streaming session did not return a source URI.");
        }

        // Update selected tracks based on server's auto-selection
        _selectedAudioTrack = _audioTracks.FirstOrDefault(t => t.Index == session.PlaybackSettings.AudioTrackIndex)
            ?? _selectedAudioTrack;
        _selectedSubtitleTrack = session.PlaybackSettings.SubtitleTrackIndex is int subIdx
            ? _subtitleTracks.FirstOrDefault(t => t.Index == subIdx)
            : null;

        _baseManifestUrl = session.Source.Uri.OriginalString;

        var playerSource = new PlayerSource
        {
            MediaId = mediaId,
            Url = _baseManifestUrl,
            MimeType = session.Source.MimeType,
            ThumbnailsUrl = thumbnailsUrl
        };

        Source = playerSource;
        AudioTrackChanged?.Invoke(_selectedAudioTrack);
        SubtitleTrackChanged?.Invoke(_selectedSubtitleTrack);
        QualityChanged?.Invoke(_selectedQuality);
    }

    public Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default)
    {
        if (_currentIndexedFileId is null)
        {
            return Task.CompletedTask;
        }

        if (!_audioTracks.Contains(track))
        {
            return Task.CompletedTask;
        }

        _selectedAudioTrack = track;
        AudioTrackChanged?.Invoke(track);

        var trackName = track.Name ?? $"Track {track.Index}";
        SwitchAudioTrackRequested?.Invoke(trackName);

        return Task.CompletedTask;
    }

    public Task ChangeSubtitleTrackAsync(SubtitleFileTrackDto? track, CancellationToken cancellationToken = default)
    {
        if (_currentIndexedFileId is null)
        {
            return Task.CompletedTask;
        }

        _selectedSubtitleTrack = track;
        SubtitleTrackChanged?.Invoke(track);

        var slug = track is not null ? BuildSubtitleTrackSlug(track) : null;
        SwitchSubtitleTrackRequested?.Invoke(slug);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Switches the video quality by rebuilding the manifest URL with the requested quality param.
    /// The player source changes, causing Video.js to reload the stream at the new resolution.
    /// </summary>
    public Task ChangeQualityAsync(VideoQualityOption? quality, CancellationToken cancellationToken = default)
    {
        if (_currentIndexedFileId is null || _baseManifestUrl is null)
        {
            return Task.CompletedTask;
        }

        _selectedQuality = quality;
        QualityChanged?.Invoke(quality);

        // Save current playback position before changing source
        var seekTime = CurrentTime;

        // Rebuild the manifest URL with the Quality parameter
        var newUrl = BuildManifestUrlWithQuality(_baseManifestUrl, quality);

        Source = new PlayerSource
        {
            Url = newUrl,
            MimeType = "application/vnd.apple.mpegurl",
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
        _aspectRatioMode = mode;
        AspectRatioModeChanged?.Invoke(mode);
        AspectRatioModeChangeRequested?.Invoke(mode);
    }

    // TODO - Maybe slugify on file indexing 
    private static string BuildSubtitleTrackSlug(SubtitleFileTrackDto track) => $"sub-{track.Index}";

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
}
