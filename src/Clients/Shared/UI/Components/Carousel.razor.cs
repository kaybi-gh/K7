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
        if (_moduleLoadFailed)
            return;

        if (_module is null)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/carousel.js");
            }
            catch (JSException)
            {
                _moduleLoadFailed = true;
                return;
            }
        }

        await _module.InvokeVoidAsync("init", _root);
    }

    public async Task NotifyItemsChangedAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("reInit", _root);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("destroy", _root);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException)
            {
            }
        }
    }

    public async Task ScrollToIndexAsync(int index)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("scrollToIndex", _root, index);
        }
    }
}
