using System.Globalization;
using System.Text;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public enum FullScreenView { Player, Lyrics, Queue, Info, SyncPlay }
public enum QueueTab { UpNext, Previous, Similar, Suggestions }

public partial class FullScreenMusicPlayer : IAsyncDisposable
{
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ICastOrchestrationService CastOrchestration { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;
    [Inject] private IRemoteControlService RemoteControl { get; set; } = default!;
    [Inject] private ISyncPlayService SyncPlay { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    private ElementReference _playerRef;
    private ElementReference _seekBarRef;
    private bool _isDragging;
    private bool _showVolumeControls = true;
    private bool _isTv;
    private bool _visualizerAvailable;
    private bool _isScrubbing;
    private double _scrubTime;
    private BoundingRect? _seekBarBounds;
    private FullScreenView _view;
    private string? _lyricsLrc;
    private string? _lyrics;
    private float[]? _waveformPeaks;
    private string? _waveformMaskStyle;
    private string? _prevWaveformState;
    private double _waveformScale = 1;
    private double _waveformOpacity = 1;
    private string? _uiTitle;
    private string? _uiArtist;
    private string? _uiAlbumTitle;
    private string? _uiCoverUrl;
    private string? _uiDominantColor;
    private DateOnly? _uiReleaseDate;
    private Guid? _uiMediaId;
    private int? _uiUserRating;
    private bool _uiCommitted;
    private Guid? _detailsLoadedForMediaId;
    private DotNetObjectReference<FullScreenMusicPlayer>? _dotNetRef;
    private bool _menuOpen;
    private bool _sleepTimerSubmenuOpen;
    private bool _visualizerEnabled;
    private bool _musicIntelligenceAvailable;
    private List<AudioQueueItem> _similarTracks = [];
    private List<AudioQueueItem> _suggestionTracks = [];
    private bool _similarLoading;
    private bool _suggestionsLoading;
    private Guid? _similarLoadedForTrackId;
    private Guid? _suggestionsLoadedForTrackId;
    private ElementReference _visualizerCanvas;
    private MusicTrackDto? _trackDetails;
    private AudioFileMetadataDto? _audioMetadata;
    private long _fileSize;
    private QueueTab _queueTab;
    private IReadOnlyList<TabOption<QueueTab>> _queueTabOptions => _musicIntelligenceAvailable
        ? [new(QueueTab.UpNext, S["UpNext"]), new(QueueTab.Previous, S["Previous"]), new(QueueTab.Similar, S["Similar"]), new(QueueTab.Suggestions, S["Suggestions"])]
        : [new(QueueTab.UpNext, S["UpNext"]), new(QueueTab.Previous, S["Previous"])];

    private double DisplayPercent => _isScrubbing && DisplayDuration > 0
        ? (_scrubTime / DisplayDuration) * 100
        : CurrentPercent;

    private double CurrentPercent => DisplayDuration > 0 ? (DisplayPosition / DisplayDuration) * 100 : 0;
    private double BufferedPercent => !IsRemoteMode && Audio.Duration > 0 ? (Audio.BufferedTime / Audio.Duration) * 100 : 0;

    private bool IsRemoteMode => RemoteControl.IsControlling && RemoteControl.IsAudio;
    private bool IsSyncPlayMode => SyncPlay.IsInGroup;

    private string? DisplayTitle => IsRemoteMode
        ? RemoteControl.Title
        : _uiCommitted ? _uiTitle : Audio.CurrentTrack?.Title;
    private string? DisplayArtist => IsRemoteMode
        ? RemoteControl.Artist
        : _uiCommitted ? _uiArtist : Audio.CurrentTrack?.Artist;
    private string? DisplayAlbumTitle => IsRemoteMode
        ? RemoteControl.AlbumTitle
        : _uiCommitted ? _uiAlbumTitle : Audio.CurrentTrack?.AlbumTitle;
    private string? DisplayCoverUrl => ToFullscreenCoverUrl(
        IsRemoteMode
            ? RemoteControl.CoverUrl
            : _uiCommitted ? _uiCoverUrl : Audio.CurrentTrack?.CoverUrl);
    private DateOnly? DisplayReleaseDate => IsRemoteMode ? null : _uiReleaseDate;
    private double DisplayPosition => IsRemoteMode ? RemoteControl.Position : Audio.CurrentTime;
    private double DisplayDuration => IsRemoteMode ? RemoteControl.Duration : Audio.Duration;
    private double DisplayVolume => IsRemoteMode ? RemoteControl.Volume : Audio.Volume;

    private string? DominantColorStyle
    {
        get
        {
            if (IsRemoteMode)
                return null;

            var raw = _uiCommitted ? _uiDominantColor : Audio.CurrentTrack?.CoverDominantColor;
            if (raw is null)
                return null;

            if (DominantColorCss.TryParseRgbComponents(raw, out var r, out var g, out var b))
                return $"--dominant-color: {r},{g},{b}; --player-accent: rgb({r}, {g}, {b});";

            var accent = DominantColorCss.ToVariableStyle("--player-accent", raw);
            return string.IsNullOrEmpty(accent) ? null : accent;
        }
    }

    private void CommitUiFromCurrentTrack(MusicTrackDto? details)
    {
        var track = Audio.CurrentTrack;
        _uiTitle = track?.Title;
        _uiArtist = track?.Artist;
        _uiAlbumTitle = track?.AlbumTitle;
        _uiCoverUrl = track?.CoverUrl;
        _uiDominantColor = details?.Pictures?
                .FirstOrDefault(p => p.Type == MetadataPictureType.Cover)?.DominantColor
            ?? details?.Pictures?
                .FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?.DominantColor
            ?? track?.CoverDominantColor;
        _uiMediaId = track?.MediaId;
        _uiUserRating = track?.UserRating;
        _uiReleaseDate = details?.ReleaseDate;
        _uiCommitted = true;
    }

    /// <summary>
    /// Queue covers use Small (~200px). Fullscreen needs Medium (same as album detail).
    /// </summary>
    private static string? ToFullscreenCoverUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        var queryIdx = url.IndexOf('?', StringComparison.Ordinal);
        if (queryIdx < 0)
        {
            return url.Contains("/metadata-pictures/", StringComparison.OrdinalIgnoreCase)
                ? $"{url}?size={MetadataPictureSize.Medium}"
                : url;
        }

        var path = url[..queryIdx];
        var query = url[(queryIdx + 1)..];
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
            .Append($"size={MetadataPictureSize.Medium}")
            .ToArray();

        return $"{path}?{string.Join('&', parts)}";
    }

    private string PlayPauseIcon => IsRemoteMode
        ? RemoteControl.PlaybackState == RemotePlaybackState.Playing ? Phosphor.Pause : Phosphor.Play
        : Audio.PlaybackState switch
        {
            PlaybackState.Playing => Phosphor.Pause,
            PlaybackState.Paused or PlaybackState.Idle or PlaybackState.Ended => Phosphor.Play,
            _ => Phosphor.CircleNotch
        };

    private bool IsBuffering => IsRemoteMode
        ? RemoteControl.PlaybackState == RemotePlaybackState.Buffering
        : Audio.PlaybackState is not (PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Idle or PlaybackState.Ended);

    private string RepeatIcon => Audio.Repeat switch
    {
        RepeatMode.One => Phosphor.RepeatOnce,
        _ => Phosphor.Repeat
    };

    private bool HasTrackProperties => _trackDetails is { } t &&
        (t.LoudnessLufs is not null || t.ReplayGainTrackGain is not null);

    private string VolumeIcon => (IsRemoteMode ? DisplayVolume <= 0 : Audio.IsMuted || Audio.Volume <= 0)
        ? Phosphor.SpeakerX
        : DisplayVolume < 0.5
            ? Phosphor.SpeakerLow
            : Phosphor.SpeakerHigh;

    protected override async Task OnInitializedAsync()
    {
        Audio.PlaybackStateChanged += OnStateChanged;
        Audio.CurrentTimeChanged += OnTimeChanged;
        Audio.DurationChanged += OnDurationChanged;
        Audio.BufferedTimeChanged += OnTimeChanged;
        Audio.CurrentTrackChanged += OnTrackChanged;
        Audio.QueueChanged += OnQueueChanged;
        Audio.ActiveRadioChanged += OnRadioChanged;
        Audio.ShuffleChanged += OnShuffleChanged;
        Audio.RepeatModeChanged += OnRepeatChanged;
        Audio.VolumeChanged += OnVolumeStateChanged;
        Audio.IsMutedChanged += OnMutedStateChanged;
        Audio.IsFullScreenVisibleChanged += OnFullScreenVisibilityChanged;
        SleepTimer.TimerChanged += OnSleepTimerChanged;
        RemoteControl.StateChanged += OnRemoteStateChanged;
        RemoteControl.SessionChanged += OnRemoteSessionChanged;
        SyncPlay.GroupUpdated += OnSyncPlayUpdated;
        SyncPlay.CommandReceived += OnSyncPlayCommandReceived;

        var deviceType = await DeviceService.GetDeviceTypeAsync();
        _isTv = deviceType == DeviceType.TV;
        _showVolumeControls = deviceType is not (DeviceType.TV or DeviceType.Phone);
        _visualizerAvailable = !_isTv;

        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            _musicIntelligenceAvailable = status.IsAvailable;
        }
        catch
        {
            _musicIntelligenceAvailable = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var currentState = _waveformMaskStyle is not null ? "waveform" : "bar";
        if (firstRender || currentState != _prevWaveformState)
        {
            _prevWaveformState = currentState;
            try
            {
                _dotNetRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("K7.SeekBar.init", _seekBarRef, _dotNetRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                // Keep _dotNetRef if we already had one from a prior successful init;
                // only clear when first-render init never succeeded.
                if (firstRender)
                {
                    _dotNetRef?.Dispose();
                    _dotNetRef = null;
                }
            }
        }
    }

    private async Task OnCastDeviceSelected(CastDeviceInfo device)
    {
        await CastOrchestration.CastCurrentAudioAsync(device);
    }

    private async Task OnRemoteDeviceSelected(ConnectedDeviceDto device)
    {
        var track = Audio.CurrentTrack;
        if (track is null) return;

        Audio.Pause();

        var senderDeviceId = DeviceStorage.Get(PreferenceKeys.DEVICE_ID);
        var request = new RemotePlaybackRequestDto
        {
            IndexedFileId = track.IndexedFileId,
            StartPosition = Audio.CurrentTime,
            IsAudio = true,
            MediaId = track.MediaId,
            Title = track.Title,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle,
            CoverUrl = track.CoverUrl,
            Duration = track.Duration,
            SenderDeviceId = senderDeviceId is not null ? Guid.Parse(senderDeviceId.AsSpan()) : null
        };

        await HubClient.RequestRemotePlaybackAsync(device.DeviceId, request);
        RemoteControl.StartSession(device.DeviceId, device.DeviceName, request);
    }

    private async Task OnResumeHere()
    {
        var position = RemoteControl.Position;
        await RemoteControl.SendStopAsync();

        Audio.Play();
        Audio.Seek(position);
    }

    private async Task OnRemoteStop()
    {
        await RemoteControl.SendStopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Audio.PlaybackStateChanged -= OnStateChanged;
        Audio.CurrentTimeChanged -= OnTimeChanged;
        Audio.DurationChanged -= OnDurationChanged;
        Audio.BufferedTimeChanged -= OnTimeChanged;
        Audio.CurrentTrackChanged -= OnTrackChanged;
        Audio.QueueChanged -= OnQueueChanged;
        Audio.ActiveRadioChanged -= OnRadioChanged;
        Audio.ShuffleChanged -= OnShuffleChanged;
        Audio.RepeatModeChanged -= OnRepeatChanged;
        Audio.VolumeChanged -= OnVolumeStateChanged;
        Audio.IsMutedChanged -= OnMutedStateChanged;
        Audio.IsFullScreenVisibleChanged -= OnFullScreenVisibilityChanged;
        SleepTimer.TimerChanged -= OnSleepTimerChanged;
        RemoteControl.StateChanged -= OnRemoteStateChanged;
        RemoteControl.SessionChanged -= OnRemoteSessionChanged;
        SyncPlay.GroupUpdated -= OnSyncPlayUpdated;
        SyncPlay.CommandReceived -= OnSyncPlayCommandReceived;

        if (_dotNetRef is not null)
        {
            try { await JS.InvokeVoidAsync("K7.SeekBar.dispose", _seekBarRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException) { }
        }

        if (_visualizerEnabled)
        {
            try { await JS.InvokeVoidAsync("K7.Visualizer.stop"); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException) { }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    private void Close() => Audio.ToggleFullScreen();

    private async Task StopAndHide()
    {
        Audio.ToggleFullScreen();
        Audio.Stop();
        await Audio.HideAsync();
    }

    private void ToggleQueue() => _view = _view == FullScreenView.Queue ? FullScreenView.Player : FullScreenView.Queue;

    private void ToggleSyncPlayView() => _view = _view == FullScreenView.SyncPlay ? FullScreenView.Player : FullScreenView.SyncPlay;

    private async Task ToggleLyrics()
    {
        if (_view == FullScreenView.Lyrics)
        {
            _view = FullScreenView.Player;
            return;
        }

        _view = FullScreenView.Lyrics;
        await LoadTrackDetailsAsync();
    }

    private async Task LoadTrackDetailsAsync()
    {
        var mediaId = Audio.CurrentTrack?.MediaId;
        if (mediaId is null || mediaId == _detailsLoadedForMediaId) return;

        // Fetch first, then swap fields in one go so concurrent renders (seek ticks, etc.)
        // do not flash empty lyrics / info / year mid-transition.
        var media = await Server.GetMediaAsync(mediaId.Value);
        if (Audio.CurrentTrack?.MediaId != mediaId)
            return;

        _detailsLoadedForMediaId = mediaId;

        if (media is MusicTrackDto track)
        {
            _trackDetails = track;
            _lyricsLrc = track.LyricsLrc;
            _lyrics = track.Lyrics;
            _waveformPeaks = track.WaveformPeaks;
            BuildWaveformMask();

            var indexedFile = track.IndexedFiles?.FirstOrDefault();
            if (indexedFile is not null)
            {
                _fileSize = indexedFile.Size;
                _audioMetadata = indexedFile.FileMetadata as AudioFileMetadataDto;
            }
            else
            {
                _fileSize = 0;
                _audioMetadata = null;
            }
        }
        else
        {
            _trackDetails = null;
            _lyricsLrc = null;
            _lyrics = null;
            _waveformPeaks = null;
            _waveformMaskStyle = null;
            _audioMetadata = null;
            _fileSize = 0;
        }
    }

    private void BuildWaveformMask()
    {
        if (_waveformPeaks is not { Length: > 0 })
        {
            _waveformMaskStyle = null;
            return;
        }

        var peaks = SmoothPeaks(_waveformPeaks);
        var count = peaks.Length;
        const float w = 1000f;
        const float h = 100f;
        const float mid = h / 2;
        var step = w / Math.Max(count - 1, 1);

        var sb = new StringBuilder(count * 80);
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 1000 100' preserveAspectRatio='none'>");
        sb.Append("<path d='");

        // Top half: left to right with smooth cubic Béziers
        for (var i = 0; i < count; i++)
        {
            var x = i * step;
            var amplitude = Math.Max(peaks[i], 0.005f) * mid;
            var y = mid - amplitude;

            if (i == 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "M{0:F1},{1:F1}", x, y);
            }
            else
            {
                var prevX = (i - 1) * step;
                var cpX = (prevX + x) / 2;
                var prevY = mid - Math.Max(peaks[i - 1], 0.005f) * mid;
                sb.AppendFormat(CultureInfo.InvariantCulture, " C{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
                    cpX, prevY, cpX, y, x, y);
            }
        }

        // Bottom half: right to left (mirror) with smooth cubic Béziers
        for (var i = count - 1; i >= 0; i--)
        {
            var x = i * step;
            var amplitude = Math.Max(peaks[i], 0.005f) * mid;
            var y = mid + amplitude;

            if (i == count - 1)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " L{0:F1},{1:F1}", x, y);
            }
            else
            {
                var nextX = (i + 1) * step;
                var cpX = (nextX + x) / 2;
                var nextY = mid + Math.Max(peaks[i + 1], 0.005f) * mid;
                sb.AppendFormat(CultureInfo.InvariantCulture, " C{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
                    cpX, nextY, cpX, y, x, y);
            }
        }

        sb.Append(" Z' fill='white'/>");
        sb.Append("</svg>");

        var encoded = Uri.EscapeDataString(sb.ToString());
        _waveformMaskStyle = $"-webkit-mask-image: url(\"data:image/svg+xml,{encoded}\"); mask-image: url(\"data:image/svg+xml,{encoded}\"); -webkit-mask-size: 100% 100%; mask-size: 100% 100%; --waveform-scale: {_waveformScale.ToString(CultureInfo.InvariantCulture)}; --waveform-opacity: {_waveformOpacity.ToString(CultureInfo.InvariantCulture)};";
    }

    private async Task MorphWaveformAsync(Func<Task> rebuildAsync)
    {
        // Flatten current waveform first; keep committed track chrome until rebuild finishes
        // so title / album / year / cover swap in one paint with the new waveform.
        _waveformScale = 0.04;
        _waveformOpacity = 0.35;
        if (_waveformMaskStyle is not null)
            BuildWaveformMask();
        await InvokeAsync(StateHasChanged);
        await Task.Delay(420);

        await rebuildAsync();

        _waveformScale = 0.04;
        _waveformOpacity = 0.35;
        if (_waveformMaskStyle is not null)
            BuildWaveformMask();
        await InvokeAsync(StateHasChanged);
        // Let the browser paint the new flat mask before growing.
        await Task.Delay(48);

        _waveformScale = 1;
        _waveformOpacity = 1;
        if (_waveformMaskStyle is not null)
            BuildWaveformMask();

        if (_visualizerEnabled && _waveformPeaks is not null)
        {
            try { await JS.InvokeVoidAsync("K7.Visualizer.setPeaks", _waveformPeaks); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        await InvokeAsync(StateHasChanged);
    }

    private static float[] SmoothPeaks(float[] raw)
    {
        var smoothed = new float[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var prev = i > 0 ? raw[i - 1] : raw[i];
            var next = i < raw.Length - 1 ? raw[i + 1] : raw[i];
            smoothed[i] = (prev + raw[i] * 4 + next) / 6f;
        }
        return smoothed;
    }

    private void OnLyricsSeek(double seconds) => Audio.Seek(seconds);

    private void TogglePlayPause()
    {
        if (_longPressTriggered || _keyHeldDown)
            return;

        if (IsRemoteMode)
        {
            _ = RemoteControl.PlaybackState == RemotePlaybackState.Playing
                ? RemoteControl.SendPauseAsync()
                : RemoteControl.SendPlayAsync();
            return;
        }

        if (Audio.PlaybackState == PlaybackState.Playing)
            Audio.Pause();
        else
            Audio.Play();
    }

    private CancellationTokenSource? _longPressCts;
    private bool _longPressTriggered;
    private bool _keyHeldDown;

    private void OnFabPointerDown(PointerEventArgs e)
    {
        _longPressTriggered = false;
        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();
        var cts = _longPressCts;
        _ = Task.Delay(600, cts.Token).ContinueWith(async _ =>
        {
            _longPressTriggered = true;
            await InvokeAsync(async () => await StopAndHide());
        }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
    }

    private void OnFabPointerUp(PointerEventArgs e)
    {
        _longPressCts?.Cancel();
        _longPressCts = null;
    }

    private void OnFabKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is not ("Enter" or " ")) return;
        if (e.Repeat) return;

        _keyHeldDown = true;
        _longPressTriggered = false;
        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();
        var cts = _longPressCts;
        _ = Task.Delay(600, cts.Token).ContinueWith(async _ =>
        {
            _longPressTriggered = true;
            await InvokeAsync(async () => await StopAndHide());
        }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
    }

    private void OnFabKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is not ("Enter" or " ")) return;

        _longPressCts?.Cancel();
        _longPressCts = null;

        var wasShortPress = _keyHeldDown && !_longPressTriggered;
        _keyHeldDown = false;

        if (wasShortPress && e.Key is "Enter")
        {
            if (Audio.PlaybackState == PlaybackState.Playing)
                Audio.Pause();
            else
                Audio.Play();
        }
    }

    private async Task OnNext()
    {
        await Audio.NextAsync();
    }

    private async Task OnPrevious()
    {
        await Audio.PreviousAsync();
    }

    private void ToggleMute()
    {
        if (IsRemoteMode)
        {
            _ = RemoteControl.SendVolumeAsync(DisplayVolume > 0 ? 0 : 1);
            return;
        }

        if (Audio.IsMuted)
            Audio.Unmute();
        else
            Audio.Mute();
    }

    private void OnVolumeChanged(double value)
    {
        if (IsRemoteMode)
            _ = RemoteControl.SendVolumeAsync(value);
        else
            Audio.SetVolume(value);
    }

    private void OnRatingChanged(int? value)
    {
        if (Audio.CurrentTrack is { } track)
            track.UserRating = value;
        _uiUserRating = value;
    }

    private async Task PlayFromQueue(int index)
    {
        if (index == Audio.CurrentIndex) return;
        await Audio.SkipToIndexAsync(index);
    }

    private async Task PlayFromHistory(int historyIndex)
    {
        if (historyIndex < 0 || historyIndex >= Audio.PlayHistory.Count) return;
        await Audio.PlayTrackAsync(Audio.PlayHistory[historyIndex]);
    }

    private async Task OnHistoryItemKeyDown(KeyboardEventArgs e, int historyIndex)
    {
        if (e.Key is "Enter" or " ")
            await PlayFromHistory(historyIndex);
    }

    private async Task OnSeekPointerDown(PointerEventArgs e)
    {
        _isDragging = true;
        _isScrubbing = true;
        _seekBarBounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", _seekBarRef);
        UpdateScrubFromPointer(e);
    }

    private void OnSeekPointerMove(PointerEventArgs e)
    {
        if (!_isDragging) return;
        UpdateScrubFromPointer(e);
    }

    private void OnSeekPointerUp(PointerEventArgs e)
    {
        if (!_isDragging) return;
        if (_isScrubbing)
        {
            if (IsRemoteMode)
                _ = RemoteControl.SendSeekAsync(_scrubTime);
            else
                Audio.Seek(_scrubTime);
        }
        _isDragging = false;
        _isScrubbing = false;
        _seekBarBounds = null;
    }

    private void UpdateScrubFromPointer(MouseEventArgs e)
    {
        if (_seekBarBounds is not { Width: > 0 } bounds || DisplayDuration <= 0) return;
        var percent = Math.Clamp((e.ClientX - bounds.Left) / bounds.Width, 0, 1);
        _scrubTime = percent * DisplayDuration;
    }

    private void OnSeekKeyDown(KeyboardEventArgs e)
    {
        if (DisplayDuration <= 0) return;

        if (_isScrubbing)
        {
            switch (e.Code)
            {
                case "ArrowRight":
                    _scrubTime = Math.Min(_scrubTime + Audio.SkipForwardSeconds, DisplayDuration);
                    break;
                case "ArrowLeft":
                    _scrubTime = Math.Max(_scrubTime - Audio.SkipBackSeconds, 0);
                    break;
            }
            return;
        }

        var skipFwd = Audio.SkipForwardSeconds;
        var skipBack = Audio.SkipBackSeconds;

        switch (e.Code)
        {
            case "ArrowRight":
                if (IsRemoteMode)
                    _ = RemoteControl.SendSeekAsync(Math.Min(DisplayPosition + skipFwd, DisplayDuration));
                else
                    Audio.Seek(Math.Min(Audio.CurrentTime + skipFwd, Audio.Duration));
                break;
            case "ArrowLeft":
                if (IsRemoteMode)
                    _ = RemoteControl.SendSeekAsync(Math.Max(DisplayPosition - skipBack, 0));
                else
                    Audio.Seek(Math.Max(Audio.CurrentTime - skipBack, 0));
                break;
        }
    }

    [JSInvokable("OnEditStart")]
    public void OnEditStart()
    {
        _isScrubbing = true;
        _scrubTime = DisplayPosition;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable("OnEditCommit")]
    public void OnEditCommit()
    {
        if (_isScrubbing)
        {
            if (IsRemoteMode)
                _ = RemoteControl.SendSeekAsync(_scrubTime);
            else
                Audio.Seek(_scrubTime);
        }
        _isScrubbing = false;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable("OnEditCancel")]
    public void OnEditCancel()
    {
        _isScrubbing = false;
        InvokeAsync(StateHasChanged);
    }

    private async Task OnQueueItemKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Code is "Enter" or "Space")
        {
            await PlayFromQueue(index);
        }
    }

