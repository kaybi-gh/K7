using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class VerticalCarousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private bool _moduleLoadFailed;
    private int _lastSlideCount = -1;
    private volatile bool _disposed;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        if (_module is null && !_moduleLoadFailed)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/vertical-carousel.js");
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
            if (firstRender)
            {
                await _module.InvokeVoidAsync("init", _root);
                if (_disposed)
                    return;
                _lastSlideCount = await _module.InvokeAsync<int>("getSlideCount", _root);
                return;
            }

            var slideCount = await _module.InvokeAsync<int>("getSlideCount", _root);
            if (_disposed)
                return;

            // Always schedule a remeasure: explore rows remount skeleton->content
            // without changing slide count, which otherwise leaves a stale viewport
            // height and lets the next shelf peek under the current one.
            _lastSlideCount = slideCount;
            await _module.InvokeVoidAsync("refresh", _root);
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
