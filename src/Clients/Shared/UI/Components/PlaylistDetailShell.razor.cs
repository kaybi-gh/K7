using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class PlaylistDetailShell : IAsyncDisposable
{
    private readonly string _jsId = Guid.NewGuid().ToString("N");
    private ElementReference _root;
    private IJSObjectReference? _module;
    private bool _attached;
    private bool _disposed;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public bool PageScrollable { get; set; }
    [Parameter] public string CssClass { get; set; } = "playlist-detail-page";
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        await EnsureModuleAsync();
        if (_disposed || _module is null || _attached)
            return;

        try
        {
            await _module.InvokeVoidAsync("attach", _jsId, _root);
            _attached = true;
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
        if (_module is not null)
        {
            try
            {
                if (_attached)
                    await _module.InvokeVoidAsync("dispose", _jsId);
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }

            _module = null;
        }

        _attached = false;
    }

    private async Task EnsureModuleAsync()
    {
        if (_module is not null)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/playlistDetailShell.js");
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}
