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

    protected override void OnParametersSet()
    {
        _slideWindow.Enabled = Virtualize && !Skeleton;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        if (firstRender)
        {
            _lastContentKey = ContentKey;
            await EnsureInitializedAsync();
            return;
        }

        if (ContentKey == _lastContentKey)
            return;

        _lastContentKey = ContentKey;
        await NotifyItemsChangedAsync();
    }

    public async Task EnsureInitializedAsync()
    {
        if (_disposed || _moduleLoadFailed)
            return;

        if (_module is null)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/carousel.js");
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
                _moduleLoadFailed = true;
                return;
            }
        }

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
