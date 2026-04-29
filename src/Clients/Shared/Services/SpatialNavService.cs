using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class SpatialNavService(IJSRuntime jsRuntime) : ISpatialNavService
{
    public async Task PushLayerAsync(ElementReference element, string type, SpatialNavLayerOptions? options = null)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.pushLayer", element, type, new
        {
            onClose = options?.OnClose,
            restoreFocus = options?.RestoreFocus,
            focusSelector = options?.FocusSelector
        });
    }

    public async Task PopLayerAsync(ElementReference element)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.popLayer", element);
    }

    public async Task FocusFirstAsync(string? selector = null)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.focusFirst", selector);
    }

    public async Task FocusElementAsync(ElementReference element)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.focusElement", element);
    }

    public async Task<bool> IsFocusInsideAsync(ElementReference element)
    {
        return await jsRuntime.InvokeAsync<bool>("SpatialNav.isFocusInside", element);
    }

    public async Task RegisterHomeEscapeAsync<T>(DotNetObjectReference<T> callback, string? homePattern = null) where T : class
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.registerHomeEscape", callback, homePattern);
    }

    public async Task RefreshAsync()
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.refresh");
    }

    public async Task AddSectionAsync(string sectionId, SpatialNavSectionOptions? options = null)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.addSection", sectionId, new
        {
            selector = options?.Selector,
            restrict = options?.Restrict ?? "self-first",
            enterTo = options?.EnterTo ?? "last-focused",
            leaveFor = options?.LeaveFor
        });
    }

    public async Task RemoveSectionAsync(string sectionId)
    {
        await jsRuntime.InvokeVoidAsync("SpatialNav.removeSection", sectionId);
    }
}
