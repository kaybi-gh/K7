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

    Task AddSectionAsync(string sectionId, SpatialNavSectionOptions? options = null);

    Task RemoveSectionAsync(string sectionId);
}

public sealed class SpatialNavLayerOptions
{
    public object? OnClose { get; set; }

    public ElementReference? RestoreFocus { get; set; }

    public string? FocusSelector { get; set; }
}

public sealed class SpatialNavSectionOptions
{
    /// <summary>CSS selector for focusable elements in this section. Defaults to [data-sn-section="{id}"] focusables.</summary>
    public string? Selector { get; set; }

    /// <summary>self-first | self-only | none</summary>
    public string Restrict { get; set; } = "self-first";

    /// <summary>last-focused | first | default-element</summary>
    public string EnterTo { get; set; } = "last-focused";

    /// <summary>Optional leaveFor overrides, e.g. { up: "#other-section" }</summary>
    public object? LeaveFor { get; set; }
}
