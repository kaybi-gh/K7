using System.Globalization;
using System.Text;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using K7.Clients.Shared.UI;

namespace K7.Clients.Shared.UI.Components.Players;

public enum FullScreenView { Player, Lyrics, Queue }

public partial class FullScreenMusicPlayer : IDisposable
{
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private ElementReference _seekBarRef;
    private bool _isDragging;
    private FullScreenView _view;
    private string? _lyricsLrc;
    private string? _lyrics;
    private float[]? _waveformPeaks;
    private string? _waveformMaskStyle;
    private Guid? _detailsLoadedForMediaId;

    private double CurrentPercent => Audio.Duration > 0 ? (Audio.CurrentTime / Audio.Duration) * 100 : 0;
    private double BufferedPercent => Audio.Duration > 0 ? (Audio.BufferedTime / Audio.Duration) * 100 : 0;

    private string? DominantColorStyle => Audio.CurrentTrack?.CoverDominantColor is { } color
        ? $"--dominant-color: {color};"
        : null;

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

    private void ToggleQueue() => _view = _view == FullScreenView.Queue ? FullScreenView.Player : FullScreenView.Queue;

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
        _waveformPeaks = null;
        _waveformMaskStyle = null;

        var media = await Server.GetMediaAsync(mediaId.Value);
        if (media is MusicTrackDto track)
        {
            _lyricsLrc = track.LyricsLrc;
            _lyrics = track.Lyrics;
            _waveformPeaks = track.WaveformPeaks;
            BuildWaveformMask();
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
        if (Audio.CurrentTrack is { } track)
            track.UserRating = value;
    }

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

    private void OnSeekKeyDown(KeyboardEventArgs e)
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

    private async Task OnQueueItemKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Code is "Enter" or "Space")
        {
            await PlayFromQueue(index);
        }
    }

    private async Task SeekToPosition(MouseEventArgs e)
    {
        var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", _seekBarRef);
        if (bounds.Width <= 0 || Audio.Duration <= 0) return;

        var percent = Math.Clamp((e.ClientX - bounds.Left) / bounds.Width, 0, 1);
        Audio.Seek(percent * Audio.Duration);
    }

    private void OnStateChanged(PlaybackState _) => InvokeAsync(StateHasChanged);
    private void OnDurationChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTimeChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnTrackChanged(AudioQueueItem? _) => InvokeAsync(async () =>
    {
        _detailsLoadedForMediaId = null;
        if (Audio.IsFullScreenVisible)
            await LoadTrackDetailsAsync();
        StateHasChanged();
    });
    private void OnQueueChanged() => InvokeAsync(StateHasChanged);
    private void OnShuffleChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnRepeatChanged(RepeatMode _) => InvokeAsync(StateHasChanged);
    private void OnVolumeStateChanged(double _) => InvokeAsync(StateHasChanged);
    private void OnMutedStateChanged(bool _) => InvokeAsync(StateHasChanged);
    private void OnFullScreenVisibilityChanged() => InvokeAsync(async () =>
    {
        if (Audio.IsFullScreenVisible)
            await LoadTrackDetailsAsync();
        StateHasChanged();
    });

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
