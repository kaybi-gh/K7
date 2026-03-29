using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;

namespace K7.Clients.Shared.UI.Components;

public partial class MiniMusicPlayer : IDisposable
{
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private SidebarService SidebarService { get; set; } = default!;

    private ElementReference _progressBarRef;
    private bool _isDragging;

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
        Audio.DurationChanged += OnStateChanged;
        Audio.BufferedTimeChanged += OnTimeChanged;
        Audio.CurrentTrackChanged += OnTrackChanged;
        Audio.QueueChanged += OnQueueChanged;
        Audio.ShuffleChanged += OnShuffleChanged;
        Audio.RepeatModeChanged += OnRepeatChanged;
        Audio.VolumeChanged += OnVolumeStateChanged;
        Audio.IsMutedChanged += OnMutedStateChanged;
        Audio.IsVisibleChanged += OnVisibilityChanged;
        SidebarService.IsOpenOnChange += OnSidebarChanged;
    }

    public void Dispose()
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
        SidebarService.IsOpenOnChange -= OnSidebarChanged;
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

    private async Task OnProgressClick(MouseEventArgs e)
    {
        if (_isDragging) return;
        await SeekToPosition(e);
    }

    private void OnProgressPointerDown(PointerEventArgs e)
    {
        _isDragging = true;
    }

    private async Task OnProgressPointerMove(PointerEventArgs e)
    {
        if (!_isDragging) return;
        await SeekToPosition(e);
    }

    private void OnProgressPointerUp(PointerEventArgs e)
    {
        _isDragging = false;
    }

    private async Task SeekToPosition(MouseEventArgs e)
    {
        var bounds = await _progressBarRef.MudGetBoundingClientRectAsync();
        if (bounds.Width <= 0 || Audio.Duration <= 0) return;

        var percent = Math.Clamp((e.ClientX - bounds.Left) / bounds.Width, 0, 1);
        Audio.Seek(percent * Audio.Duration);
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
    private void OnSidebarChanged() => InvokeAsync(StateHasChanged);

    private void OnProgressKeyDown(KeyboardEventArgs e)
    {
        if (Audio.Duration <= 0) return;

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
