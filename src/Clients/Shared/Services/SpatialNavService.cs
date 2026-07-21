using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class SpatialNavService(IJSRuntime jsRuntime) : ISpatialNavService
{
    public Task PushLayerAsync(ElementReference element, string type, SpatialNavLayerOptions? options = null) =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.pushLayer", element, type, new
        {
            onClose = options?.OnClose,
            restoreFocus = options?.RestoreFocus,
            focusSelector = options?.FocusSelector
        }));

    public Task PopLayerAsync(ElementReference element) =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.popLayer", element));

    public Task AttachLayerCallbackAsync(ElementReference element, object onClose) =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.attachLayerCallback", element, onClose));

    public Task FocusFirstAsync(string? selector = null) =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.focusFirst", selector));

    public Task FocusElementAsync(ElementReference element) =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.focusElement", element));

    public async Task<bool> IsFocusInsideAsync(ElementReference element)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>("SpatialNav.isFocusInside", element);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            System.Diagnostics.Debug.WriteLine($"[SpatialNav] IsFocusInside failed: {ex.Message}");
            return false;
        }
    }

    public Task RegisterHomeEscapeAsync<T>(DotNetObjectReference<T> callback, string? homePattern = null) where T : class =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.registerHomeEscape", callback, homePattern));

    public Task RefreshAsync() =>
        InvokeSafeAsync(() => jsRuntime.InvokeVoidAsync("SpatialNav.refresh"));

    private static async Task InvokeSafeAsync(Func<ValueTask> invoke)
    {
        try
        {
            await invoke();
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            System.Diagnostics.Debug.WriteLine($"[SpatialNav] JS call failed: {ex.Message}");
        }
    }
}