    private void OnStateChanged(PlaybackState _) => InvokeAsync(StateHasChanged);
    private void OnDurationChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTimeChanged(double _) => InvokeAsync(async () =>
    {
        if (_visualizerEnabled)
        {
            try { await JS.InvokeVoidAsync("K7.Visualizer.setProgress", DisplayPercent / 100.0); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        StateHasChanged();
    });
    private void OnTrackChanged(AudioQueueItem? _) => InvokeAsync(async () =>
    {
        _detailsLoadedForMediaId = null;
        _similarLoadedForTrackId = null;
        _suggestionsLoadedForTrackId = null;
        if (Audio.IsFullScreenVisible)
        {
            await MorphWaveformAsync(async () =>
            {
                await LoadTrackDetailsAsync();
                CommitUiFromCurrentTrack(_trackDetails);
            });
        }
        else
        {
            CommitUiFromCurrentTrack(null);
            StateHasChanged();
        }
    });
    private void OnQueueChanged() => InvokeAsync(StateHasChanged);
    private void OnRadioChanged() => InvokeAsync(StateHasChanged);
    private void OnShuffleChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnRepeatChanged(RepeatMode _) => InvokeAsync(StateHasChanged);
    private void OnVolumeStateChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnMutedStateChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnSleepTimerChanged() => InvokeAsync(StateHasChanged);
    private void OnRemoteStateChanged() => InvokeAsync(StateHasChanged);
    private void OnRemoteSessionChanged() => InvokeAsync(StateHasChanged);
    private void OnSyncPlayUpdated() => InvokeAsync(() =>
    {
        if (!SyncPlay.IsInGroup && _view == FullScreenView.SyncPlay)
        {
            _view = FullScreenView.Player;
        }

        StateHasChanged();
    });
    private void OnSyncPlayCommandReceived(SyncPlayCommandDto _) => InvokeAsync(StateHasChanged);
    private void OnFullScreenVisibilityChanged() => InvokeAsync(async () =>
    {
        if (Audio.IsFullScreenVisible)
        {
            await LoadTrackDetailsAsync();
            CommitUiFromCurrentTrack(_trackDetails);
        }

        StateHasChanged();
    });

    private void StartSleepTimer(int minutes)
    {
        SleepTimer.Start(SleepTimerMode.Duration, TimeSpan.FromMinutes(minutes));
        _sleepTimerSubmenuOpen = false;
        _menuOpen = false;
    }

    private void StartSleepTimerEndOfTrack()
    {
        SleepTimer.Start(SleepTimerMode.EndOfTrack);
        _sleepTimerSubmenuOpen = false;
        _menuOpen = false;
    }

    private void CancelSleepTimer()
    {
        SleepTimer.Cancel();
        _menuOpen = false;
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        return $"{remaining.Minutes}:{remaining.Seconds:00}";
    }

    private async Task ShareTrack()
    {
        _menuOpen = false;
        var track = Audio.CurrentTrack;
        if (track is null) return;

        var url = $"{Navigation.BaseUri}music/albums/{_trackDetails?.AlbumId ?? track.MediaId}?track={track.MediaId}";
        var shared = await JS.InvokeAsync<bool>("K7.shareOrCopy", url);
        Snackbar.Add(S[shared ? "Shared" : "CopiedToClipboard"], K7Severity.Success);
    }

    private void GoToArtist()
    {
        _menuOpen = false;
        var artistId = Audio.CurrentTrack?.ArtistId ?? _trackDetails?.ArtistId;
        if (artistId is null) return;
        Audio.ToggleFullScreen();
        Navigation.NavigateTo($"/music/artists/{artistId}");
    }

    private void GoToAlbum()
    {
        _menuOpen = false;
        var albumId = _trackDetails?.AlbumId;
        if (albumId is null) return;
        Audio.ToggleFullScreen();
        Navigation.NavigateTo($"/music/albums/{albumId}");
    }

    private async Task SaveQueueAsPlaylist()
    {
        _menuOpen = false;
        if (Audio.Queue.Count == 0) return;

        var reference = await DialogService.ShowAsync<CreatePlaylistDialog>(S["SaveQueueAsPlaylist"]);
        var result = await reference.Result;
        if (result.Canceled || result.Data is not Guid playlistId) return;

        foreach (var item in Audio.Queue)
        {
            await PlaylistService.AddPlaylistItemAsync(playlistId, item.MediaId);
        }

        Snackbar.Add(S["QueueSavedAsPlaylist"], K7Severity.Success);
    }

    private async Task AddToPlaylist()
    {
        _menuOpen = false;
        var track = Audio.CurrentTrack;
        if (track is null) return;

        var parameters = new K7DialogParameters { ["MediaId"] = track.MediaId };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AddToPlaylistDialog>(S["AddToPlaylist"], parameters, options);
    }

    private async Task ToggleVisualizer()
    {
        _menuOpen = false;

        if (!_visualizerAvailable)
        {
            Snackbar.Add(S["VisualizerUnavailable"], K7Severity.Normal);
            return;
        }

        _visualizerEnabled = !_visualizerEnabled;
        StateHasChanged();

        try
        {
            if (_visualizerEnabled)
            {
                await Task.Yield();
                await JS.InvokeVoidAsync("K7.Visualizer.start", _visualizerCanvas, _waveformPeaks);
            }
            else
            {
                await JS.InvokeVoidAsync("K7.Visualizer.stop");
            }
        }
        catch (JSException)
        {
            _visualizerEnabled = false;
            _visualizerAvailable = false;
            Snackbar.Add(S["VisualizerUnavailable"], K7Severity.Normal);
            StateHasChanged();
        }
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            return "0:00";

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private async Task ShowTrackInfo()
    {
        _menuOpen = false;
        _view = FullScreenView.Info;
        await LoadTrackDetailsAsync();
    }

    private static string FormatChannels(AudioFileTrackDto? audioTrack)
    {
        if (audioTrack is null) return "-";
        if (!string.IsNullOrEmpty(audioTrack.ChannelLayout))
            return $"{audioTrack.Channels} ({audioTrack.ChannelLayout})";
        return audioTrack.Channels.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.Hours > 0
            ? $"{duration.Hours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1024 => $"{bytes / 1024.0:F0} KB",
            _ => $"{bytes} B"
        };
    }

    private async Task OnQueueTabChanged(QueueTab tab)
    {
        _queueTab = tab;
        if (tab == QueueTab.Similar)
            await LoadSimilarTracksAsync();
        else if (tab == QueueTab.Suggestions)
            await LoadSuggestionTracksAsync();
        StateHasChanged();
    }

    private async Task LoadSimilarTracksAsync()
    {
        if (Audio.CurrentTrack is null) return;

        var currentId = Audio.CurrentTrack.MediaId;
        if (currentId == _similarLoadedForTrackId) return;

        _similarLoading = true;
        _similarTracks = [];
        StateHasChanged();

        try
        {
            var matches = await MusicIntelligence.GetSimilarTracksAsync(currentId);
            if (matches.Count == 0)
            {
                // Keep the spinner visible while AudioMuse finishes warming / indexing.
                await Task.Delay(1500);
                matches = await MusicIntelligence.GetSimilarTracksAsync(currentId);
            }

            _similarTracks = await HydrateMiTracksAsync(matches);

            if (_similarTracks.Count > 0)
                _similarLoadedForTrackId = currentId;
        }
        catch
        {
            _similarTracks = [];
        }

        _similarLoading = false;
    }

    private async Task LoadSuggestionTracksAsync()
    {
        if (Audio.CurrentTrack is null) return;

        var currentId = Audio.CurrentTrack.MediaId;
        if (currentId == _suggestionsLoadedForTrackId) return;

        _suggestionsLoading = true;
        _suggestionTracks = [];
        StateHasChanged();

        try
        {
            var recentIds = Audio.Queue
                .Take(Audio.CurrentIndex + 1)
                .Select(t => t.MediaId)
                .Distinct()
                .ToList();

            var matches = await MusicIntelligence.GetSuggestionsAsync(recentIds);
            if (matches.Count == 0)
            {
                await Task.Delay(1500);
                matches = await MusicIntelligence.GetSuggestionsAsync(recentIds);
            }

            _suggestionTracks = await HydrateMiTracksAsync(matches);

            if (_suggestionTracks.Count > 0)
                _suggestionsLoadedForTrackId = currentId;
        }
        catch
        {
            _suggestionTracks = [];
        }

        _suggestionsLoading = false;
    }

    private async Task<List<AudioQueueItem>> HydrateMiTracksAsync(IReadOnlyList<MusicIntelligenceTrackMatchDto> matches)
    {
        if (matches.Count == 0)
            return [];

        var trackIds = matches.Select(m => m.ItemId).ToList();
        var tracks = await IntelligentSearchHelper.LoadScopedTracksAsync(
            Server,
            trackIds,
            libraryIds: null,
            libraryGroupIds: null);

        var matchMap = matches
            .GroupBy(m => m.ItemId)
            .ToDictionary(g => g.Key, g => g.First());

        return MusicTrackQueueMapper.ToQueueItems(tracks, ApiClient, matchMap, S["Untitled"]);
    }

    private async Task PlaySuggestionFromIndex(int index)
    {
        if (index < 0 || index >= _suggestionTracks.Count) return;
        await Audio.PlayTracksAsync(_suggestionTracks, index);
    }

    private async Task OnSuggestionItemKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Code is "Enter" or "Space")
            await PlaySuggestionFromIndex(index);
    }

    private async Task PlaySimilarFromIndex(int index)
    {
        if (index < 0 || index >= _similarTracks.Count) return;
        await Audio.PlayTracksAsync(_similarTracks, index);
    }

    private async Task OnSimilarItemKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Code is "Enter" or "Space")
            await PlaySimilarFromIndex(index);
    }
}
