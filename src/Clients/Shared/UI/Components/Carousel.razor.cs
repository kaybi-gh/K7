using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class Carousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private bool _moduleLoadFailed;
    private volatile bool _disposed;

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

    private string? _lastContentKey;

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
            await _module.InvokeVoidAsync("init", _root);
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
