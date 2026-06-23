using System.Globalization;
using System.Text;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.Shared.UI.Components.Players;

public enum FullScreenView { Player, Lyrics, Queue, Info, SyncPlay }
public enum QueueTab { UpNext, Previous, Similar }

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
    private bool _isScrubbing;
    private double _scrubTime;
    private BoundingRect? _seekBarBounds;
    private FullScreenView _view;
    private string? _lyricsLrc;
    private string? _lyrics;
    private float[]? _waveformPeaks;
    private string? _waveformMaskStyle;
    private string? _prevWaveformState;
    private Guid? _detailsLoadedForMediaId;
    private DotNetObjectReference<FullScreenMusicPlayer>? _dotNetRef;
    private bool _menuOpen;
    private bool _sleepTimerSubmenuOpen;
    private bool _visualizerEnabled;
    private bool _audioMuseEnabled;
    private List<AudioQueueItem> _similarTracks = [];
    private bool _similarLoading;
    private Guid? _similarLoadedForTrackId;
    private ElementReference _visualizerCanvas;
    private MusicTrackDto? _trackDetails;
    private AudioFileMetadataDto? _audioMetadata;
    private long _fileSize;
    private QueueTab _queueTab;
    private IReadOnlyList<TabOption<QueueTab>> _queueTabOptions => _audioMuseEnabled
        ? [new(QueueTab.UpNext, S["UpNext"]), new(QueueTab.Previous, S["Previous"]), new(QueueTab.Similar, S["Similar"])]
        : [new(QueueTab.UpNext, S["UpNext"]), new(QueueTab.Previous, S["Previous"])];

    private double DisplayPercent => _isScrubbing && DisplayDuration > 0
        ? (_scrubTime / DisplayDuration) * 100
        : CurrentPercent;

    private double CurrentPercent => DisplayDuration > 0 ? (DisplayPosition / DisplayDuration) * 100 : 0;
    private double BufferedPercent => !IsRemoteMode && Audio.Duration > 0 ? (Audio.BufferedTime / Audio.Duration) * 100 : 0;

    private bool IsRemoteMode => RemoteControl.IsControlling && RemoteControl.IsAudio;
    private bool IsSyncPlayMode => SyncPlay.IsInGroup;

    private string? DisplayTitle => IsRemoteMode ? RemoteControl.Title : Audio.CurrentTrack?.Title;
    private string? DisplayArtist => IsRemoteMode ? RemoteControl.Artist : Audio.CurrentTrack?.Artist;
    private string? DisplayAlbumTitle => IsRemoteMode ? RemoteControl.AlbumTitle : Audio.CurrentTrack?.AlbumTitle;
    private string? DisplayCoverUrl => IsRemoteMode ? RemoteControl.CoverUrl : Audio.CurrentTrack?.CoverUrl;
    private double DisplayPosition => IsRemoteMode ? RemoteControl.Position : Audio.CurrentTime;
    private double DisplayDuration => IsRemoteMode ? RemoteControl.Duration : Audio.Duration;
    private double DisplayVolume => IsRemoteMode ? RemoteControl.Volume : Audio.Volume;

    private string? DominantColorStyle => Audio.CurrentTrack?.CoverDominantColor is { } color && !IsRemoteMode
        ? $"--dominant-color: {color};"
        : null;

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
        _showVolumeControls = deviceType is not (DeviceType.TV or DeviceType.Phone);

        try
        {
            var flags = await ServerPreferences.GetServerFeatureFlagsAsync();
            _audioMuseEnabled = flags.AudioMuseAiEnabled;
        }
        catch
        {
            _audioMuseEnabled = false;
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
            catch (JSException) { }
            catch (InvalidOperationException) { }
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

        try { await JS.InvokeVoidAsync("K7.SeekBar.dispose", _seekBarRef); }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }

        if (_visualizerEnabled)
        {
            try { await JS.InvokeVoidAsync("K7.Visualizer.stop"); }
            catch (JSDisconnectedException) { }
            catch (InvalidOperationException) { }
        }

        _dotNetRef?.Dispose();
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

        _detailsLoadedForMediaId = mediaId;
        _lyricsLrc = null;
        _lyrics = null;
        _trackDetails = null;
        _audioMetadata = null;
        _fileSize = 0;

        var media = await Server.GetMediaAsync(mediaId.Value);
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
        }
        else
        {
            _waveformPeaks = null;
            _waveformMaskStyle = null;
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
        _waveformMaskStyle = $"-webkit-mask-image: url(\"data:image/svg+xml,{encoded}\"); mask-image: url(\"data:image/svg+xml,{encoded}\"); -webkit-mask-size: 100% 100%; mask-size: 100% 100%;";
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
    }

    private async Task PlayFromQueue(int index)
    {
        if (index == Audio.CurrentIndex) return;
        await Audio.SkipToIndexAsync(index);
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
    private void OnTimeChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTrackChanged(AudioQueueItem? _) => InvokeAsync(async () =>
    {
        _detailsLoadedForMediaId = null;
        _similarLoadedForTrackId = null;
        if (Audio.IsFullScreenVisible)
            await LoadTrackDetailsAsync();
        StateHasChanged();
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
            await LoadTrackDetailsAsync();
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
        if (remaining.TotalHours >= 1)
            return $"{remaining.Hours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
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
        _visualizerEnabled = !_visualizerEnabled;
        StateHasChanged();

        if (_visualizerEnabled)
        {
            await Task.Yield();
            await JS.InvokeVoidAsync("K7.Visualizer.start", _visualizerCanvas);
        }
        else
        {
            await JS.InvokeVoidAsync("K7.Visualizer.stop");
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
            var trackIds = await AudioMuseAi.GetSimilarTracksAsync(currentId);
            if (trackIds.Count > 0)
            {
                var result = await Server.GetLiteMediasAsync(new GetMediasWithPaginationQuery
                {
                    MediaTypes = [MediaType.MusicTrack],
                    Ids = trackIds.ToArray(),
                    PageNumber = 1,
                    PageSize = trackIds.Count
                });

                _similarTracks = result?.Items?.OfType<LiteMusicTrackDto>()
                    .Where(t => t.IndexedFileId.HasValue)
                    .Select(t => new AudioQueueItem
                    {
                        IndexedFileId = t.IndexedFileId!.Value,
                        MediaId = t.Id,
                        Title = t.Title ?? S["Untitled"],
                        Artist = t.ArtistName,
                        AlbumTitle = t.AlbumTitle,
                        ArtistId = t.ArtistId,
                        Genre = t.Genre,
                        Duration = t.Duration
                    })
                    .ToList() ?? [];
            }

            _similarLoadedForTrackId = currentId;
        }
        catch
        {
            _similarTracks = [];
        }

        _similarLoading = false;
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