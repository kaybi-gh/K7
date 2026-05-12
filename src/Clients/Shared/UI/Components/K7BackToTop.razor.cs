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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/backToTop.js");

        _dotnetRef = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("init", _buttonRef, _dotnetRef, Threshold);
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        InvokeAsync(StateHasChanged);
    }

    private async Task ScrollToTop()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("scrollToTop", _buttonRef);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _buttonRef);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
        _dotnetRef?.Dispose();
    }
}
