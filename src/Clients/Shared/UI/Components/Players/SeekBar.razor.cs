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
    private DotNetObjectReference<SeekBar>? _dotNetRef;
    private bool _needsRender = true;
    private DateTime _lastProgressRenderUtc;

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

    protected override bool ShouldRender()
    {
        if (!_needsRender)
            return false;

        _needsRender = false;
        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("K7.SeekBar.init", SeekBarRef, _dotNetRef);
                if (ThumbnailsUri is not null)
                {
                    // Preload the thumbnail sprite to avoid freeze on first seekbar hovering
                    await JS.InvokeVoidAsync("eval", $"(new Image()).src = '{ThumbnailsUri}'");
                }
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }
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
            RequestRender();
        }
    }

    private async Task OnPointerMove(PointerEventArgs e)
    {
        if (!IsVisible) return;

        var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", SeekBarRef);
        SeekBarWidth = bounds.Width;
        SeekBarLeft = bounds.Left;

        UpdateHover(e.ClientX);
        RequestRender();

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
            RequestRender();
        }
    }

    private void OnPointerLeave(PointerEventArgs e)
    {
        if (!_isDragging)
        {
            IsHovering = false;
        }

        RequestRender();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        _preventKeyDefault = false;
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;

        if (!_isScrubbing)
            return;

        switch (code)
        {
            case "ArrowLeft":
                _preventKeyDefault = true;
                _scrubRepeatCount++;
                _scrubDecayTimer?.Stop();
                _scrubDecayTimer?.Start();
                _scrubTime = Math.Max(0, _scrubTime - GetScrubStep());
                HoverPercent = _scrubTime / PlayerService.Duration * 100;
                HoverTime = _scrubTime;
                break;
            case "ArrowRight":
                _preventKeyDefault = true;
                _scrubRepeatCount++;
                _scrubDecayTimer?.Stop();
                _scrubDecayTimer?.Start();
                _scrubTime = Math.Min(PlayerService.Duration, _scrubTime + GetScrubStep());
                HoverPercent = _scrubTime / PlayerService.Duration * 100;
                HoverTime = _scrubTime;
                break;
        }

        RequestRender();
    }

    [JSInvokable("OnEditStart")]
    public void OnEditStart()
    {
        _isScrubbing = true;
        _scrubRepeatCount = 0;
        _scrubTime = PlayerService.CurrentTime;
        HoverPercent = CurrentPercent;
        HoverTime = _scrubTime;
        IsHovering = true;
        _ = OnDragChanged.InvokeAsync(true);
        RequestRender();
    }

    [JSInvokable("OnEditCommit")]
    public async Task OnEditCommit()
    {
        if (_isScrubbing)
        {
            PlayerService.Seek(_scrubTime);
        }
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        await OnDragChanged.InvokeAsync(false);
        RequestRender();
    }

    [JSInvokable("OnEditCancel")]
    public async Task OnEditCancel()
    {
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        await OnDragChanged.InvokeAsync(false);
        RequestRender();
    }

    private void OnFocus(FocusEventArgs e)
    {
        _isFocused = true;
        RequestRender();
    }

    private void OnBlur(FocusEventArgs e)
    {
        _isFocused = false;
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        RequestRender();
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
        RequestRender();
    }

    private void OnCurrentTimeChanged(double time)
    {
        RequestProgressRender();
    }

    private void OnBufferedTimeChanged(double time)
    {
        RequestProgressRender();
    }

    private void RequestProgressRender()
    {
        if (DateTime.UtcNow - _lastProgressRenderUtc < TimeSpan.FromMilliseconds(250))
            return;

        _lastProgressRenderUtc = DateTime.UtcNow;
        RequestRender();
    }

    private void RequestRender()
    {
        _needsRender = true;
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _scrubDecayTimer?.Dispose();
        if (_dotNetRef is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("K7.SeekBar.dispose", SeekBarRef);
            }
            catch (JSDisconnectedException)
            {
            }
            _dotNetRef.Dispose();
        }
        PlayerService.DurationChanged -= OnDurationChanged;
        PlayerService.CurrentTimeChanged -= OnCurrentTimeChanged;
        PlayerService.BufferedTimeChanged -= OnBufferedTimeChanged;
    }

    public class Chapter
    {
        public string? Title { get; set; }
        public double Start { get; set; }
    }
}

internal sealed record BoundingRect(double Left, double Top, double Width, double Height);
