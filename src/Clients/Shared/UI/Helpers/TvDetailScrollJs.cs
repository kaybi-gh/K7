using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Helpers;

public static class TvDetailScrollJs
{
    public static async Task<bool> TryInitAsync(
        IJSRuntime jsRuntime,
        ElementReference root,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(root.Id))
            return false;

        try
        {
            return await jsRuntime.InvokeAsync<bool>("K7.TvDetailScroll.init", cancellationToken, root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            return false;
        }
    }

    public static async Task TrySyncAsync(
        IJSRuntime jsRuntime,
        ElementReference root,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(root.Id))
            return;

        try
        {
            await jsRuntime.InvokeVoidAsync("K7.TvDetailScroll.sync", cancellationToken, root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public static async Task TryDisposeAsync(
        IJSRuntime jsRuntime,
        ElementReference root,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(root.Id))
            return;

        try
        {
            await jsRuntime.InvokeVoidAsync("K7.TvDetailScroll.dispose", cancellationToken, root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }
}
