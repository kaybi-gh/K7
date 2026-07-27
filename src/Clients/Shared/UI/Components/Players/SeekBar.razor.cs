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
    private bool _allowScrubRender;
    private DateTime _lastProgressRenderUtc;
    private string? _preloadedThumbnailsUri;

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
        // While TV/desktop keyboard scrubbing, preview is painted by JS. Block Blazor
        // progress re-renders or the thumb teleports back to a stale HoverPercent.
        if (_isScrubbing && !_allowScrubRender)
            return false;

        if (!_needsRender)
            return false;

        _needsRender = false;
        _allowScrubRender = false;
        return true;
    }

    protected override async Task OnParametersSetAsync()
    {
        await EnsureThumbnailsPreloadedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("K7.SeekBar.init", SeekBarRef, _dotNetRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                _dotNetRef?.Dispose();
                _dotNetRef = null;
            }
        }

        await EnsureThumbnailsPreloadedAsync();
    }

    private async Task EnsureThumbnailsPreloadedAsync()
    {
        var uri = ThumbnailsUri?.ToString();
        if (string.IsNullOrEmpty(uri) || uri == _preloadedThumbnailsUri)
            return;

        _preloadedThumbnailsUri = uri;
        try
        {
            // ThumbnailsUri often arrives after firstRender (source change); preload then.
            await JS.InvokeVoidAsync("K7.preloadImage", uri);
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
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
                ApplyScrubStep(-1);
                RequestScrubUiRender();
                break;
            case "ArrowRight":
                _preventKeyDefault = true;
                ApplyScrubStep(1);
                RequestScrubUiRender();
                break;
        }
    }

    [JSInvokable("OnEditStart")]
    public void OnEditStart()
    {
        if (_isScrubbing)
            return;

        _isScrubbing = true;
        _scrubRepeatCount = 0;
        _scrubTime = PlayerService.CurrentTime;
        HoverPercent = CurrentPercent;
        HoverTime = _scrubTime;
        // Drop Blazor current-position preview; JS paints the scrub preview only.
        IsHovering = false;
        _isFocused = false;
        _ = OnDragChanged.InvokeAsync(true);
        // Must re-render once so showPreview becomes false and the live-position
        // thumb/thumbnail are removed from the DOM (otherwise they stay stuck while
        // JS adds a second pair for the scrub position).
        RequestScrubUiRender();
    }

    [JSInvokable]
    public void ScrubBy(int direction)
    {
        // Fallback for non-JS paths; TV key-repeat uses K7.SeekBar.stepLocal instead.
        if (!_isScrubbing)
            OnEditStart();

        if (direction == 0)
            return;

        ApplyScrubStep(direction < 0 ? -1 : 1);
        RequestScrubUiRender();
    }

    private void ApplyScrubStep(int direction)
    {
        _scrubRepeatCount++;
        _scrubDecayTimer?.Stop();
        _scrubDecayTimer?.Start();
        var step = GetScrubStep();
        _scrubTime = direction < 0
            ? Math.Max(0, _scrubTime - step)
            : Math.Min(PlayerService.Duration, _scrubTime + step);
        HoverPercent = PlayerService.Duration > 0
            ? _scrubTime / PlayerService.Duration * 100
            : 0;
        HoverTime = _scrubTime;
        IsHovering = true;
    }

    [JSInvokable("OnEditCommit")]
    public Task OnEditCommit() => OnEditCommitAt(_scrubTime);

    [JSInvokable]
    public async Task OnEditCommitAt(double scrubTime)
    {
        // Always seek: TV scrub is driven by JS (K7.SeekBar.stepLocal). OnEditStart can be
        // missed on the first arrow while the overlay becomes visible, leaving _isScrubbing false
        // while the thumbnail still moves - guarding on it would drop the commit entirely.
        PlayerService.Seek(Math.Clamp(scrubTime, 0, Math.Max(0, PlayerService.Duration)));

        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        await OnDragChanged.InvokeAsync(false);

        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.afterScrubCommit");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }

        RequestScrubUiRender();
    }

    [JSInvokable("OnEditCancel")]
    public async Task OnEditCancel()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.clearLocalScrub", SeekBarRef);
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }

        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        await OnDragChanged.InvokeAsync(false);

        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.afterScrubCommit");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }

        RequestScrubUiRender();
    }

    /// <summary>
    /// Exit seekbar edit mode without dismissing the video overlay (OK then Escape, no L/R).
    /// Also clears parent scrubbing flags via OnDragChanged(false) without afterScrubCommit hide
    /// when the parent treats wasScrubbing specially - VideoPlayer uses OnRemoteOverlayHidden
    /// for hide; here we only clear SeekBar local state. Parent overlay clears via soft cancel path.
    /// </summary>
    [JSInvokable]
    public void OnEditCancelSoft()
    {
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        // Clear overlay scrubbing flag without HideOverlay (OnDragChanged(false) would hide).
        // Parent Overlay syncs via HandleBack soft path / OnRemoteOverlayHidden.
        RequestScrubUiRender();
    }

    private void OnFocus(FocusEventArgs e)
    {
        _isFocused = true;
        RequestRender();
    }

    private void OnBlur(FocusEventArgs e)
    {
        _isFocused = false;
        // Do not clear scrubbing here. Android TV WebView emits spurious blurs while
        // data-sn-editing is active; scrubbing ends via OnEditCommit / OnEditCancel only.
        RequestRender();
    }

    private double GetScrubStep()
    {
        // Finer steps for keyboard/TV scrub; acceleration still kicks in on long holds.
        return _scrubRepeatCount switch
        {
            <= 4 => 2,
            <= 10 => 5,
            <= 18 => 10,
            <= 28 => 20,
            <= 40 => 30,
            _ => 60
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

    private const double ChapterGapPx = 3;

    private List<ChapterSegment> GetChapterSegments()
    {
        var duration = PlayerService.Duration;
        if (duration <= 0 || Chapters.Count == 0)
            return [];

        var segments = new List<ChapterSegment>();
        for (var i = 0; i < Chapters.Count; i++)
        {
            var start = Chapters[i].Start;
            var end = (i + 1 < Chapters.Count) ? Chapters[i + 1].Start : duration;
            if (end <= start)
                continue;

            segments.Add(new ChapterSegment(start, end, Chapters[i].Title));
        }

        return segments;
    }

    private void OnDurationChanged(double duration)
    {
        if (_isScrubbing)
            return;
        RequestRender();
    }

    private void OnCurrentTimeChanged(double time)
    {
        if (_isScrubbing)
            return;
        RequestProgressRender();
    }

    private void OnBufferedTimeChanged(double time)
    {
        if (_isScrubbing)
            return;
        RequestProgressRender();
    }

    private void RequestProgressRender()
    {
        if (DateTime.UtcNow - _lastProgressRenderUtc < TimeSpan.FromMilliseconds(250))
            return;

        _lastProgressRenderUtc = DateTime.UtcNow;
        RequestRender();
    }

    private void RequestScrubUiRender()
    {
        _allowScrubRender = true;
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
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
            _dotNetRef.Dispose();
            _dotNetRef = null;
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

    private readonly record struct ChapterSegment(double Start, double End, string? Title)
    {
        public double Duration => End - Start;
    }
}

internal sealed record BoundingRect(double Left, double Top, double Width, double Height);
