using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class Carousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Carousel>? _dotnetRef;
    private bool _moduleLoadFailed;
    private volatile bool _disposed;
    private bool _jsWindowReceived;
    private int _lastItemCount = -1;
    private readonly CarouselSlideWindow _slideWindow = new();
    private bool _sliding;
    private bool _gridLimited;
    private bool _gridExpanded;

    /// <summary>The sections of a media page this carousel sits in, when it does: they say whether lists are grids.</summary>
    [CascadingParameter] private MediaSections? Sections { get; set; }

    /// <summary>
    /// Shows every card at once on as many lines as it takes, instead of one line that slides.
    /// Left null, the carousel follows the media page sections it sits in, and slides anywhere else.
    /// </summary>
    [Parameter] public bool? Grid { get; set; }

    /// <summary>
    /// As a grid: the lines shown before a "show more" button. Left null, the carousel follows the
    /// media page sections it sits in, and shows every line anywhere else.
    /// </summary>
    [Parameter] public int? GridRows { get; set; }

    [Parameter] public bool Skeleton { get; set; } = false;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Style { get; set; }
    [Parameter] public bool ShowLoopBack { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Fingerprint of slide ids. Blazor inserts/removes <see cref="CarouselItem"/> nodes via @key
    /// (feed stays FIFO within its page size). When this changes, Embla picks up the new DOM slides:
    /// at snap 0 new cards become visible; mid-carousel the current card stays anchored.
    /// </summary>
    [Parameter] public string? ContentKey { get; set; }

    /// <summary>
    /// Mount MediaCards only for slides near the viewport. Off-screen slides stay as sized placeholders
    /// so Embla snap width is unchanged.
    /// </summary>
    [Parameter] public bool Virtualize { get; set; }

    [Parameter] public int VirtualOverscan { get; set; } = CarouselVirtualWindow.DefaultOverscan;
    [Parameter] public int VirtualAnchorIndex { get; set; }
    [Parameter] public int ItemCount { get; set; }

    private string? _lastContentKey;

    private bool IsGrid => Grid ?? Sections?.Grid ?? false;

    private int? GridRowLimit => IsGrid ? GridRows ?? Sections?.GridRows : null;

    protected override void OnParametersSet()
    {
        _slideWindow.Enabled = Virtualize && !Skeleton && !IsGrid;
        if (!_slideWindow.Enabled)
            return;

        if (_jsWindowReceived && ItemCount == _lastItemCount)
            return;

        ApplyAnchorWindow();
    }

    [JSInvokable]
    public Task OnVisibleSlides(int firstVisible, int lastVisible)
    {
        if (_disposed || !_slideWindow.Enabled)
            return Task.CompletedTask;

        _jsWindowReceived = true;
        var itemCount = ItemCount > 0 ? ItemCount : Math.Max(lastVisible + VirtualOverscan + 1, 1);
        var (first, last) = CarouselVirtualWindow.FromVisibleRange(
            firstVisible, lastVisible, VirtualOverscan, itemCount);
        if (first == _slideWindow.First && last == _slideWindow.Last)
            return Task.CompletedTask;

        _slideWindow.First = first;
        _slideWindow.Last = last;
        _lastItemCount = itemCount;
        return InvokeAsync(StateHasChanged);
    }

    private void ApplyAnchorWindow()
    {
        var itemCount = ItemCount > 0 ? ItemCount : CarouselVirtualWindow.DefaultInitialVisibleCount + VirtualOverscan;
        var (first, last) = CarouselVirtualWindow.FromAnchor(VirtualAnchorIndex, VirtualOverscan, itemCount);
        _slideWindow.First = first;
        _slideWindow.Last = last;
        _lastItemCount = ItemCount;
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return Task.CompletedTask;

        // A grid does not slide: Embla is not started, or is stopped if the setting just changed.
        if (IsGrid)
        {
            if (_sliding)
            {
                _sliding = false;
                StopSlidingAsync().FireAndForget();
            }

            // Every render: the cards may have changed, and so may the limit.
            if (GridRowLimit is not null || _gridLimited)
            {
                _gridLimited = GridRowLimit is not null;
                LimitGridAsync(_gridExpanded ? 0 : GridRowLimit ?? 0).FireAndForget();
            }

            return Task.CompletedTask;
        }

        if (_gridLimited)
        {
            _gridLimited = false;
            LimitGridAsync(0).FireAndForget();
        }

        if (firstRender || !_sliding)
        {
            _sliding = true;
            _lastContentKey = ContentKey;
            // Do not await: EmptyLayout AppReadySignal runs after child OnAfterRenderAsync.
            // Awaiting import(carousel.js)+Embla blocked first paint ~20s on Android TV.
            EnsureInitializedAsync().FireAndForget();
            return Task.CompletedTask;
        }

        if (ContentKey == _lastContentKey)
            return Task.CompletedTask;

        _lastContentKey = ContentKey;
        NotifyItemsChangedAsync().FireAndForget();
        return Task.CompletedTask;
    }

    private void ToggleGridExpanded() => _gridExpanded = !_gridExpanded;

    // rows = 0 shows every line again.
    private async Task LimitGridAsync(int rows)
    {
        await EnsureModuleAsync();
        if (_disposed || _module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("limitGrid", _root, rows);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private async Task EnsureModuleAsync()
    {
        if (_disposed || _moduleLoadFailed || _module is not null)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/carousel.js");
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
            _moduleLoadFailed = true;
        }
    }

    public async Task EnsureInitializedAsync()
    {
        await EnsureModuleAsync();

        if (_disposed || _module is null)
            return;

        try
        {
            if (Virtualize)
                _dotnetRef ??= DotNetObjectReference.Create(this);

            await _module.InvokeVoidAsync("init", _root, _dotnetRef);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private async Task StopSlidingAsync()
    {
        if (_disposed || _module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("destroy", _root);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    public async Task NotifyItemsChangedAsync()
    {
        if (_disposed || _module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("reInit", _root);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    public async Task ScrollToIndexAsync(int index)
    {
        await EnsureInitializedAsync();
        if (_disposed || _module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("scrollToIndex", _root, index);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        var module = _module;
        _module = null;
        var dotnetRef = _dotnetRef;
        _dotnetRef = null;
        dotnetRef?.Dispose();

        if (module is null)
            return;

        try
        {
            await module.InvokeVoidAsync("destroy", _root);
            await module.DisposeAsync();
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}
