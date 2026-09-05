using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// EventCallback receiver that does not call StateHasChanged.
/// Use for high-frequency TV focus handlers so Home/Explore carousels and Virtualize grids stay mounted.
/// </summary>
public sealed class SuppressRenderEventHandler : IHandleEvent
{
    public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg) =>
        callback.InvokeAsync(arg);
}
