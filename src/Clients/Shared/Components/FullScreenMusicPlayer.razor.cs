using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;

namespace K7.Clients.Shared.Components;

public partial class FullScreenMusicPlayer : IDisposable
{
    private ElementReference _seekBarRef;
    private bool _isDragging;
    private bool _showQueue;

    private double CurrentPercent => Audio.Duration > 0 ? (Audio.CurrentTime / Audio.Duration) * 100 : 0;
    private double BufferedPercent => Audio.Duration > 0 ? (Audio.BufferedTime / Audio.Duration) * 100 : 0;

    private string PlayPauseIcon => Audio.PlaybackState == PlaybackState.Playing
        ? Icons.Material.Filled.Pause
        : Icons.Material.Filled.PlayArrow;

    private string ShuffleIcon => Icons.Material.Filled.Shuffle;

    private string RepeatIcon => Audio.Repeat switch
    {
        RepeatMode.One => Icons.Material.Filled.RepeatOne,
        _ => Icons.Material.Filled.Repeat
    };

    private string VolumeIcon => Audio.IsMuted || Audio.Volume <= 0
        ? Icons.Material.Filled.VolumeOff
        : Audio.Volume < 0.5
            ? Icons.Material.Filled.VolumeDown
            : Icons.Material.Filled.VolumeUp;

    protected override void OnInitialized()
    {
        Audio.PlaybackStateChanged += OnStateChanged;
        Audio.CurrentTimeChanged += OnTimeChanged;
        Audio.DurationChanged += OnDurationChanged;
        Audio.BufferedTimeChanged += OnTimeChanged;
        Audio.CurrentTrackChanged += OnTrackChanged;
        Audio.QueueChanged += OnQueueChanged;
        Audio.ShuffleChanged += OnShuffleChanged;
        Audio.RepeatModeChanged += OnRepeatChanged;
        Audio.VolumeChanged += OnVolumeStateChanged;
        Audio.IsMutedChanged += OnMutedStateChanged;
        Audio.IsFullScreenVisibleChanged += OnFullScreenVisibilityChanged;
    }

    public void Dispose()
    {
        Audio.PlaybackStateChanged -= OnStateChanged;
        Audio.CurrentTimeChanged -= OnTimeChanged;
        Audio.DurationChanged -= OnDurationChanged;
        Audio.BufferedTimeChanged -= OnTimeChanged;
        Audio.CurrentTrackChanged -= OnTrackChanged;
        Audio.QueueChanged -= OnQueueChanged;
        Audio.ShuffleChanged -= OnShuffleChanged;
        Audio.RepeatModeChanged -= OnRepeatChanged;
        Audio.VolumeChanged -= OnVolumeStateChanged;
        Audio.IsMutedChanged -= OnMutedStateChanged;
        Audio.IsFullScreenVisibleChanged -= OnFullScreenVisibilityChanged;
    }

    private void Close() => Audio.ToggleFullScreen();

    private void OnBackdropClick() => Audio.ToggleFullScreen();

    private void ToggleQueue() => _showQueue = !_showQueue;

    private void TogglePlayPause()
    {
        if (Audio.PlaybackState == PlaybackState.Playing)
            Audio.Pause();
        else
            Audio.Play();
    }

    private async Task OnNext() => await Audio.NextAsync();
    private async Task OnPrevious() => await Audio.PreviousAsync();

    private void ToggleMute()
    {
        if (Audio.IsMuted)
            Audio.Unmute();
        else
            Audio.Mute();
    }

    private void OnVolumeChanged(double value) => Audio.SetVolume(value);

    private async Task PlayFromQueue(int index)
    {
        if (index == Audio.CurrentIndex) return;
        await Audio.PlayTracksAsync(Audio.Queue, index);
    }

    private async Task OnSeekClick(MouseEventArgs e)
    {
        if (_isDragging) return;
        await SeekToPosition(e);
    }

    private void OnSeekPointerDown(PointerEventArgs e) => _isDragging = true;

    private async Task OnSeekPointerMove(PointerEventArgs e)
    {
        if (!_isDragging) return;
        await SeekToPosition(e);
    }

    private void OnSeekPointerUp(PointerEventArgs e) => _isDragging = false;

    private async Task SeekToPosition(MouseEventArgs e)
    {
        var bounds = await _seekBarRef.MudGetBoundingClientRectAsync();
        if (bounds.Width <= 0 || Audio.Duration <= 0) return;

        var percent = Math.Clamp((e.ClientX - bounds.Left) / bounds.Width, 0, 1);
        Audio.Seek(percent * Audio.Duration);
    }

    private void OnStateChanged(PlaybackState _) => InvokeAsync(StateHasChanged);
    private void OnDurationChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTimeChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTrackChanged(AudioQueueItem? _) => InvokeAsync(StateHasChanged);
    private void OnQueueChanged() => InvokeAsync(StateHasChanged);
    private void OnShuffleChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnRepeatChanged(RepeatMode _) => InvokeAsync(StateHasChanged);
    private void OnVolumeStateChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnMutedStateChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnFullScreenVisibilityChanged() => InvokeAsync(StateHasChanged);

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
