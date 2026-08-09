using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7BackToTop : IAsyncDisposable
{
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public int Threshold { get; set; } = 200;

    private ElementReference _buttonRef;
    private IJSObjectReference? _module;
    private bool _visible;
    private DotNetObjectReference<K7BackToTop>? _dotnetRef;
    private volatile bool _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/backToTop.js");
            if (_disposed)
                return;

            _dotnetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", _buttonRef, _dotnetRef, Threshold);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool visible)
    {
        if (_disposed || _visible == visible)
            return;

        _visible = visible;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task ScrollToTop()
    {
        if (_disposed || _module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("scrollToTop", _buttonRef);
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

        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", _buttonRef);
                await module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }
        }

        _dotnetRef?.Dispose();
        _dotnetRef = null;
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}
