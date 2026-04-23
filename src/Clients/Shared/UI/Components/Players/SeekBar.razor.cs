using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SeekBar : IAsyncDisposable
{
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference SeekBarRef;
    private bool IsHovering;
    private bool _isDragging;
    private bool _isFocused;
    private bool _isScrubbing;
    private bool _preventKeyDefault;
    private int _scrubRepeatCount;
    private System.Timers.Timer? _scrubDecayTimer;
    private double HoverPercent;
    private double HoverTime;
    private double _scrubTime;

    private double SeekBarWidth = 0;
    private double SeekBarLeft;

    [Parameter] public EventCallback<bool> OnDragChanged { get; set; }
    [Parameter] public Uri? ThumbnailsUri { get; set; }
    [Parameter] public List<Chapter> Chapters { get; set; } = [];
    [Parameter] public bool IsVisible { get; set; }

    private const int ThumbWidth = 320;
    private const int ThumbHeight = 180;
    private const int IntervalSeconds = 30;
    private const int ThumbsPerRow = 10;

    private double CurrentPercent => (PlayerService.CurrentTime / PlayerService.Duration) * 100;
    private double BufferedPercent => (PlayerService.BufferedTime / PlayerService.Duration) * 100;

    protected override void OnInitialized()
    {
        PlayerService.DurationChanged += OnDurationChanged;
        PlayerService.CurrentTimeChanged += OnCurrentTimeChanged;
        PlayerService.BufferedTimeChanged += OnBufferedTimeChanged;
        _scrubDecayTimer = new System.Timers.Timer(400) { AutoReset = false };
        _scrubDecayTimer.Elapsed += (_, _) => _scrubRepeatCount = 0;
    }

    private async Task OnPointerDown(PointerEventArgs e)
    {
        if (IsVisible)
        {
            if (SeekBarWidth <= 0)
            {
                var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", SeekBarRef);
                SeekBarWidth = bounds.Width;
                SeekBarLeft = bounds.Left;
            }

            UpdateHover(e.ClientX);
            IsHovering = true;
            _isDragging = true;
            await OnDragChanged.InvokeAsync(true);
        }
    }

    private async Task OnPointerMove(PointerEventArgs e)
    {
        if (!IsVisible) return;

        if (SeekBarWidth <= 0)
        {
            var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", SeekBarRef);
            SeekBarWidth = bounds.Width;
            SeekBarLeft = bounds.Left;
        }

        UpdateHover(e.ClientX);

        if (_isDragging)
        {
            await OnDragChanged.InvokeAsync(true);
        }
        else
        {
            IsHovering = true;
        }
    }

    private async Task OnPointerUp(PointerEventArgs e)
    {
        if (IsVisible && _isDragging)
        {
            if (SeekBarWidth <= 0)
            {
                var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", SeekBarRef);
                SeekBarWidth = bounds.Width;
                SeekBarLeft = bounds.Left;
            }

            if (SeekBarWidth > 0)
            {
                var x = e.ClientX - SeekBarLeft;
                var percent = Math.Clamp(x / SeekBarWidth, 0, 1);
                var seekTime = PlayerService.Duration * percent;

                PlayerService.Seek(seekTime);
            }

            _isDragging = false;
            IsHovering = false;
            await OnDragChanged.InvokeAsync(false);
        }
    }

    private void OnPointerLeave(PointerEventArgs e)
    {
        if (!_isDragging)
        {
            IsHovering = false;
        }
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        _preventKeyDefault = true;
        // Android TV WebView sends empty Code for remote keys; fall back to Key
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;

        if (_isScrubbing)
        {
            switch (code)
            {
                case "ArrowLeft":
                    _scrubRepeatCount++;
                    _scrubDecayTimer?.Stop();
                    _scrubDecayTimer?.Start();
                    _scrubTime = Math.Max(0, _scrubTime - GetScrubStep());
                    HoverPercent = _scrubTime / PlayerService.Duration * 100;
                    HoverTime = _scrubTime;
                    break;
                case "ArrowRight":
                    _scrubRepeatCount++;
                    _scrubDecayTimer?.Stop();
                    _scrubDecayTimer?.Start();
                    _scrubTime = Math.Min(PlayerService.Duration, _scrubTime + GetScrubStep());
                    HoverPercent = _scrubTime / PlayerService.Duration * 100;
                    HoverTime = _scrubTime;
                    break;
                case "Enter":
                    PlayerService.Seek(_scrubTime);
                    _isScrubbing = false;
                    _scrubRepeatCount = 0;
                    IsHovering = false;
                    break;
                case "Escape":
                    _isScrubbing = false;
                    _scrubRepeatCount = 0;
                    IsHovering = false;
                    break;
                default:
                    _preventKeyDefault = false;
                    break;
            }
        }
        else
        {
            switch (code)
            {
                case "Enter":
                    _isScrubbing = true;
                    _scrubRepeatCount = 0;
                    _scrubTime = PlayerService.CurrentTime;
                    HoverPercent = CurrentPercent;
                    HoverTime = _scrubTime;
                    IsHovering = true;
                    break;
                default:
                    _preventKeyDefault = false;
                    break;
            }
        }
    }

    private void OnFocus(FocusEventArgs e)
    {
        _isFocused = true;
    }

    private void OnBlur(FocusEventArgs e)
    {
        _isFocused = false;
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
    }

    private double GetScrubStep()
    {
        return _scrubRepeatCount switch
        {
            <= 5 => 10,
            <= 12 => 20,
            <= 20 => 30,
            <= 30 => 60,
            _ => 120
        };
    }

    private void UpdateHover(double clientX)
    {
        if (SeekBarWidth <= 0) return;

        var relativeX = clientX - SeekBarLeft;
        var percent = Math.Clamp(relativeX / SeekBarWidth, 0, 1);
        HoverPercent = percent * 100;
        HoverTime = PlayerService.Duration * percent;
    }

    private string GetSpriteStyle(double time)
    {
        var index = (int)(time / IntervalSeconds);
        var col = index % ThumbsPerRow;
        var row = index / ThumbsPerRow;

        return $"background-image: url('{ThumbnailsUri}'); " +
               $"background-position: -{col * ThumbWidth}px -{row * ThumbHeight}px; " +
               $"background-size: {ThumbsPerRow * ThumbWidth}px auto; " +
               $"width: {ThumbWidth}px; height: {ThumbHeight}px;";
    }

    private string GetHumanReadableTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.Hours > 0
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    private Chapter? GetHoveredChapter(double seconds)
    {
        for (var i = Chapters.Count - 1; i >= 0; i--)
        {
            if (Chapters[i].Start <= seconds)
                return Chapters[i];
        }

        return null;
    }

    private void OnDurationChanged(double duration)
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnCurrentTimeChanged(double time)
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnBufferedTimeChanged(double time)
    {
        InvokeAsync(StateHasChanged);
    }

    public ValueTask DisposeAsync()
    {
        _scrubDecayTimer?.Dispose();
        PlayerService.DurationChanged -= OnDurationChanged;
        PlayerService.CurrentTimeChanged -= OnCurrentTimeChanged;
        PlayerService.BufferedTimeChanged -= OnBufferedTimeChanged;
        return ValueTask.CompletedTask;
    }

    public class Chapter
    {
        public string? Title { get; set; }
        public double Start { get; set; }
    }
}

internal sealed record BoundingRect(double Left, double Top, double Width, double Height);
