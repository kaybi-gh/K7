using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using K7.Clients.Shared.UI;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class MiniMusicPlayer : IAsyncDisposable
{
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    private ElementReference _progressBarRef;
    private bool _isDragging;
    private bool _showVolumeControls = true;
    private bool _seekbarFocusable = true;
    private bool _isScrubbing;
    private double _scrubTime;
    private BoundingRect? _progressBarBounds;
    private DotNetObjectReference<MiniMusicPlayer>? _dotNetRef;

    private double DisplayPercent => _isScrubbing && Audio.Duration > 0
        ? (_scrubTime / Audio.Duration) * 100
        : CurrentPercent;

    private double CurrentPercent => Audio.Duration > 0 ? (Audio.CurrentTime / Audio.Duration) * 100 : 0;
    private double BufferedPercent => Audio.Duration > 0 ? (Audio.BufferedTime / Audio.Duration) * 100 : 0;

    private string PlayPauseIcon => Audio.PlaybackState == PlaybackState.Playing
        ? Phosphor.Pause
        : Phosphor.Play;

    private string RepeatIcon => Audio.Repeat switch
    {
        RepeatMode.One => Phosphor.RepeatOnce,
        _ => Phosphor.Repeat
    };

    private string VolumeIcon => Audio.IsMuted || Audio.Volume <= 0
        ? Phosphor.SpeakerX
        : Audio.Volume < 0.5
            ? Phosphor.SpeakerLow
            : Phosphor.SpeakerHigh;

    protected override async Task OnInitializedAsync()
    {
        Audio.PlaybackStateChanged += OnStateChanged;
        Audio.CurrentTimeChanged += OnTimeChanged;
        Audio.DurationChanged += OnStateChanged;
        Audio.BufferedTimeChanged += OnTimeChanged;
        Audio.CurrentTrackChanged += OnTrackChanged;
        Audio.QueueChanged += OnQueueChanged;
        Audio.ShuffleChanged += OnShuffleChanged;
        Audio.RepeatModeChanged += OnRepeatChanged;
        Audio.VolumeChanged += OnVolumeStateChanged;
        Audio.IsMutedChanged += OnMutedStateChanged;
        Audio.IsVisibleChanged += OnVisibilityChanged;

        var deviceType = await DeviceService.GetDeviceTypeAsync();
        _showVolumeControls = deviceType is not (DeviceType.TV or DeviceType.Phone);
        _seekbarFocusable = deviceType is DeviceType.Desktop;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("K7.SeekBar.init", _progressBarRef, _dotNetRef);
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Audio.PlaybackStateChanged -= OnStateChanged;
        Audio.CurrentTimeChanged -= OnTimeChanged;
        Audio.DurationChanged -= OnStateChanged;
        Audio.BufferedTimeChanged -= OnTimeChanged;
        Audio.CurrentTrackChanged -= OnTrackChanged;
        Audio.QueueChanged -= OnQueueChanged;
        Audio.ShuffleChanged -= OnShuffleChanged;
        Audio.RepeatModeChanged -= OnRepeatChanged;
        Audio.VolumeChanged -= OnVolumeStateChanged;
        Audio.IsMutedChanged -= OnMutedStateChanged;
        Audio.IsVisibleChanged -= OnVisibilityChanged;

        try { await JS.InvokeVoidAsync("K7.SeekBar.dispose", _progressBarRef); }
        catch (JSDisconnectedException) { }
        _dotNetRef?.Dispose();
    }

    private void TogglePlayPause()
    {
        if (Audio.PlaybackState == PlaybackState.Playing)
            Audio.Pause();
        else
            Audio.Play();
    }

    private async Task OnNext() => await Audio.NextAsync();
    private async Task OnPrevious() => await Audio.PreviousAsync();

    private async Task StopAndHide()
    {
        Audio.Stop();
        await Audio.HideAsync();
    }

    private void ToggleMute()
    {
        if (Audio.IsMuted)
            Audio.Unmute();
        else
            Audio.Mute();
    }

    private void OnVolumeChanged(double value) => Audio.SetVolume(value);

    private void OnRatingChanged(int? value)
    {
        if (Audio.CurrentTrack is not null)
            Audio.CurrentTrack.UserRating = value;
    }

    private async Task OnProgressPointerDown(PointerEventArgs e)
    {
        _isDragging = true;
        _isScrubbing = true;
        _progressBarBounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", _progressBarRef);
        UpdateScrubFromPointer(e);
    }

    private void OnProgressPointerMove(PointerEventArgs e)
    {
        if (!_isDragging) return;
        UpdateScrubFromPointer(e);
    }

    private void OnProgressPointerUp(PointerEventArgs e)
    {
        if (!_isDragging) return;
        if (_isScrubbing)
            Audio.Seek(_scrubTime);
        _isDragging = false;
        _isScrubbing = false;
        _progressBarBounds = null;
    }

    private void UpdateScrubFromPointer(MouseEventArgs e)
    {
        if (_progressBarBounds is not { Width: > 0 } bounds || Audio.Duration <= 0) return;
        var percent = Math.Clamp((e.ClientX - bounds.Left) / bounds.Width, 0, 1);
        _scrubTime = percent * Audio.Duration;
    }

    private void OnStateChanged(PlaybackState _) => InvokeAsync(StateHasChanged);
    private void OnStateChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTimeChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTrackChanged(AudioQueueItem? _) => InvokeAsync(StateHasChanged);
    private void OnQueueChanged() => InvokeAsync(StateHasChanged);
    private void OnShuffleChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnRepeatChanged(RepeatMode _) => InvokeAsync(StateHasChanged);
    private void OnVolumeStateChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnMutedStateChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnVisibilityChanged() => InvokeAsync(StateHasChanged);

    private void OnProgressKeyDown(KeyboardEventArgs e)
    {
        if (Audio.Duration <= 0) return;

        if (_isScrubbing)
        {
            switch (e.Code)
            {
                case "ArrowRight":
                    _scrubTime = Math.Min(_scrubTime + 5, Audio.Duration);
                    break;
                case "ArrowLeft":
                    _scrubTime = Math.Max(_scrubTime - 5, 0);
                    break;
            }
            return;
        }

        switch (e.Code)
        {
            case "ArrowRight":
                Audio.Seek(Math.Min(Audio.CurrentTime + 5, Audio.Duration));
                break;
            case "ArrowLeft":
                Audio.Seek(Math.Max(Audio.CurrentTime - 5, 0));
                break;
        }
    }

    [JSInvokable("OnEditStart")]
    public void OnEditStart()
    {
        _isScrubbing = true;
        _scrubTime = Audio.CurrentTime;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable("OnEditCommit")]
    public void OnEditCommit()
    {
        if (_isScrubbing)
            Audio.Seek(_scrubTime);
        _isScrubbing = false;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable("OnEditCancel")]
    public void OnEditCancel()
    {
        _isScrubbing = false;
        InvokeAsync(StateHasChanged);
    }

    private void OnTrackInfoKeyDown(KeyboardEventArgs e)
    {
        if (e.Code is "Enter" or "Space")
        {
            Audio.ToggleFullScreen();
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
}
