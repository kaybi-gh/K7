using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Interfaces;

public interface ISpatialNavService
{
    Task PushLayerAsync(ElementReference element, string type, SpatialNavLayerOptions? options = null);

    Task PopLayerAsync(ElementReference element);

    Task FocusFirstAsync(string? selector = null);

    Task FocusElementAsync(ElementReference element);

    Task<bool> IsFocusInsideAsync(ElementReference element);

    Task RegisterHomeEscapeAsync<T>(DotNetObjectReference<T> callback, string? homePattern = null) where T : class;

    Task RefreshAsync();
}

public sealed class SpatialNavLayerOptions
{
    public object? OnClose { get; set; }

    public ElementReference? RestoreFocus { get; set; }

    public string? FocusSelector { get; set; }
}
