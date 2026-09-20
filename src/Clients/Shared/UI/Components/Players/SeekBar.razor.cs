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
    private DateTime _ignoreEditStartUntilUtc;
    private double _lastRenderedClockTime = double.NaN;
    private string? _preloadedThumbnailsUri;

    [Parameter] public EventCallback<bool> OnDragChanged { get; set; }
    [Parameter] public Uri? ThumbnailsUri { get; set; }
    [Parameter] public List<Chapter> Chapters { get; set; } = [];
    [Parameter] public bool IsVisible { get; set; }

    /// <summary>
    /// When false, the bar stays pointer-draggable but is skipped by SpatialNav.
    /// </summary>
    [Parameter] public bool KeyboardFocusable { get; set; } = true;

    /// <summary>
    /// When true, use <see cref="ExternalCurrentTime"/> / Duration / Buffered instead of IPlayerService
    /// (remote-control mode).
    /// </summary>
    [Parameter] public bool UseExternalClock { get; set; }

    [Parameter] public double ExternalCurrentTime { get; set; }
    [Parameter] public double ExternalDuration { get; set; }
    [Parameter] public double ExternalBufferedTime { get; set; }
    [Parameter] public EventCallback<double> OnSeekRequested { get; set; }

    private const int ThumbWidth = 320;
    private const int ThumbHeight = 180;
    private const int IntervalSeconds = 30;
    private const int ThumbsPerRow = 10;

    private double ClockCurrentTime => UseExternalClock ? ExternalCurrentTime : PlayerService.CurrentTime;
    private double ClockDuration => UseExternalClock ? ExternalDuration : PlayerService.Duration;
    private double ClockBufferedTime => UseExternalClock ? ExternalBufferedTime : PlayerService.BufferedTime;

    private double CurrentPercent => ClockDuration > 0
        ? Math.Clamp(ClockCurrentTime / ClockDuration * 100, 0, 100)
        : 0;
    private double BufferedPercent => ClockDuration > 0
        ? Math.Clamp(ClockBufferedTime / ClockDuration * 100, 0, 100)
        : 0;

    protected override void OnInitialized()
    {
        if (!UseExternalClock)
        {
            PlayerService.DurationChanged += OnDurationChanged;
            PlayerService.CurrentTimeChanged += OnCurrentTimeChanged;
            PlayerService.BufferedTimeChanged += OnBufferedTimeChanged;
        }

        _scrubDecayTimer = new System.Timers.Timer(400) { AutoReset = false };
        _scrubDecayTimer.Elapsed += (_, _) => _scrubRepeatCount = 0;
    }

    protected override void OnParametersSet()
    {
        if (UseExternalClock)
            RequestProgressRender();
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
        try
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            // Re-init every render: Blazor may recycle the DOM node and the WeakMap
            // binding from firstRender alone leaves remote Enter with no DotNet target.
            await JS.InvokeVoidAsync("K7.SeekBar.init", SeekBarRef, _dotNetRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
            if (firstRender)
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
        if (!IsVisible)
            return;

        await RefreshBoundsAsync();
        IsHovering = true;
        _isDragging = true;
        UpdateHover(e.ClientX);
        await OnDragChanged.InvokeAsync(true);
        RequestRender();
    }

    private async Task OnPointerMove(PointerEventArgs e)
    {
        if (!IsVisible)
            return;

        await RefreshBoundsAsync();
        if (!_isDragging)
            IsHovering = true;
        UpdateHover(e.ClientX);
        RequestRender();

        if (_isDragging)
            await OnDragChanged.InvokeAsync(true);
    }

    private async Task OnPointerUp(PointerEventArgs e)
    {
        if (!IsVisible || !_isDragging)
            return;

        await RefreshBoundsAsync();
        if (SeekBarWidth > 0)
        {
            var x = e.ClientX - SeekBarLeft;
            var percent = Math.Clamp(x / SeekBarWidth, 0, 1);
            var seekTime = ClockDuration > 0 ? ClockDuration * percent : 0;
            UpdateHover(e.ClientX);
            _isDragging = false;
            IsHovering = false;
            try
            {
                await JS.InvokeVoidAsync("K7.SeekBar.forceExitEdit", SeekBarRef);
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
            await OnDragChanged.InvokeAsync(false);
            RequestRender();
            await SeekToAsync(seekTime);
            return;
        }

        _isDragging = false;
        IsHovering = false;
        await OnDragChanged.InvokeAsync(false);
        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.forceExitEdit", SeekBarRef);
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        RequestRender();
    }

    private async Task RefreshBoundsAsync()
    {
        var bounds = await JS.InvokeAsync<BoundingRect>("K7.getBoundingRect", SeekBarRef);
        SeekBarWidth = bounds.Width;
        SeekBarLeft = bounds.Left;
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
        // Drop late OnEditStart queued before Enter-commit (key-repeat beginSeekBarScrub).
        if (DateTime.UtcNow < _ignoreEditStartUntilUtc)
            return;

        if (_isScrubbing)
            return;

        _isScrubbing = true;
        _scrubRepeatCount = 0;
        _scrubTime = ClockCurrentTime;
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
            : Math.Min(ClockDuration, _scrubTime + step);
        HoverPercent = ClockDuration > 0
            ? _scrubTime / ClockDuration * 100
            : 0;
        HoverTime = _scrubTime;
        IsHovering = true;
    }

    [JSInvokable("OnEditCommit")]
    public void OnEditCommit() => OnEditCommitAt(_scrubTime);

    /// <summary>
    /// Must return immediately. MAUI WebView deadlocks if a [JSInvokable] method
    /// awaits JS while the script stack is still inside invokeMethodAsync
    /// (browser WASM schedules around it - remote panel works there).
    /// JS commitVideoSeekBarScrubIfAny already cleared scrub / resumed SpatialNav.
    /// </summary>
    [JSInvokable]
    public void OnEditCommitAt(double scrubTime)
    {
        // Block late OnEditStart / Arrow key-repeat re-entering edit for a beat.
        _ignoreEditStartUntilUtc = DateTime.UtcNow.AddMilliseconds(600);
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        RequestScrubUiRender();

        _ = InvokeAsync(() => CompleteEditCommitAsync(scrubTime));
    }

    private async Task CompleteEditCommitAsync(double scrubTime)
    {
        try
        {
            await SeekToAsync(scrubTime);
        }
        finally
        {
            try
            {
                await OnDragChanged.InvokeAsync(false);
            }
            catch
            {
            }

            await SafeAfterScrubCommitAsync();
            RequestScrubUiRender();
        }
    }

    [JSInvokable("OnEditCancel")]
    public void OnEditCancel()
    {
        _isScrubbing = false;
        _scrubRepeatCount = 0;
        IsHovering = false;
        RequestScrubUiRender();
        _ = InvokeAsync(CompleteEditCancelAsync);
    }

    private async Task CompleteEditCancelAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.clearLocalScrub", SeekBarRef);
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }

        try
        {
            await OnDragChanged.InvokeAsync(false);
        }
        catch
        {
        }

        await SafeAfterScrubCommitAsync();
        RequestScrubUiRender();
    }

    private async Task SafeAfterScrubCommitAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.SeekBar.afterScrubCommit");
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }
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
        HoverTime = ClockDuration * percent;
    }

    private async Task SeekToAsync(double time)
    {
        // When duration is unknown (0), do not clamp the target to 0 - that sent every
        // remote scrub/click to the start of the file.
        var duration = ClockDuration;
        time = duration > 0 ? Math.Clamp(time, 0, duration) : Math.Max(0, time);
        if (OnSeekRequested.HasDelegate)
            await OnSeekRequested.InvokeAsync(time);
        else
            PlayerService.Seek(time);
    }

    private string GetSpriteStyle(double time)
    {
        var index = (int)(time / IntervalSeconds);
        var col = index % ThumbsPerRow;
        var row = index / ThumbsPerRow;

        var url = ResolveSpriteUrl();
        if (string.IsNullOrEmpty(url))
            return "";

        return $"background-image: url(\"{url}\"); " +
               $"background-position: -{col * ThumbWidth}px -{row * ThumbHeight}px; " +
               $"background-size: {ThumbsPerRow * ThumbWidth}px auto; " +
               $"width: {ThumbWidth}px; height: {ThumbHeight}px;";
    }

    private string? ResolveSpriteUrl()
    {
        if (ThumbnailsUri is null)
            return null;

        var raw = ThumbnailsUri.IsAbsoluteUri ? ThumbnailsUri.AbsoluteUri : ThumbnailsUri.OriginalString;
        return raw.Replace("\"", "%22", StringComparison.Ordinal);
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
        var duration = ClockDuration;
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
        var clock = ClockCurrentTime;
        var jumped = !double.IsNaN(_lastRenderedClockTime)
            && Math.Abs(clock - _lastRenderedClockTime) >= 1.0;
        if (!jumped && DateTime.UtcNow - _lastProgressRenderUtc < TimeSpan.FromMilliseconds(250))
            return;

        _lastProgressRenderUtc = DateTime.UtcNow;
        _lastRenderedClockTime = clock;
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
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
            {
            }
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }

        if (!UseExternalClock)
        {
            PlayerService.DurationChanged -= OnDurationChanged;
            PlayerService.CurrentTimeChanged -= OnCurrentTimeChanged;
            PlayerService.BufferedTimeChanged -= OnBufferedTimeChanged;
        }
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
